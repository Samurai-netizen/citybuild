using CityBuilder.Application.Cities;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// Cities for the persistence tests, built straight from the engine's constructors (no content needed: the mapping doesn't read it), and a
/// text form of a city so two cities compare in one assertion that shows the first difference.
/// </summary>
internal static class SampleCities
{
    /// <summary>The id of every sample city.</summary>
    public static readonly Guid CityId = new("c17e0000-0000-4000-8000-000000000001");

    /// <summary>The owner of every sample city.</summary>
    public static readonly Guid PlayerId = new("91a7e400-0000-4000-8000-000000000001");

    /// <summary>The city clock of <see cref="Full"/>.</summary>
    public const long Clock = 420;

    /// <summary>The id of the constructing building in <see cref="Full"/>.</summary>
    public const int ConstructingId = 1;

    /// <summary>The id of the upgrading building in <see cref="Full"/>.</summary>
    public const int UpgradingId = 2;

    /// <summary>The id of the idle building in <see cref="Full"/>.</summary>
    public const int IdleId = 3;

    /// <summary>The id of the producing building with workers in <see cref="Full"/>.</summary>
    public const int ProducingId = 4;

    /// <summary>The id of the producing building whose cycle is frozen (no workers, no end) in <see cref="Full"/>.</summary>
    public const int FrozenId = 5;

    /// <summary>The id of the producing building whose cycle started at clock 0 in <see cref="Full"/>.</summary>
    public const int StartedAtZeroId = 6;

    /// <summary>The id of the construction site (constructing without a timer, waiting for goods; CR-0008) in <see cref="Full"/>.</summary>
    public const int SiteId = 8;

    /// <summary>The id of the courier station with two couriers and two routes (CR-0008) in <see cref="Full"/>.</summary>
    public const int StationId = 9;

    /// <summary>
    /// A city in era 2 with something in every field: 250 coins in the purse, goods in three stores, a building in each phase (constructing,
    /// upgrading, idle with an upgrade waiting for planks, producing, a frozen cycle with a null end, a cycle started at clock 0, a construction
    /// site waiting for logs), the research <c>masonry</c> completed at clock 120 and <c>forestry</c> running, roads at (2, 1) level 1, (3, 1)
    /// level 2 and (4, 0) level 1 (given out of order), trip 1 carrying 2 logs (the next trip id 3: trip 2 has arrived), a courier station
    /// with courier 1 walking to stop 2 with logs and planks from two buildings and courier 2 resting with logs, and routes for both slots
    /// (every amount form), the next building id above the highest one, state version 7.
    /// </summary>
    public static CityAggregate Full()
    {
        Building[] buildings =
        [
            new(ConstructingId, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Constructing, 400, 700, 0, 0, 0, 0),
            new(UpgradingId, new BuildingTypeId("woodcutter"), 1, 0, 1, 1, BuildingPhase.Upgrading, 200, 500, 2, 0, 0, 0, Goods(("logs", 3))),
            new(IdleId, new BuildingTypeId("sawmill"), 2, 0, 2, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, Goods(("logs", 2)), Goods(("planks", 5))),
            new(ProducingId, new BuildingTypeId("woodcutter"), 3, 0, 1, 2, BuildingPhase.Producing, 300, 440, 0, 120, 80, 400, Goods(("logs", 7))),
            new(FrozenId, new BuildingTypeId("quarry"), 0, 1, 1, 0, BuildingPhase.Producing, 350, null, 0, 60, 60, 350),
            new(StartedAtZeroId, new BuildingTypeId("farm"), 1, 1, 1, 1, BuildingPhase.Producing, 0, 600, 0, 600, 600, 0),
            new(SiteId, new BuildingTypeId("sawmill"), 2, 2, 1, 0, BuildingPhase.Constructing, 0, null, 0, 0, 0, 0, null, Goods(("logs", 10))),
            new(StationId, new BuildingTypeId("courier_station"), 3, 3, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
        ];
        var state = new CityState(
            Clock,
            era: 2,
            nextBuildingId: 10,
            purse: 250,
            buildings,
            [new ResearchId("masonry")],
            new ActiveResearch(new ResearchId("forestry"), 300, 86_700),
            [new RoadTile(4, 0, 1), new RoadTile(2, 1, 1), new RoadTile(3, 1, 2)],
            [new Trip(1, ProducingId, IdleId, new ItemId("logs"), 2, 410, 430)],
            nextTripId: 3,
            couriers:
            [
                new Courier(StationId, 1, CourierState.Walking, 2, 400, 430, 500,
                    [new CourierLoad(new ItemId("logs"), ProducingId, 3), new CourierLoad(new ItemId("planks"), IdleId, 1)]),
                new Courier(StationId, 2, CourierState.Resting, 0, null, null, 0, [new CourierLoad(new ItemId("logs"), UpgradingId, 2)]),
            ],
            routes:
            [
                Route(StationId, 1, (ProducingId, [(true, "logs", "half"), (true, "planks", "all")]), (IdleId, [(false, "logs", "all")])),
                Route(StationId, 2, (IdleId, [(true, "planks", "50%")]), (SiteId, [(false, "planks", "3")])),
            ]);

        return new CityAggregate(CityId, PlayerId, state, [new(new ResearchId("masonry"), 120)], 1_790_000_000, 7, "content-1");
    }

    /// <summary>
    /// <see cref="Full"/> later, at clock 90 000: 240 coins; the producing building finished its cycle and its logs went (its store row stays,
    /// at 0), the idle one is gone and a new one (id 7) is under construction on the tile of the road at (2, 1), which was removed; the site got
    /// 6 of its logs and holds a plank; trip 1 has arrived; the road at (3, 1) is gone, the one at (4, 0) is at level 2, and a new one lies at
    /// (5, 5); <c>forestry</c> completed at its end and <c>carpentry</c> waits for 4 planks; the station has one worker: courier 1 stands
    /// at the station with nothing, courier 2 is gone, slot 1's route has a third stop and slot 2's lost its route. Ids as in <see cref="Full"/>.
    /// </summary>
    /// <param name="stateVersion">The version the city was loaded with; <see cref="Full"/>'s 7 by default.</param>
    public static CityAggregate Later(long stateVersion = 7)
    {
        CityAggregate full = Full();
        Building[] buildings =
        [
            .. full.State.Buildings.Where(building => building.Id is not (IdleId or ProducingId or SiteId or StationId)).Select(CityCopy),
            new(StationId, new BuildingTypeId("courier_station"), 3, 3, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(ProducingId, new BuildingTypeId("woodcutter"), 3, 0, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(7, new BuildingTypeId("cottage"), 2, 1, 1, 0, BuildingPhase.Constructing, 89_000, 89_900, 0, 0, 0, 0),
            new(SiteId, new BuildingTypeId("sawmill"), 2, 2, 1, 0, BuildingPhase.Constructing, 0, null, 0, 0, 0, 0, Goods(("planks", 1)), Goods(("logs", 4))),
        ];
        var state = new CityState(
            90_000,
            full.State.Era,
            full.State.NextBuildingId,
            purse: 240,
            buildings,
            [new ResearchId("masonry"), new ResearchId("forestry")],
            new ActiveResearch(new ResearchId("carpentry"), null, null, Goods(("planks", 4))),
            [new RoadTile(4, 0, 2), new RoadTile(5, 5, 1)],
            nextTripId: 3,
            couriers: [new Courier(StationId, 1, CourierState.AtStation, 0, null, null, 500)],
            routes: [Route(StationId, 1, (ProducingId, [(true, "logs", "half")]), (7, [(false, "logs", "all")]), (SiteId, [(false, "logs", "quarter")]))]);
        KeyValuePair<ResearchId, long>[] completedAt = [new(new ResearchId("masonry"), 120), new(new ResearchId("forestry"), 86_700)];

        return new CityAggregate(full.CityId, full.PlayerId, state, completedAt, full.LastSettledUnixSec, stateVersion, full.ContentVersion);
    }

    /// <summary>
    /// A city whose times, labor and quantities are all beyond 32 bits (above <see cref="int.MaxValue"/>), so only <c>bigint</c> columns keep
    /// them: city clock 5 000 000 420, a constructing building, a producing one with a cycle of 5 000 000 000 worker-seconds, a frozen one,
    /// 9 000 000 000 coins, 2 200 000 000 logs in the producer's store, <c>masonry</c> completed at 4 999 999 000 and <c>forestry</c> running,
    /// last settled at 3 000 000 000 (a server second in 2065). Ids as in <see cref="Full"/>.
    /// </summary>
    public static CityAggregate BeyondThirtyTwoBits()
    {
        const long clock = 5_000_000_420;
        Building[] buildings =
        [
            new(ConstructingId, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Constructing, clock - 20, clock + 3_000_000_000, 0, 0, 0, 0),
            new(ProducingId, new BuildingTypeId("woodcutter"), 3, 0, 1, 2, BuildingPhase.Producing, clock - 100, clock - 20 + 2_000_000_000, 0,
                5_000_000_000, 4_000_000_000, clock - 20, Goods(("logs", 2_200_000_000))),
            new(FrozenId, new BuildingTypeId("quarry"), 0, 1, 1, 0, BuildingPhase.Producing, clock - 50, null, 0, 4_500_000_000, 4_400_000_000,
                clock - 50),
        ];
        var state = new CityState(
            clock,
            era: 2,
            nextBuildingId: 9,
            purse: 9_000_000_000,
            buildings,
            [new ResearchId("masonry")],
            new ActiveResearch(new ResearchId("forestry"), clock - 300, clock + 2_500_000_000));

        return new CityAggregate(
            CityId, PlayerId, state, [new(new ResearchId("masonry"), 4_999_999_000)], 3_000_000_000, 7, "content-1");
    }

    /// <summary>The city as a load returns it after one save: the same values with the next state version.</summary>
    /// <param name="city">The city that was saved.</param>
    public static CityAggregate Saved(CityAggregate city) =>
        new(city.CityId, city.PlayerId, city.State, city.ResearchCompletedAt, city.LastSettledUnixSec, city.StateVersion + 1, city.ContentVersion);

    /// <summary>A new city: clock 0, era 1, no coins, buildings or research, state version 0.</summary>
    public static CityAggregate Empty()
    {
        var state = new CityState(0, 1, 1, 0, [], [], activeResearch: null);
        return new CityAggregate(CityId, PlayerId, state, [], 1_790_000_000, 0, "content-1");
    }

    /// <summary>Every stored value of the city as text, field by field.</summary>
    /// <param name="city">The city.</param>
    /// <returns>One line per part of the city.</returns>
    public static string Describe(CityAggregate city)
    {
        CityState state = city.State;
        IEnumerable<string> lines =
        [
            $"city {city.CityId} player {city.PlayerId} settled {city.LastSettledUnixSec} version {city.StateVersion} content {city.ContentVersion}",
            $"clock {state.Clock}, era {state.Era}, next id {state.NextBuildingId}, purse {state.Purse}, next trip {state.NextTripId}",
            "research done " + string.Join(", ", city.ResearchCompletedAt.Select(done => $"{done.Key}@{done.Value}")),
            "research done (state) " + string.Join(", ", state.CompletedResearch),
            state.ActiveResearch is null
                ? "no active research"
                : $"research {state.ActiveResearch.Id} {state.ActiveResearch.StartedAt}..{state.ActiveResearch.EndsAt} needs {Text(state.ActiveResearch.Needs)}",
            .. state.Buildings.Select(building =>
                $"#{building.Id} {building.TypeId} ({building.X},{building.Y}) L{building.Level} w{building.AssignedWorkers} {building.Phase} " +
                $"{building.PhaseStartedAt}..{building.PhaseEndsAt?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "frozen"} " +
                $"target {building.TargetLevel} labor {building.CycleLabor}/{building.LaborRemaining} segment {building.SegmentStartedAt} " +
                $"store {Text(building.Store)} needs {Text(building.Needs)}"),
            "roads " + string.Join(", ", state.Roads.Select(road => $"({road.X},{road.Y}) L{road.Level}")),
            "trips " + string.Join(", ", state.Trips.Select(trip =>
                $"{trip.Id} {trip.SourceId}->{trip.TargetId} {trip.Item}={trip.Quantity} {trip.DepartedAt}..{trip.ArrivesAt}")),
            .. state.Couriers.Select(courier =>
                $"courier {courier.StationId}/{courier.Slot} {courier.State} to {courier.ToStop} {courier.DepartedAt}..{courier.ArrivesAt} " +
                $"stamina {courier.Stamina} load " + string.Join(" ", courier.Loads.Select(part => $"{part.Item}@{part.FromId}={part.Quantity}"))),
            .. state.Routes.Select(route => $"route {route.StationId}/{route.Slot} " + string.Join(" ", route.Stops.Select(stop =>
                $"{stop.BuildingId}[" + string.Join("; ", stop.Actions.Select(action => $"{action.Kind} {action.Item} {action.Amount}")) + "]"))),
        ];
        return string.Join("\n", lines);
    }

    /// <summary>A route of a station's slot over stops written as (building, actions), each action as (take?, good, amount text).</summary>
    /// <param name="stationId">The courier station's id.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="stops">The stops.</param>
    /// <returns>The route.</returns>
    public static Route Route(int stationId, int slot, params (int BuildingId, (bool Take, string Item, string Amount)[] Actions)[] stops) =>
        new(stationId, slot, stops.Select(stop => new RouteStop(stop.BuildingId, stop.Actions.Select(action => new RouteAction(
            action.Take ? RouteActionKind.Take : RouteActionKind.Put,
            new ItemId(action.Item),
            Amount.TryParse(action.Amount, out Amount? amount) ? amount : throw new ArgumentException("Not an amount: " + action.Amount))))));

    /// <summary>Goods and quantities as the engine's pairs, for a store or a project's needs.</summary>
    /// <param name="goods">The goods.</param>
    /// <returns>The pairs.</returns>
    public static KeyValuePair<ItemId, long>[] Goods(params (string Item, long Quantity)[] goods) =>
        [.. goods.Select(good => new KeyValuePair<ItemId, long>(new ItemId(good.Item), good.Quantity))];

    /// <summary>Quantities as <c>item=quantity</c> joined by spaces, in their (ordinal) order.</summary>
    /// <param name="amounts">The quantities.</param>
    /// <returns>The text; empty for none.</returns>
    private static string Text(IReadOnlyDictionary<ItemId, long> amounts) => string.Join(" ", amounts.Select(amount => $"{amount.Key}={amount.Value}"));

    /// <summary>A new building object with the same values, so two cities never share one (the engine changes buildings in place).</summary>
    /// <param name="building">The building.</param>
    /// <returns>The copy.</returns>
    private static Building CityCopy(Building building) => new(
        building.Id, building.TypeId, building.X, building.Y, building.Level, building.AssignedWorkers, building.Phase, building.PhaseStartedAt,
        building.PhaseEndsAt, building.TargetLevel, building.CycleLabor, building.LaborRemaining, building.SegmentStartedAt, building.Store.ToArray(),
        building.Needs.ToArray());
}
