using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SportTournaments.Tests;

// Real relational SQLite, isolated from production MySQL; HTTP stubs at the network boundary.
internal sealed class TestDatabase<T> : IDisposable where T : DbContext
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public T Context { get; }
    public TestDatabase()
    {
        connection.Open();
        var options = new DbContextOptionsBuilder<T>().UseSqlite(connection).Options;
        Context = (T)Activator.CreateInstance(typeof(T), options)!;
        Context.Database.EnsureCreated();
    }
    public void Dispose() { Context.Dispose(); connection.Dispose(); }
}

internal class StubHttpHandler(Func<string, object?> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var result = response(request.RequestUri!.AbsolutePath);
        if (result is Exception exception) throw exception;
        return Task.FromResult(result is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) });
    }
    public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("http://test/") };
}

internal class FixedTime : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2030-01-01T00:00:00Z");
}
