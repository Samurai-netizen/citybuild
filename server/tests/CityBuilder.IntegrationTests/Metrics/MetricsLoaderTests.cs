extern alias MetricsTool;

using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MetricsTool::CityBuilder.MetricsReport;
using Npgsql;

namespace CityBuilder.IntegrationTests.Metrics;

/// <summary>
/// The metrics report's loader on PostgreSQL (docs/plan/CR-0009-T2-PLAN.md §3.1, §4 step 6): four seeded players with known answers.
/// Alice (created 2026-11-01 10:00, last seen 12:00 that day) placed a woodcutter, built it, and her city produced logs and planks; the
/// dev account <c>devtest</c> (2026-11-02) came once; Bob (2026-11-04 23:59:59) came once and is still playing; Carol (2026-11-05 00:00)
/// has only her creation. Every player has a <c>player.created</c> event. The report runs on 2026-11-20, so all but Bob left.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class MetricsLoaderTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>PostgreSQL's error code for a write in a read-only transaction.</summary>
    private const string _readOnlyTransaction = "25006";

    /// <summary>Alice: the player with commands, buildings and production.</summary>
    private static readonly Guid _alice = new("3e7c0000-0000-4000-8000-0000000000a1");

    /// <summary>The dev account, which <c>--exclude devtest</c> removes.</summary>
    private static readonly Guid _devtest = new("3e7c0000-0000-4000-8000-0000000000d1");

    /// <summary>Bob: created in the last second of 2026-11-04, still playing.</summary>
    private static readonly Guid _bob = new("3e7c0000-0000-4000-8000-0000000000b1");

    /// <summary>Carol: created in the first second of 2026-11-05, only her creation.</summary>
    private static readonly Guid _carol = new("3e7c0000-0000-4000-8000-0000000000c1");

    /// <summary>Alice's creation.</summary>
    private static readonly DateTimeOffset _aliceCreated = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The machine's clock of the report.</summary>
    private static readonly DateTimeOffset _clock = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Without a filter every player is loaded with their creation and last seen, and every event with all its values.</summary>
    [Fact]
    public async Task Load_NoFilter_LoadsEveryPlayerAndTheirEvents()
    {
        await SeedAsync();

        MetricsData data = await LoadAsync(new LoadFilter(null, null, []));

        Assert.Equal([_alice, _bob, _carol, _devtest], data.Players.Select(p => p.Id).Order());
        MetricsPlayer alice = data.Players.Single(p => p.Id == _alice);
        Assert.Equal((_aliceCreated, _aliceCreated.AddHours(2).ToUnixTimeSeconds()), (alice.CreatedAt, alice.LastSeenUnix));
        MetricsEvent built = Assert.Single(data.Events, e => e.EventType == "building.completed");
        Assert.Equal(
            (_alice, "woodcutter", (string?)null, (long?)1, 3600L, _aliceCreated.AddHours(1)),
            (built.PlayerId, built.Detail, built.Outcome, built.Value, built.CityClock, built.CreatedAt));
        Assert.Equal(10, data.Events.Count(e => e.EventType != "production.first"));
    }

    /// <summary>
    /// <c>--exclude devtest</c> removes that player and their events, and no statement selects a dev name: <c>dev_name</c> appears only
    /// after a statement's first <c>WHERE</c>. A loaded player holds no text at all.
    /// </summary>
    [Fact]
    public async Task Load_Exclude_RemovesThePlayerAndSelectsNoName()
    {
        await SeedAsync();
        var log = new List<string>();

        MetricsData data = await LoadAsync(new LoadFilter(null, null, ["devtest"]), log);

        Assert.DoesNotContain(_devtest, data.Players.Select(p => p.Id));
        Assert.DoesNotContain(data.Events, e => e.PlayerId == _devtest);
        Assert.Equal(3, data.Players.Count);
        string[] statements = [.. log.Where(line => line.Contains("SELECT", StringComparison.Ordinal))];
        Assert.Contains(statements, s => s.Contains("dev_name", StringComparison.Ordinal));
        Assert.All(statements, s => Assert.DoesNotContain("dev_name", s.Split("WHERE")[0], StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(MetricsPlayer).GetProperties(), p => p.PropertyType == typeof(string));
    }

    /// <summary>The period keeps the players created from the first day's start to the last day's end (UTC), with their events.</summary>
    [Fact]
    public async Task Load_Period_KeepsThePlayersCreatedInIt()
    {
        await SeedAsync();

        MetricsData data = await LoadAsync(new LoadFilter(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4), []));

        Assert.Equal([_bob, _devtest], data.Players.Select(p => p.Id).Order());
        Assert.Equal([_bob, _devtest], data.Events.Select(e => e.PlayerId).Distinct().Order());
    }

    /// <summary>
    /// <c>production.first</c> per item from the first <c>SETTLEMENT_PRODUCED</c> ledger row of the player's city: logs at 10:30 (not the
    /// later row, not the consumed one), planks at 11:00; its city clock is the one of the player's last event at or before it.
    /// </summary>
    [Fact]
    public async Task Load_ProductionFirst_FromTheFirstProducedLedgerRowPerItem()
    {
        await SeedAsync();

        MetricsData data = await LoadAsync(new LoadFilter(null, null, ["devtest"]));

        MetricsEvent[] first = [.. data.Events.Where(e => e.EventType == "production.first").OrderBy(e => e.Detail, StringComparer.Ordinal)];
        Assert.Equal(
            [("logs", _aliceCreated.AddMinutes(30), 300L), ("planks", _aliceCreated.AddHours(1), 3600L)],
            first.Select(e => (e.Detail!, e.CreatedAt, e.CityClock)));
        Assert.All(first, e => Assert.Equal((_alice, (string?)null, (long?)null), (e.PlayerId, e.Outcome, e.Value)));
    }

    /// <summary>
    /// The explorer and a funnel on the loaded data give the answers worked out by hand: "left after", "left without" and "last stop",
    /// with a checkpoint nobody reached and one everybody reached ("—", no division by zero).
    /// </summary>
    [Fact]
    public async Task Load_ThenExplorerAndFunnel_GiveTheKnownAnswers()
    {
        await SeedAsync();
        CheckpointFile file = new(
            [
                new Checkpoint("created", "Created", "player.created", null, null, null, 1),
                new Checkpoint("came", "Came", "session.start", null, null, null, 1),
                new Checkpoint("placed", "Placed a woodcutter", "command.place-building", "woodcutter", "accepted", null, 1),
                new Checkpoint("never", "Completed a research", "research.completed", null, null, null, 1),
            ],
            [new Funnel("First steps", ["came", "placed"])]);

        ReportContext context = ReportContext.Create(await LoadAsync(new LoadFilter(null, null, [])), _clock, 3, 1);
        Dictionary<string, ExplorerRow> rows = Explorer.Build(context, file).Where(r => r.Checkpoint.IsNamed).ToDictionary(r => r.Checkpoint.Key);

        Assert.Equal(3, context.LeftCount);
        string[] keys = ["created", "came", "placed", "never"];
        Assert.Equal([4, 3, 1, 0], keys.Select(k => rows[k].Reached));
        Assert.Equal(["75.0", "66.7", "100.0", Rate.None], keys.Select(k => rows[k].LeftAfter.ToString()));
        Assert.Equal([Rate.None, "100.0", "66.7", "75.0"], keys.Select(k => rows[k].LeftWithout.ToString()));
        Assert.Equal([1, 1, 1, 0], keys.Select(k => rows[k].LastStop));
        FunnelTable funnel = Assert.Single(Funnels.Build(context, file));
        Assert.Equal([3, 1], funnel.Steps.Select(s => s.Reached));
        Assert.Equal(["75.0", "33.3"], funnel.Steps.Select(s => s.OfPrevious.ToString()));
        Assert.Equal([1, 2], funnel.Steps.Select(s => s.Drop));
        Assert.Equal([0L, 300L], funnel.Steps.Select(s => s.MedianSecondsFromPrevious));
    }

    /// <summary>The loader's transaction is read-only: a write inside it fails and changes nothing.</summary>
    [Fact]
    public async Task ReadOnly_AWriteInside_Fails()
    {
        await SeedAsync();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using CityBuilderDbContext db = Database.CreateContext();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => MetricsLoader.ReadOnlyAsync(
            db, token => db.Database.ExecuteSqlRawAsync("DELETE FROM player_events", token), cancellationToken));

        Assert.Equal(_readOnlyTransaction, refused.SqlState);
        await using CityBuilderDbContext check = Database.CreateContext();
        Assert.Equal(10, await check.PlayerEvents.CountAsync(cancellationToken));
    }

    /// <summary>Loads the test database with the given filter.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="log">Where the context's log lines go, or none.</param>
    /// <returns>The data.</returns>
    private async Task<MetricsData> LoadAsync(LoadFilter filter, List<string>? log = null)
    {
        DbContextOptionsBuilder<CityBuilderDbContext> options = new DbContextOptionsBuilder<CityBuilderDbContext>().UseNpgsql(Database.ConnectionString);
        if (log is not null)
        {
            options.LogTo(log.Add, LogLevel.Information);
        }

        await using var db = new CityBuilderDbContext(options.Options);
        return await MetricsLoader.LoadAsync(db, filter, TestContext.Current.CancellationToken);
    }

    /// <summary>Stores the four players, their cities, their ten events and the ledger rows the class comment names.</summary>
    /// <returns>A task that completes when the rows are stored.</returns>
    private async Task SeedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using CityBuilderDbContext db = Database.CreateContext();
        DateTimeOffset devtestCreated = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset bobCreated = new(2026, 11, 4, 23, 59, 59, TimeSpan.Zero);
        DateTimeOffset carolCreated = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        Guid aliceCity = AddPlayer(db, _alice, "alice", _aliceCreated, _aliceCreated.AddHours(2));
        Guid devtestCity = AddPlayer(db, _devtest, "devtest", devtestCreated, devtestCreated.AddMinutes(10));
        AddPlayer(db, _bob, "bob", bobCreated, new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
        AddPlayer(db, _carol, "carol", carolCreated, carolCreated);
        await db.SaveChangesAsync(cancellationToken);

        AddEvent(db, _alice, "player.created", null, null, null, 0, _aliceCreated);
        AddEvent(db, _alice, "session.start", null, null, null, 0, _aliceCreated);
        AddEvent(db, _alice, "command.place-building", "woodcutter", "accepted", null, 300, _aliceCreated.AddMinutes(5));
        AddEvent(db, _alice, "building.completed", "woodcutter", null, 1, 3600, _aliceCreated.AddHours(1));
        AddEvent(db, _devtest, "player.created", null, null, null, 0, devtestCreated);
        AddEvent(db, _devtest, "session.start", null, null, null, 0, devtestCreated);
        AddEvent(db, _bob, "player.created", null, null, null, 0, bobCreated);
        AddEvent(db, _bob, "session.start", null, null, null, 0, bobCreated);
        AddEvent(db, _carol, "player.created", null, null, null, 0, carolCreated);
        await db.SaveChangesAsync(cancellationToken);
        AddEvent(db, _alice, "session.start", null, null, null, 7000, _aliceCreated.AddHours(1).AddMinutes(50));
        await db.SaveChangesAsync(cancellationToken);

        AddLedger(db, aliceCity, "logs", -1, 0, "SETTLEMENT_CONSUMED", _aliceCreated.AddMinutes(20));
        AddLedger(db, aliceCity, "logs", 2, 2, "SETTLEMENT_PRODUCED", _aliceCreated.AddMinutes(30));
        AddLedger(db, aliceCity, "planks", 1, 1, "SETTLEMENT_PRODUCED", _aliceCreated.AddHours(1));
        AddLedger(db, aliceCity, "logs", 4, 6, "SETTLEMENT_PRODUCED", _aliceCreated.AddHours(1).AddMinutes(30));
        AddLedger(db, devtestCity, "logs", 2, 2, "SETTLEMENT_PRODUCED", devtestCreated.AddMinutes(5));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Adds a player and their city.</summary>
    /// <param name="db">The context.</param>
    /// <param name="id">The player id.</param>
    /// <param name="devName">The dev name.</param>
    /// <param name="createdAt">The creation.</param>
    /// <param name="lastSeen">The city's last seen.</param>
    /// <returns>The city's id.</returns>
    private static Guid AddPlayer(CityBuilderDbContext db, Guid id, string devName, DateTimeOffset createdAt, DateTimeOffset lastSeen)
    {
        var cityId = Guid.CreateVersion7(createdAt);
        db.Players.Add(new PlayerRow { Id = id, DevName = devName, CreatedAt = createdAt });
        db.Cities.Add(new CityRow
        {
            Id = cityId, PlayerId = id, Era = 1, LastSettledUnix = lastSeen.ToUnixTimeSeconds(), LastSeenUnix = lastSeen.ToUnixTimeSeconds(),
            NextBuildingId = 1, StateVersion = 1, ContentVersion = "mvp-3", CreatedAt = createdAt, UpdatedAt = lastSeen,
        });
        return cityId;
    }

    /// <summary>Adds an event.</summary>
    /// <param name="db">The context.</param>
    /// <param name="player">The player.</param>
    /// <param name="type">The event type.</param>
    /// <param name="detail">The detail.</param>
    /// <param name="outcome">The outcome.</param>
    /// <param name="value">The value.</param>
    /// <param name="cityClock">The city clock.</param>
    /// <param name="at">The time.</param>
    private static void AddEvent(
        CityBuilderDbContext db, Guid player, string type, string? detail, string? outcome, long? value, long cityClock, DateTimeOffset at) =>
        db.PlayerEvents.Add(new PlayerEventRow
        {
            PlayerId = player, EventType = type, Detail = detail, Outcome = outcome, Value = value, CityClock = cityClock, CreatedAt = at,
        });

    /// <summary>Adds a ledger row.</summary>
    /// <param name="db">The context.</param>
    /// <param name="city">The city.</param>
    /// <param name="item">The item.</param>
    /// <param name="delta">The change.</param>
    /// <param name="balanceAfter">The total after it.</param>
    /// <param name="reason">The reason code.</param>
    /// <param name="at">The time.</param>
    private static void AddLedger(CityBuilderDbContext db, Guid city, string item, long delta, long balanceAfter, string reason, DateTimeOffset at) =>
        db.LedgerEntries.Add(new LedgerEntryRow
        {
            CityId = city, ItemId = item, Delta = delta, BalanceAfter = balanceAfter, Reason = reason, RefType = "settlement", CreatedAt = at,
        });
}
