using CityBuilder.Application.Cities;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.Content;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;
using static CityBuilder.IntegrationTests.Persistence.DatabaseLocks;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// Concurrent requests on PostgreSQL (ADR-0009: each request row-locks only the acting player's own city): many settles of one city run one
/// after the other and end exactly as one settle at the final time, with no production counted twice and no update lost; settles and
/// transactions of different cities don't wait for each other. The cities are the frame saw city on the server's content
/// (<see cref="DatabaseCities.WithFrameSawRunning"/>), last settled at <see cref="PostgresApiFactory.Start"/>, where the clock starts.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class ConcurrencyDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>How many requests each test runs at once.</summary>
    private const int _parallel = 20;

    /// <summary>The state version of a city as stored before each test: its 0, saved once.</summary>
    private const long _storedVersion = 1;

    /// <summary>
    /// The lock timeout of the server the queued settles run on: 30 s, so the first of 20 settles queued behind a held lock doesn't give up
    /// while the test waits for the last one to queue (the default 3 s could run out on a slow machine).
    /// </summary>
    private const int _queueLockTimeoutMilliseconds = 30_000;

    /// <summary>
    /// The lock timeout of the server the settles of other cities run on: 200 ms, so a settle that waited for another city's lock fails.
    /// </summary>
    private const int _shortLockTimeoutMilliseconds = 200;

    /// <summary>
    /// 20 settles of one city queued behind its row lock, all started before the clock moves on 1 h and the lock is let go, run one after the
    /// other: the first settles the hour and saves; each next one sees what the one before committed, so it finds nothing to settle and
    /// doesn't save. The city ends at the next version, as the same city settled once at that time (its twin), with the same ledger.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TwentyQueuedBehindTheLock_EndAsOneSettleAtTheFinalTime()
    {
        (Guid player, Guid city, Guid twinPlayer, Guid twinCity) = await StoreCityAndTwinAsync();
        await using WebApplicationFactory<Program> server = WithLockTimeout(Factory, _queueLockTimeoutMilliseconds);

        SettleCityResult[] results = await QueueBehindTheLockAsync(server, player, _parallel, TimeSpan.FromHours(1));
        await SettleAsync(server, twinPlayer);


        Assert.Single(results, result => result.Saved);
        CityAggregate stored = await LoadAsync(server, player);
        Assert.Equal(_storedVersion + 1, stored.StateVersion);
        Assert.Equal(StateOf(await LoadAsync(server, twinPlayer)), StateOf(stored));
        Assert.Equal(Totals(await LedgerAsync(Database, twinCity)), Totals(await LedgerAsync(Database, city)));
    }

    /// <summary>
    /// 20 settles of one city in four waves of five, each wave queued behind the city's held lock while the clock moves on 25 min (100 min in
    /// all, so the 1-hour research completes between the second and the third wave): in each wave the first settle saves and the others find
    /// nothing left to settle, so the version rises once per wave, and the city ends exactly as the same city settled once at the final
    /// time, with the same ledger totals.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TwentyInWavesWhileTimePasses_EndAsOneSettleAtTheFinalTime()
    {
        const int waves = 4;
        (Guid player, Guid city, Guid twinPlayer, Guid twinCity) = await StoreCityAndTwinAsync();
        await using WebApplicationFactory<Program> server = WithLockTimeout(Factory, _queueLockTimeoutMilliseconds);
        var results = new List<SettleCityResult>();

        for (int wave = 0; wave < waves; wave++)
        {
            results.AddRange(await QueueBehindTheLockAsync(server, player, _parallel / waves, TimeSpan.FromMinutes(25)));
        }

        SettleCityResult once = await SettleAsync(server, twinPlayer);

        Assert.Equal([FrameSaw], once.Settlement.CompletedResearch);
        Assert.Equal(waves, results.Count(result => result.Saved));
        CityAggregate stored = await LoadAsync(server, player);
        Assert.Equal(_storedVersion + waves, stored.StateVersion);
        Assert.Equal(StateOf(once.City), StateOf(stored));
        Assert.Equal(Totals(await LedgerAsync(Database, twinCity)), Totals(await LedgerAsync(Database, city)));
    }

    /// <summary>
    /// While one city's row lock is held, settles of 19 other cities, run in parallel on a server with a 200 ms lock timeout, all finish and
    /// save: none waits for the held lock or for another's. The held city is still locked when they are done.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OtherCitiesWhileOneIsLocked_FinishWithoutWaiting()
    {
        Guid[] players = await StoreCitiesAsync(_parallel);
        await using WebApplicationFactory<Program> server = WithLockTimeout(Factory, _shortLockTimeoutMilliseconds);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task holder = HoldAsync(Factory, players[0], locked, release.Task);
        await locked.Task.WaitAsync(TestContext.Current.CancellationToken);
        Factory.Clock.Advance(TimeSpan.FromHours(1));

        SettleCityResult[] results = await Task.WhenAll(players.Skip(1).Select(player => Task.Run(() => SettleAsync(server, player))));

        Assert.False(holder.IsCompleted);
        release.SetResult();
        await holder;
        Assert.All(results, result => Assert.True(result.Saved));
        Assert.Equal(players.Skip(1), results.Select(result => result.City.PlayerId));
    }

    /// <summary>
    /// 20 transactions on 20 cities hold their row locks at the same time: each waits inside its transaction until all 20 hold theirs, which
    /// can only happen if no lock waits for another (a wait would end in the 200 ms lock timeout); then each commits a save.
    /// </summary>
    [Fact]
    public async Task RunAsync_TwentyCities_HoldTheirLocksAtTheSameTime()
    {
        Guid[] players = await StoreCitiesAsync(_parallel);
        await using WebApplicationFactory<Program> server = WithLockTimeout(Factory, _shortLockTimeoutMilliseconds);
        var allLocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int holding = 0;

        await Task.WhenAll(players.Select(player => Task.Run(() => RunAsync(server, player, async (transaction, ct) =>
        {
            if (Interlocked.Increment(ref holding) == _parallel)
            {
                allLocked.SetResult();
            }

            await allLocked.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            transaction.City.MarkSettled(transaction.City.LastSettledUnixSec + 60, transaction.City.ContentVersion);
            await transaction.SaveAsync([], receipt: null, ct);
        }))));

        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(
            Enumerable.Repeat(_storedVersion + 1, _parallel),
            await db.Cities.Select(row => row.StateVersion).ToListAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Holds the player's city lock, starts <paramref name="settles"/> settles of it in parallel, waits until PostgreSQL shows all of them
    /// waiting for the lock, moves the clock on by <paramref name="later"/>, lets the lock go, and waits for every settle.
    /// </summary>
    /// <param name="server">The server the settles run on.</param>
    /// <param name="player">The player whose city is settled.</param>
    /// <param name="settles">How many settles to queue.</param>
    /// <param name="later">How far the clock moves on while they wait.</param>
    /// <returns>What the settles did, in the order they were started.</returns>
    private async Task<SettleCityResult[]> QueueBehindTheLockAsync(
        WebApplicationFactory<Program> server, Guid player, int settles, TimeSpan later)
    {
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task holder = HoldAsync(server, player, locked, release.Task);
        await locked.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task<SettleCityResult>[] queued = [.. Enumerable.Range(0, settles).Select(_ => Task.Run(() => SettleAsync(server, player)))];
        await WaitUntilWaitingAsync(Database, settles);
        Factory.Clock.Advance(later);
        release.SetResult();
        await holder;
        return await Task.WhenAll(queued);
    }

    /// <summary>Stores the frame saw city for a player and the same city for a second player, its twin, both last settled at the clock's start.</summary>
    /// <returns>The first player and city, then the twin's player and city.</returns>
    private async Task<(Guid Player, Guid City, Guid TwinPlayer, Guid TwinCity)> StoreCityAndTwinAsync()
    {
        GameContent content = ContentOf(Factory);
        long start = PostgresApiFactory.Start.ToUnixTimeSeconds();
        (Guid player, Guid city, Guid twinPlayer, Guid twinCity) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await StoreAsync(Factory, new Player(player, "racer"), WithFrameSawRunning(content, city, player, start));
        await StoreAsync(Factory, new Player(twinPlayer, "twin"), WithFrameSawRunning(content, twinCity, twinPlayer, start));
        return (player, city, twinPlayer, twinCity);
    }

    /// <summary>Stores <paramref name="count"/> players, each with the frame saw city of their own, last settled at the clock's start.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The players' ids, in the order they were stored.</returns>
    private async Task<Guid[]> StoreCitiesAsync(int count)
    {
        GameContent content = ContentOf(Factory);
        long start = PostgresApiFactory.Start.ToUnixTimeSeconds();
        Guid[] players = [.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())];
        for (int i = 0; i < count; i++)
        {
            await StoreAsync(Factory, new Player(players[i], $"player{i}"), WithFrameSawRunning(content, Guid.NewGuid(), players[i], start));
        }

        return players;
    }

    /// <summary>Runs one request's city transaction on a server: a new scope, its runner, and the work.</summary>
    /// <param name="factory">The server.</param>
    /// <param name="playerId">The acting player.</param>
    /// <param name="work">The work on the locked city.</param>
    /// <returns>A task that completes when the transaction has committed.</returns>
    private static async Task RunAsync(
        WebApplicationFactory<Program> factory, Guid playerId, Func<CityTransaction, CancellationToken, Task> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICityTransactionRunner>().RunAsync(
            playerId,
            async (transaction, ct) =>
            {
                await work(transaction, ct);
                return 0;
            },
            TestContext.Current.CancellationToken);
    }
}
