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
    /// The valid fixture content (a 6 × 5 grid, two builders, a house <c>hut</c>, producers <c>lumber_camp</c> and, locked, <c>workshop</c>,
    /// the warehouse <c>depot</c>, the courier station <c>post</c>, the science building <c>school</c>) with four additions for the rules: the
    /// workshop gets a level 2 with 180 labor, up to 6 workers and a store of 40 wood and 160 boards for 30 gold (coins only, so it starts at
    /// once), so an upgrade can refund a cycle's inputs and raise the worker limit, and the spec's durations (180 labor: 4 workers 45 s, 5
    /// workers 36 s) run on it; the lumber camp's level 2 takes 4 workers, for the spec's example of 3 workers becoming 4, and costs 30 gold and
    /// 2 boards, so its upgrade waits for the boards while the camp keeps producing (CR-0008 D12); <c>manor</c>, an era-2 house available at the start (10 gold, 5 s),
    /// for the era check of placement; and <c>mint</c>, a producer of the currency (5 gold per 20 labor, up to 2 workers, no store), whose
    /// output goes into the purse.
    /// </summary>
    public static GameContent TestContent { get; } = ContentAssert.Valid(ContentFixture.ValidWith(data =>
    {
        // "!" reaches into the valid fixture, which sets the building list and every building's levels (ContentFixture.Valid).
        ContentFixture.Level(data, "lumber_camp", 2).MaxWorkers = 4;
        ContentFixture.Level(data, "lumber_camp", 2).UpgradeCost = new() { ["gold"] = 30, ["boards"] = 2 };
        ContentFixture.Building(data, "workshop").Levels!.Add(new BuildingLevelData
        {
            Level = 2,
            MaxWorkers = 6,
            Recipe = new RecipeData { Inputs = new() { ["wood"] = 2 }, Outputs = new() { ["boards"] = 1 }, Labor = 180 },
            Storage = new() { ["wood"] = 40, ["boards"] = 160 },
            UpgradeCost = new() { ["gold"] = 30 },
            UpgradeSeconds = 15,
        });
        data.Buildings!.Add(new BuildingData
        {
            Id = "mint",
            Name = "Mint",
            Kind = "producer",
            Era = 1,
            AvailableAtStart = true,
            Cost = new() { ["gold"] = 10 },
            BuildSeconds = 5,
            Levels = [new BuildingLevelData { Level = 1, MaxWorkers = 2, Recipe = new RecipeData { Outputs = new() { ["gold"] = 5 }, Labor = 20 } }],
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
    /// A city at <paramref name="clock"/> (default 1000) in era 1. The next building id defaults to one above the highest id, the next trip id
    /// to one above the highest trip's; the purse to 0; the roads, trips, couriers and routes to nothing. Goods live in the buildings' stores
    /// (the building helpers' <c>store</c>), on trips and with couriers.
    /// </summary>
    public static CityState City(
        long clock = 1000,
        int era = 1,
        long purse = 0,
        Building[]? buildings = null,
        string[]? completedResearch = null,
        ActiveResearch? activeResearch = null,
        int? nextBuildingId = null,
        RoadTile[]? roads = null,
        Trip[]? trips = null,
        int? nextTripId = null,
        Courier[]? couriers = null,
        Route[]? routes = null)
    {
        buildings ??= [];
        trips ??= [];
        int nextId = nextBuildingId ?? (buildings.Length == 0 ? 1 : buildings.Max(building => building.Id) + 1);
        return new CityState(
            clock,
            era,
            nextId,
            purse,
            buildings,
            (completedResearch ?? []).Select(id => new ResearchId(id)),
            activeResearch,
            roads,
            trips,
            nextTripId ?? (trips.Length == 0 ? 1 : trips.Max(trip => trip.Id) + 1),
            couriers,
            routes);
    }

    /// <summary>A courier at its station with <paramref name="stamina"/> and the given load parts (good, the building it came from, quantity).</summary>
    public static Courier AtStation(int stationId, int slot, long stamina = 600, params (string Item, int FromId, long Quantity)[] load) =>
        new(stationId, slot, CourierState.AtStation, 0, null, null, stamina, Load(load));

    /// <summary>A courier walking to stop <paramref name="toStop"/> of its route from <paramref name="departedAt"/> to <paramref name="arrivesAt"/>.</summary>
    public static Courier Walking(
        int stationId, int slot, int toStop, long departedAt, long arrivesAt, long stamina = 600, params (string Item, int FromId, long Quantity)[] load) =>
        new(stationId, slot, CourierState.Walking, toStop, departedAt, arrivesAt, stamina, Load(load));

    /// <summary>A route of a station's slot over stops written as (building, actions), each action as (take?, good, amount text).</summary>
    public static Route RouteOf(int stationId, int slot, params (int BuildingId, (bool Take, string Item, string Amount)[] Actions)[] stops) =>
        new(stationId, slot, Stops(stops));

    /// <summary>Stops written as (building, actions), each action as (take?, good, amount text), for a route or for <c>SetRoute</c>.</summary>
    public static RouteStop[] Stops(params (int BuildingId, (bool Take, string Item, string Amount)[] Actions)[] stops) =>
        stops.Select(stop => new RouteStop(stop.BuildingId, stop.Actions.Select(action => new RouteAction(
            action.Take ? RouteActionKind.Take : RouteActionKind.Put,
            new ItemId(action.Item),
            Amount.TryParse(action.Amount, out Amount? amount) ? amount : throw new ArgumentException("Not an amount: " + action.Amount))))).ToArray();

    /// <summary>Load parts written as (good, the building it came from, quantity).</summary>
    private static CourierLoad[] Load((string Item, int FromId, long Quantity)[] load) =>
        load.Select(part => new CourierLoad(new ItemId(part.Item), part.FromId, part.Quantity)).ToArray();

    /// <summary>A trip under way: <paramref name="quantity"/> of <paramref name="item"/> from building <paramref name="sourceId"/> to <paramref name="targetId"/>.</summary>
    public static Trip Trip(int id, int sourceId, int targetId, string item, long quantity, long departedAt, long arrivesAt) =>
        new(id, sourceId, targetId, new ItemId(item), quantity, departedAt, arrivesAt);

    /// <summary>
    /// An idle building in canonical form: phase start 0, and no end, target level or cycle; with <paramref name="needs"/> its upgrade waits for
    /// those goods.
    /// </summary>
    public static Building Idle(
        int id, string type, int x, int y, int level = 1, int workers = 0, (string Item, long Quantity)[]? store = null, (string Item, long Quantity)[]? needs = null) =>
        new(id, new BuildingTypeId(type), x, y, level, workers, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, Pairs(store), Pairs(needs));

    /// <summary>A building under construction from <paramref name="startedAt"/> to <paramref name="endsAt"/>, level 1 and no workers.</summary>
    public static Building Constructing(int id, string type, int x, int y, long startedAt, long endsAt) =>
        new(id, new BuildingTypeId(type), x, y, 1, 0, BuildingPhase.Constructing, startedAt, endsAt, 0, 0, 0, 0);

    /// <summary>A construction site: constructing without a timer, waiting for <paramref name="needs"/> (at least one good).</summary>
    public static Building Site(int id, string type, int x, int y, (string Item, long Quantity)[] needs, (string Item, long Quantity)[]? store = null) =>
        new(id, new BuildingTypeId(type), x, y, 1, 0, BuildingPhase.Constructing, 0, null, 0, 0, 0, 0, Pairs(store), Pairs(needs));

    /// <summary>A building upgrading from <paramref name="level"/> to the next, keeping its workers, from <paramref name="startedAt"/> to <paramref name="endsAt"/>.</summary>
    public static Building Upgrading(
        int id, string type, int x, int y, int level, int workers, long startedAt, long endsAt, (string Item, long Quantity)[]? store = null) =>
        new(id, new BuildingTypeId(type), x, y, level, workers, BuildingPhase.Upgrading, startedAt, endsAt, level + 1, 0, 0, 0, Pairs(store));

    /// <summary>
    /// A producing building whose cycle started at <paramref name="cycleStartedAt"/>; <paramref name="endsAt"/> null means frozen; with
    /// <paramref name="needs"/> its upgrade waits for those goods.
    /// </summary>
    public static Building Producing(
        int id,
        string type,
        int x,
        int y,
        int level,
        int workers,
        long cycleStartedAt,
        long cycleLabor,
        long laborRemaining,
        long segmentStartedAt,
        long? endsAt,
        (string Item, long Quantity)[]? store = null,
        (string Item, long Quantity)[]? needs = null) =>
        new(id, new BuildingTypeId(type), x, y, level, workers, BuildingPhase.Producing, cycleStartedAt, endsAt, 0, cycleLabor, laborRemaining, segmentStartedAt,
            Pairs(store), Pairs(needs));

    /// <summary>Item and quantity pairs as the engine's, or null for none.</summary>
    public static KeyValuePair<ItemId, long>[]? Pairs((string Item, long Quantity)[]? amounts) =>
        amounts?.Select(amount => new KeyValuePair<ItemId, long>(new ItemId(amount.Item), amount.Quantity)).ToArray();
}
