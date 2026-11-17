using CityBuilder.Domain.Metrics;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Repositories;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The player events' persistence without a database (CR-0009, <c>06</c> <c>player_events</c>): what
/// <see cref="PlayerEventRepository.Add"/> stages, and the column list, which is the privacy guard of the table.
/// <see cref="PlayerEventDatabaseTests"/> writes the rows to PostgreSQL.
/// </summary>
public sealed class PlayerEventPersistenceTests
{
    /// <summary>The player of the events.</summary>
    private static readonly Guid _player = new("91a7e400-0000-4000-8000-0000000000e1");

    /// <summary>The server time the events were recorded at.</summary>
    private static readonly DateTimeOffset _at = new(2026, 11, 17, 18, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Each event is staged as one inserted row with its values, and <c>created_at</c> is the time the event was recorded (not a database
    /// default), so the rows of one request share it.
    /// </summary>
    [Fact]
    public void Add_TwoEvents_StagesOneRowEachWithItsValues()
    {
        using CityBuilderDbContext db = PersistenceTestContext.Create();

        new PlayerEventRepository(db).Add(
        [
            new PlayerEvent(_player, "building.completed", "woodcutter", null, 2, 3600, _at),
            new PlayerEvent(_player, "command.place-building", "sawmill", "INSUFFICIENT_RESOURCE", null, 3600, _at),
        ]);

        List<PlayerEventRow> rows = [.. db.ChangeTracker.Entries<PlayerEventRow>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .OrderBy(row => row.EventType, StringComparer.Ordinal)];
        Assert.Equal(
            [
                (_player, "building.completed", "woodcutter", (string?)null, (long?)2, 3600L, _at),
                (_player, "command.place-building", "sawmill", "INSUFFICIENT_RESOURCE", null, 3600L, _at),
            ],
            rows.Select(row => (row.PlayerId, row.EventType, row.Detail, row.Outcome, row.Value, row.CityClock, row.CreatedAt)));
    }

    /// <summary>No events stage nothing.</summary>
    [Fact]
    public void Add_NoEvents_StagesNothing()
    {
        using CityBuilderDbContext db = PersistenceTestContext.Create();

        new PlayerEventRepository(db).Add([]);

        Assert.Empty(db.ChangeTracker.Entries());
    }

    /// <summary>A null list is refused.</summary>
    [Fact]
    public void Add_Null_Throws()
    {
        using CityBuilderDbContext db = PersistenceTestContext.Create();

        Assert.Throws<ArgumentNullException>(() => new PlayerEventRepository(db).Add(null!));
    }

    /// <summary>
    /// The privacy guard (CR-0009, <c>07</c> → Logging policy): <c>player_events</c> has exactly these columns, and its row type exactly
    /// these properties. A row holds the player's id and content ids, never a display name, a token, an address, a device or a request
    /// body; a new column fails here first, and adding one is the owner's decision.
    /// </summary>
    [Fact]
    public void PlayerEvents_Columns_AreExactlyThese()
    {
        using CityBuilderDbContext db = PersistenceTestContext.Create();
        ITable table = Assert.Single(
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables, t => t.Name == "player_events");

        Assert.Equal(
            ["city_clock", "created_at", "detail", "event_type", "id", "outcome", "player_id", "value"],
            table.Columns.Select(column => column.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["CityClock", "CreatedAt", "Detail", "EventType", "Id", "Outcome", "PlayerId", "Value"],
            typeof(PlayerEventRow).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}
