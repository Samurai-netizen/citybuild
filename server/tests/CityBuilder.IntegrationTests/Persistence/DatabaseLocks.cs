using System.Globalization;
using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// Row locks in the tests on a test's database: a server with another lock timeout, a transaction that holds a city's lock until the test
/// releases it, and a wait until PostgreSQL shows sessions waiting for a lock, so a test knows a request is blocked, not merely slow.
/// </summary>
internal static class DatabaseLocks
{
    /// <summary>
    /// A server like <paramref name="factory"/> on the same database and with the same clock, with another lock timeout
    /// (<c>Database:LockTimeoutMilliseconds</c>).
    /// </summary>
    /// <param name="factory">The server.</param>
    /// <param name="milliseconds">The lock timeout of the derived server.</param>
    /// <returns>The derived server; the test disposes it.</returns>
    public static WebApplicationFactory<Program> WithLockTimeout(WebApplicationFactory<Program> factory, int milliseconds) =>
        factory.WithWebHostBuilder(host =>
            host.UseSetting("Database:LockTimeoutMilliseconds", milliseconds.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Begins one request's city transaction for the player and holds the city's row lock until <paramref name="release"/> completes, then
    /// commits without a save. The returned task completes when the transaction has ended.
    /// </summary>
    /// <param name="factory">The server.</param>
    /// <param name="playerId">The player whose city is locked.</param>
    /// <param name="locked">Set once the lock is held.</param>
    /// <param name="release">Completes when the test lets the lock go.</param>
    /// <returns>A task that completes when the transaction has committed.</returns>
    public static async Task HoldAsync(
        WebApplicationFactory<Program> factory, Guid playerId, TaskCompletionSource locked, Task release)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICityTransactionRunner>().RunAsync(
            playerId,
            async (_, ct) =>
            {
                locked.SetResult();
                await release.WaitAsync(ct);
                return 0;
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Waits until at least <paramref name="sessions"/> sessions on the test's database wait for a lock in PostgreSQL
    /// (<c>pg_stat_activity</c>). Polls every 20 ms; fails after 500 polls (about 10 s).
    /// </summary>
    /// <param name="database">The test's database.</param>
    /// <param name="sessions">How many sessions must be waiting.</param>
    /// <returns>A task that completes when that many sessions wait for a lock.</returns>
    public static async Task WaitUntilWaitingAsync(TestDatabase database, int sessions)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using CityBuilderDbContext db = database.CreateContext();
        for (int poll = 0; poll < 500; poll++)
        {
            int waiting = await db.Database.SqlQuery<int>(
                $"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'")
                .SingleAsync(cancellationToken);
            if (waiting >= sessions)
            {
                return;
            }

            // A pause for the other PostgreSQL sessions to get to their locks; it measures no game time.
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }

        Assert.Fail($"Fewer than {sessions} sessions waited for a lock within 500 polls (about 10 s).");
    }
}
