using CityBuilder.Application.Cities;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// Cities built by hand on the server's real content and stored in PostgreSQL, for the command tests that need states the commands can't
/// reach yet over HTTP (a producing trading post, a stock at its cap, a staffed level-2 woodcutter), and the text views of the stored rows
/// those tests compare. Shared by <c>UpgradeBuildingDatabaseTests</c> (M0-31) and the assign-workers tests (M0-32). The numbers in the
/// builders' comments are the real content's (<c>server/content/mvp-content.json</c>).
/// </summary>
internal static class HandBuiltCities
{
    /// <summary>
    /// Stores a new player with a city at clock 0 on the server's content, era 1, no research, last settled at
    /// <see cref="PostgresApiFactory.Start"/>. The zero-length advance a command ends with runs first, so the stored city is canonical and a
    /// request with no time elapsed saves nothing of its own.
    /// </summary>
    /// <param name="factory">The server on the test's database.</param>
    /// <param name="name">The player's dev name.</param>
    /// <param name="coins">The coins in stock.</param>
    /// <param name="logs">The logs in stock.</param>
    /// <param name="planks">The planks in stock.</param>
    /// <param name="buildings">The buildings, with ids from 1 up.</param>
    /// <returns>The player and their city.</returns>
    public static async Task<(Guid Player, Guid City)> StoreCityAsync(
        PostgresApiFactory factory, string name, long coins, long logs, long planks, params Building[] buildings)
    {
        GameContent content = ContentOf(factory);
        var state = new CityState(
            0,
            era: 1,
            nextBuildingId: buildings.Length + 1,
            [new(new ItemId("coins"), coins), new(new ItemId("logs"), logs), new(new ItemId("planks"), planks)],
            buildings,
            [],
            activeResearch: null);
        Simulator.Advance(state, content, 0);
        (Guid player, Guid city) = (Guid.NewGuid(), Guid.NewGuid());
        var aggregate = new CityAggregate(city, player, state, [], PostgresApiFactory.Start.ToUnixTimeSeconds(), 0, content.ContentVersion);
        await StoreAsync(factory, new Player(player, name), aggregate);
        return (player, city);
    }

    /// <summary>A completed, idle house (4 workers at level 1, 6 at level 2) on row 0.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="level">Its level.</param>
    /// <param name="x">Its column.</param>
    /// <returns>The building.</returns>
    public static Building House(int id, int level = 1, int x = 0) =>
        new(id, new BuildingTypeId("house"), x, 0, level, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

    /// <summary>
    /// A woodcutter on row 1 (60 labor a log at both levels; max workers 2 at level 1, 4 at level 2): <c>Constructing</c> from 0 to its 60
    /// seconds, <c>Upgrading</c> from 0 to level 2's 120 seconds, <c>Idle</c>, or <c>Producing</c> a cycle of 60 labor since clock 0 with its
    /// workers.
    /// </summary>
    /// <param name="id">Its id.</param>
    /// <param name="phase">Its phase.</param>
    /// <param name="workers">Its workers (0 while constructing).</param>
    /// <param name="x">Its column.</param>
    /// <param name="level">Its level (1 while constructing or upgrading).</param>
    /// <returns>The building.</returns>
    public static Building Woodcutter(int id, BuildingPhase phase, int workers, int x = 1, int level = 1) => phase switch
    {
        BuildingPhase.Constructing => new(id, new BuildingTypeId("woodcutter"), x, 1, 1, 0, phase, 0, 60, 0, 0, 0, 0),
        BuildingPhase.Upgrading => new(id, new BuildingTypeId("woodcutter"), x, 1, 1, workers, phase, 0, 120, 2, 0, 0, 0),
        BuildingPhase.Producing => new(id, new BuildingTypeId("woodcutter"), x, 1, level, workers, phase, 0, 60 / workers, 0, 60, 60, 0),
        _ => new(id, new BuildingTypeId("woodcutter"), x, 1, level, workers, phase, 0, null, 0, 0, 0, 0),
    };

    /// <summary>A level-1 sawmill at (1,2) producing since clock 0 with 2 workers: 180 labor, ends at 90; its 2 logs are in the cycle.</summary>
    /// <param name="id">Its id.</param>
    /// <returns>The building.</returns>
    public static Building Sawmill(int id) =>
        new(id, new BuildingTypeId("sawmill"), 1, 2, 1, 2, BuildingPhase.Producing, 0, 90, 0, 180, 180, 0);

    /// <summary>A level-1 trading post at (1,2) producing since clock 0 with 2 workers: 120 labor, ends at 60; its 2 planks are in the cycle.</summary>
    /// <param name="id">Its id.</param>
    /// <returns>The building.</returns>
    public static Building TradingPost(int id) =>
        new(id, new BuildingTypeId("trading_post"), 1, 2, 1, 2, BuildingPhase.Producing, 0, 60, 0, 120, 120, 0);

    /// <summary>A ledger row as text: item, delta, balance, reason, reference type and id.</summary>
    /// <param name="row">The row.</param>
    /// <returns>For example <c>coins -150 850 COST_UPGRADE command …</c>.</returns>
    public static string Line(LedgerEntryRow row) => $"{row.ItemId} {row.Delta} {row.BalanceAfter} {row.Reason} {row.RefType} {row.RefId}";

    /// <summary>A stored building row as text, read by a context of the test's own.</summary>
    /// <param name="database">The test's database.</param>
    /// <param name="city">The city.</param>
    /// <param name="buildingId">The building.</param>
    /// <returns>For example <c>2 woodcutter L1 Upgrading 0-120 target 2 workers 0</c>.</returns>
    public static async Task<string> BuildingAsync(TestDatabase database, Guid city, int buildingId)
    {
        await using CityBuilderDbContext db = database.CreateContext();
        CityBuildingRow row = await db.CityBuildings.AsNoTracking()
            .SingleAsync(building => building.CityId == city && building.BuildingId == buildingId, TestContext.Current.CancellationToken);
        return $"{row.BuildingId} {row.TypeId} L{row.Level} {row.Phase} {row.PhaseStartedAt}-{row.PhaseEndsAt} target {row.TargetLevel ?? 0} workers {row.AssignedWorkers}";
    }

    /// <summary>A city's stored stock above 0 as text in ordinal item order, read by a context of the test's own.</summary>
    /// <param name="database">The test's database.</param>
    /// <param name="city">The city.</param>
    /// <returns>For example <c>coins 850, logs 100, planks 30</c>.</returns>
    public static async Task<string> StockAsync(TestDatabase database, Guid city)
    {
        await using CityBuilderDbContext db = database.CreateContext();
        List<CityInventoryRow> rows = await db.CityInventory.AsNoTracking().Where(row => row.CityId == city && row.Quantity > 0)
            .ToListAsync(TestContext.Current.CancellationToken);
        return string.Join(", ", rows.OrderBy(row => row.ItemId, StringComparer.Ordinal).Select(row => $"{row.ItemId} {row.Quantity}"));
    }
}
