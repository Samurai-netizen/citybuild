using CityBuilder.Domain.Metrics;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Repositories;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// <c>player_events</c> on PostgreSQL (CR-0009, <c>06</c>): events written through <see cref="PlayerEventRepository"/> come back with
/// every value, nulls stay nulls, the ids follow the order the events were given in (the report orders a player's events by it), the
/// events go with their player, and the database refuses a negative city clock and an unknown player.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class PlayerEventDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>PostgreSQL's error code for a check violation.</summary>
    private const string _checkViolation = "23514";

    /// <summary>PostgreSQL's error code for a foreign key violation.</summary>
    private const string _foreignKeyViolation = "23503";

    /// <summary>The player every test stores.</summary>
    private static readonly Guid _player = new("91a7e400-0000-4000-8000-0000000000e2");

    /// <summary>The server time the events were recorded at, a whole second (the column keeps microseconds).</summary>
    private static readonly DateTimeOffset _at = new(2026, 11, 17, 18, 30, 0, TimeSpan.Zero);

    /// <summary>An event with every value set is read back with each of them.</summary>
    [Fact]
    public async Task Add_EventWithEveryValue_IsStoredAsGiven()
    {
        await StorePlayerWithEventsAsync(new PlayerEvent(_player, "building.completed", "woodcutter", "accepted", 2, 3600, _at));

        await using CityBuilderDbContext db = Database.CreateContext();
        PlayerEventRow row = await db.PlayerEvents.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            (_player, "building.completed", "woodcutter", "accepted", (long?)2, 3600L, _at),
            (row.PlayerId, row.EventType, row.Detail, row.Outcome, row.Value, row.CityClock, row.CreatedAt));
        Assert.True(row.Id > 0);
    }

    /// <summary>An event without a detail, an outcome and a value is stored with three nulls, not empty strings or 0.</summary>
    [Fact]
    public async Task Add_EventWithoutOptionalValues_StoresNulls()
    {
        await StorePlayerWithEventsAsync(new PlayerEvent(_player, "session.start", null, null, null, 0, _at));

        await using CityBuilderDbContext db = Database.CreateContext();
        PlayerEventRow row = await db.PlayerEvents.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(("session.start", (string?)null, (string?)null, (long?)null, 0L), (row.EventType, row.Detail, row.Outcome, row.Value, row.CityClock));
    }

    /// <summary>
    /// The events of one save get increasing ids in the order they were given: one request's events share their <c>created_at</c>, so the
    /// id is what orders them (the session start, then the settlement, then the command).
    /// </summary>
    [Fact]
    public async Task Add_SeveralEvents_AreNumberedInTheOrderGiven()
    {
        await StorePlayerWithEventsAsync(
            new PlayerEvent(_player, "session.start", null, null, null, 100, _at),
            new PlayerEvent(_player, "research.completed", "masonry", null, null, 100, _at),
            new PlayerEvent(_player, "building.completed", "woodcutter", null, 1, 100, _at),
            new PlayerEvent(_player, "command.place-building", "quarry", "accepted", null, 100, _at));

        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(
            ["session.start", "research.completed", "building.completed", "command.place-building"],
            await db.PlayerEvents.OrderBy(row => row.Id).Select(row => row.EventType).ToListAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Deleting a player deletes their events (<c>ON DELETE CASCADE</c>): analytics rows don't block it, unlike the ledger.</summary>
    [Fact]
    public async Task DeletePlayer_WithEvents_DeletesTheirEvents()
    {
        await StorePlayerWithEventsAsync(
            new PlayerEvent(_player, "player.created", null, null, null, 0, _at),
            new PlayerEvent(_player, "session.start", null, null, null, 0, _at));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using CityBuilderDbContext db = Database.CreateContext();

        await db.Players.Where(row => row.Id == _player).ExecuteDeleteAsync(cancellationToken);

        Assert.Equal(0, await db.PlayerEvents.CountAsync(cancellationToken));
    }

    /// <summary>A row with a city clock below 0 breaks <c>city_clock &gt;= 0</c>, whatever the code above the database does.</summary>
    [Fact]
    public async Task SaveChangesAsync_NegativeCityClock_ViolatesTheClockCheck()
    {
        await StorePlayerWithEventsAsync();
        await using CityBuilderDbContext db = Database.CreateContext();
        db.PlayerEvents.Add(new PlayerEventRow { PlayerId = _player, EventType = "session.start", CityClock = -1, CreatedAt = _at });

        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        PostgresException postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal((_checkViolation, "ck_player_events_city_clock"), (postgres.SqlState, postgres.ConstraintName));
    }

    /// <summary>An event of a player that doesn't exist breaks the foreign key to <c>players</c>.</summary>
    [Fact]
    public async Task SaveChangesAsync_EventOfAnUnknownPlayer_ViolatesTheForeignKey()
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        new PlayerEventRepository(db).Add([new PlayerEvent(_player, "session.start", null, null, null, 0, _at)]);

        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        PostgresException postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal((_foreignKeyViolation, "fk_player_events_players_player_id"), (postgres.SqlState, postgres.ConstraintName));
    }

    /// <summary>Stores the player and then, in a second save, their events through the repository.</summary>
    /// <param name="events">The events, in order; may be none.</param>
    /// <returns>A task that completes when both saves are committed.</returns>
    private async Task StorePlayerWithEventsAsync(params PlayerEvent[] events)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using CityBuilderDbContext db = Database.CreateContext();
        db.Players.Add(new PlayerRow { Id = _player, DevName = "dev1", CreatedAt = _at });
        await db.SaveChangesAsync(cancellationToken);

        new PlayerEventRepository(db).Add(events);
        await db.SaveChangesAsync(cancellationToken);
    }
}
