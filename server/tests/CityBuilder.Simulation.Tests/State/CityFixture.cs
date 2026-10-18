using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// Builds cities and buildings for the state and rule tests, over the synthetic <see cref="ContentFixture"/> (its own numbers, not the game's).
/// Each building helper sets exactly the fields its phase uses, so a test names only what matters to it.
/// </summary>
internal static class CityFixture
{
    /// <summary>
    /// The valid fixture content (a 6 × 5 grid, two builders, a house <c>hut</c>, producers <c>lumber_camp</c> and, locked, <c>workshop</c>)
    /// with three additions for the rules: the workshop gets a level 2, so an upgrade can refund a cycle's inputs; the lumber camp's level 2
    /// takes 4 workers, for the spec's example of 3 workers becoming 4; and <c>manor</c>, an era-2 house available at the start (10 gold,
    /// 5 s), for the era check of placement.
    /// </summary>
    public static GameContent TestContent { get; } = ContentAssert.Valid(ContentFixture.ValidWith(data =>
    {
        ContentFixture.Level(data, "lumber_camp", 2).MaxWorkers = 4;
        ContentFixture.Building(data, "workshop").Levels!.Add(new BuildingLevelData
        {
            Level = 2,
            MaxWorkers = 3,
            Recipe = new RecipeData { Inputs = new() { ["wood"] = 2 }, Outputs = new() { ["boards"] = 1 }, Labor = 90 },
            UpgradeCost = new() { ["gold"] = 30, ["wood"] = 1 },
            UpgradeSeconds = 15,
        });
        data.Buildings!.Add(new BuildingData
        {
            Id = "manor",
            Name = "Manor",
            Kind = "house",
            Era = 2,
            AvailableAtStart = true,
            Cost = new() { ["gold"] = 10 },
            BuildSeconds = 5,
            Levels = [new BuildingLevelData { Level = 1, WorkersProvided = 4 }],
        });
    }));

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
