using Microsoft.Extensions.Http.Resilience;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SportTournaments.Registration;
using SportTournaments.Registration.Domain;
using SportTournaments.Registration.API;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0))));
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddHttpClient<TournamentApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:TournamentService"]!);
}).AddStandardResilienceHandler(options =>
{
    options.Retry.DisableForUnsafeHttpMethods();
});
builder.Services.AddHttpClient<ParticipantApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ParticipantService"]!);
}).AddStandardResilienceHandler(options =>
{
    options.Retry.DisableForUnsafeHttpMethods();
});

var app = builder.Build();
app.UseMiddleware<ErrorHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { service = "Registration", status = "Healthy" })
        : Results.Problem(statusCode: 503, title: "Database unavailable"));

// The workshop applies migrations on startup. Compose waits for healthy MySQL first.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
app.Run();

