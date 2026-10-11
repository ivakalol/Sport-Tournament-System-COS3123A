using Microsoft.Extensions.Http.Resilience;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SportTournaments.Tournament;
using SportTournaments.Tournament.Domain;
using SportTournaments.Tournament.API;

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
builder.Services.AddScoped<TournamentService>();
builder.Services.AddHttpClient<RegistrationApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:RegistrationService"]!);
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
        ? Results.Ok(new { service = "Tournament", status = "Healthy" })
        : Results.Problem(statusCode: 503, title: "Database unavailable"));

// The workshop applies migrations on startup. Compose waits for healthy MySQL first.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
app.Run();

