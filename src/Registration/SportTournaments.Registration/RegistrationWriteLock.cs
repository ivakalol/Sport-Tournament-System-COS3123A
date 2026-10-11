using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SportTournaments.Registration.Domain;

namespace SportTournaments.Registration;

// MySQL named locks are shared by all instances using this Registration database.
// One short write at a time protects capacity and participant schedules across parallel signups.
internal sealed class RegistrationWriteLock(AppDbContext context) : IAsyncDisposable
{
    private DbConnection? connection;

    public async Task AcquireAsync(CancellationToken ct)
    {
        // SQLite is used only by isolated service tests; production is always MySQL.
        if (!context.Database.IsMySql()) return;
        await context.Database.OpenConnectionAsync(ct);
        connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT GET_LOCK('SportTournaments.Registration.Write', 10)";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            throw new HttpRequestException("Registration is busy. Retry later.");
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is null) return;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT RELEASE_LOCK('SportTournaments.Registration.Write')";
            await command.ExecuteScalarAsync(CancellationToken.None);
        }
        finally { await context.Database.CloseConnectionAsync(); }
    }
}
