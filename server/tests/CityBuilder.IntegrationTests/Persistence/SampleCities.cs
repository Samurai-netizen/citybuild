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

    /// <summary>
    /// A city in era 2 with something in every field: stock of two items, a building in each phase (constructing, upgrading, idle, producing,
    /// a frozen cycle with a null end, a cycle started at clock 0), the research <c>masonry</c> completed at clock 120 and <c>forestry</c>
    /// running, the next building id above the highest one, state version 7.
    /// </summary>
    public static CityAggregate Full()
    {
        Building[] buildings =
        [
            new(ConstructingId, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Constructing, 400, 700, 0, 0, 0, 0),
            new(UpgradingId, new BuildingTypeId("woodcutter"), 1, 0, 1, 1, BuildingPhase.Upgrading, 200, 500, 2, 0, 0, 0),
            new(IdleId, new BuildingTypeId("sawmill"), 2, 0, 2, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(ProducingId, new BuildingTypeId("woodcutter"), 3, 0, 1, 2, BuildingPhase.Producing, 300, 440, 0, 120, 80, 400),
            new(FrozenId, new BuildingTypeId("quarry"), 0, 1, 1, 0, BuildingPhase.Producing, 350, null, 0, 60, 60, 350),
            new(StartedAtZeroId, new BuildingTypeId("farm"), 1, 1, 1, 1, BuildingPhase.Producing, 0, 600, 0, 600, 600, 0),
        ];
        var state = new CityState(
            Clock,
            era: 2,
            nextBuildingId: 9,
            [new(new ItemId("coins"), 250), new(new ItemId("logs"), 7)],
            buildings,
            [new ResearchId("masonry")],
            new ActiveResearch(new ResearchId("forestry"), 300, 86_700));

        return new CityAggregate(CityId, PlayerId, state, [new(new ResearchId("masonry"), 120)], 1_790_000_000, 7, "content-1");
    }

    /// <summary>
    /// <see cref="Full"/> later, at clock 90 000: coins spent, logs used up, planks new; the producing building finished its cycle, the idle one
    /// is gone and a new one (id 7) is under construction; <c>forestry</c> completed at its end. Ids as in <see cref="Full"/>.
    /// </summary>
    /// <param name="stateVersion">The version the city was loaded with; <see cref="Full"/>'s 7 by default.</param>
    public static CityAggregate Later(long stateVersion = 7)
    {
        CityAggregate full = Full();
        Building[] buildings =
        [
            .. full.State.Buildings.Where(building => building.Id is not (IdleId or ProducingId)),
            new(ProducingId, new BuildingTypeId("woodcutter"), 3, 0, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(7, new BuildingTypeId("cottage"), 2, 1, 1, 0, BuildingPhase.Constructing, 89_000, 89_900, 0, 0, 0, 0),
        ];
        var state = new CityState(
            90_000,
            full.State.Era,
            full.State.NextBuildingId,
            [new(new ItemId("coins"), 240), new(new ItemId("planks"), 3)],
            buildings,
            [new ResearchId("masonry"), new ResearchId("forestry")],
            activeResearch: null);
        KeyValuePair<ResearchId, long>[] completedAt = [new(new ResearchId("masonry"), 120), new(new ResearchId("forestry"), 86_700)];

        return new CityAggregate(full.CityId, full.PlayerId, state, completedAt, full.LastSettledUnixSec, stateVersion, full.ContentVersion);
    }

    /// <summary>
    /// A city whose times, labor and quantities are all beyond 32 bits (above <see cref="int.MaxValue"/>), so only <c>bigint</c> columns keep
    /// them: city clock 5 000 000 420, a constructing building, a producing one with a cycle of 5 000 000 000 worker-seconds, a frozen one,
    /// 9 000 000 000 coins, <c>masonry</c> completed at 4 999 999 000 and <c>forestry</c> running, last settled at 3 000 000 000 (a server
    /// second in 2065). Ids as in <see cref="Full"/>.
    /// </summary>
    public static CityAggregate BeyondThirtyTwoBits()
    {
        const long clock = 5_000_000_420;
        Building[] buildings =
        [
            new(ConstructingId, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Constructing, clock - 20, clock + 3_000_000_000, 0, 0, 0, 0),
            new(ProducingId, new BuildingTypeId("woodcutter"), 3, 0, 1, 2, BuildingPhase.Producing, clock - 100, clock - 20 + 2_000_000_000, 0,
                5_000_000_000, 4_000_000_000, clock - 20),
            new(FrozenId, new BuildingTypeId("quarry"), 0, 1, 1, 0, BuildingPhase.Producing, clock - 50, null, 0, 4_500_000_000, 4_400_000_000,
                clock - 50),
        ];
        var state = new CityState(
            clock,
            era: 2,
            nextBuildingId: 9,
            [new(new ItemId("coins"), 9_000_000_000), new(new ItemId("logs"), 2_200_000_000)],
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

    /// <summary>A new city: clock 0, era 1, no stock, buildings or research, state version 0.</summary>
    public static CityAggregate Empty()
    {
        var state = new CityState(0, 1, 1, [], [], [], activeResearch: null);
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
            $"clock {state.Clock}, era {state.Era}, next id {state.NextBuildingId}",
            "stock " + string.Join(", ", state.Inventory.Select(stock => $"{stock.Key}={stock.Value}")),
            "research done " + string.Join(", ", city.ResearchCompletedAt.Select(done => $"{done.Key}@{done.Value}")),
            "research done (state) " + string.Join(", ", state.CompletedResearch),
            state.ActiveResearch is null
                ? "no active research"
                : $"research {state.ActiveResearch.Id} {state.ActiveResearch.StartedAt}..{state.ActiveResearch.EndsAt}",
            .. state.Buildings.Select(building =>
                $"#{building.Id} {building.TypeId} ({building.X},{building.Y}) L{building.Level} w{building.AssignedWorkers} {building.Phase} " +
                $"{building.PhaseStartedAt}..{building.PhaseEndsAt?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "frozen"} " +
                $"target {building.TargetLevel} labor {building.CycleLabor}/{building.LaborRemaining} segment {building.SegmentStartedAt}"),
        ];
        return string.Join("\n", lines);
    }
}
