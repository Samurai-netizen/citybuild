using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// Builds cities and buildings for the state and rule tests, over the synthetic <see cref="ContentFixture"/> (its own numbers, not the game's).
/// Each building helper sets exactly the fields its phase uses, so a test names only what matters to it.
/// </summary>
internal static class CityFixture
{
    /// <summary>The valid fixture content: a 6 × 5 grid, two builders, a house <c>hut</c>, producers <c>lumber_camp</c> and (locked) <c>workshop</c>.</summary>
    public static GameContent TestContent { get; } = ContentAssert.Valid(ContentFixture.Valid());

    /// <summary>
    /// A city at <paramref name="clock"/> (default 1000) in era 1. The next building id defaults to one above the highest id; the inventory
    /// to nothing.
    /// </summary>
    public static CityState City(
        long clock = 1000,
        int era = 1,
        (string Item, long Quantity)[]? inventory = null,
        Building[]? buildings = null,
        string[]? completedResearch = null,
        ActiveResearch? activeResearch = null,
        int? nextBuildingId = null)
    {
        buildings ??= [];
        int nextId = nextBuildingId ?? (buildings.Length == 0 ? 1 : buildings.Max(building => building.Id) + 1);
        return new CityState(
            clock,
            era,
            nextId,
            (inventory ?? []).Select(stock => new KeyValuePair<ItemId, long>(new ItemId(stock.Item), stock.Quantity)),
            buildings,
            (completedResearch ?? []).Select(id => new ResearchId(id)),
            activeResearch);
    }

    public static Building Idle(int id, string type, int x, int y, int level = 1, int workers = 0) =>
        new(id, new BuildingTypeId(type), x, y, level, workers, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

    public static Building Constructing(int id, string type, int x, int y, long startedAt, long endsAt) =>
        new(id, new BuildingTypeId(type), x, y, 1, 0, BuildingPhase.Constructing, startedAt, endsAt, 0, 0, 0, 0);

    public static Building Upgrading(int id, string type, int x, int y, int level, int workers, long startedAt, long endsAt) =>
        new(id, new BuildingTypeId(type), x, y, level, workers, BuildingPhase.Upgrading, startedAt, endsAt, level + 1, 0, 0, 0);

    /// <summary>A producing building whose cycle started at <paramref name="cycleStartedAt"/>; <paramref name="endsAt"/> null means frozen.</summary>
    public static Building Producing(
        int id, string type, int x, int y, int level, int workers, long cycleStartedAt, long cycleLabor, long laborRemaining, long segmentStartedAt, long? endsAt) =>
        new(id, new BuildingTypeId(type), x, y, level, workers, BuildingPhase.Producing, cycleStartedAt, endsAt, 0, cycleLabor, laborRemaining, segmentStartedAt);
}
