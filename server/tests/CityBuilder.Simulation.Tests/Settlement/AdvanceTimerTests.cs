using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Settlement;

/// <summary>
/// Timer completions during <c>Simulator.Advance</c> (<c>04</c> sections 3, 5 and 6): research, construction with auto-staffing, upgrades
/// and cycle ends. Fixture numbers: a hut provides 3 workers at level 1 and 5 at level 2; a lumber camp takes up to 2 workers at level 1 and
/// 4 at level 2 and makes 1 wood per cycle; <c>carpentry</c> unlocks the workshop; the production cap is 3600 s.
/// </summary>
public sealed class AdvanceTimerTests
{
    [Fact]
    public void Advance_ResearchEndsMidAdvance_CompletesItAndFreesTheSlot()
    {
        CityState city = City(clock: 1000, activeResearch: new ActiveResearch(new ResearchId("carpentry"), 950, 1050));

        SettlementReport report = Simulator.Advance(city, TestContent, 100);

        Assert.Equal([new ResearchId("carpentry")], city.CompletedResearch);
        Assert.Null(city.ActiveResearch);
        Assert.Equal([new ResearchId("carpentry")], report.CompletedResearch);
    }

    [Fact]
    public void Advance_ResearchCompletes_ItsEffectsApplyFromThatMoment()
    {
        CityState city = City(clock: 1000, activeResearch: new ActiveResearch(new ResearchId("carpentry"), 950, 1050));
        Assert.False(CityQueries.IsUnlocked(city, TestContent, new BuildingTypeId("workshop")));

        Simulator.Advance(city, TestContent, 50);

        Assert.True(CityQueries.IsUnlocked(city, TestContent, new BuildingTypeId("workshop")));
    }

    [Fact]
    public void Advance_TimersAfterTheProductionCap_StillCompleteOnTime()
    {
        // The window is 1000..8200 and production may start only until 4600; timers run to the end (the dormant tail).
        CityState city = City(
            clock: 1000,
            buildings: [Constructing(1, "hut", 0, 0, startedAt: 995, endsAt: 6000), Upgrading(2, "hut", 1, 0, level: 1, workers: 0, startedAt: 990, endsAt: 8200)],
            activeResearch: new ActiveResearch(new ResearchId("carpentry"), 900, 5100));

        SettlementReport report = Simulator.Advance(city, TestContent, 7200);

        Assert.Equal([1, 2], report.CompletedBuildings);
        Assert.Equal([new ResearchId("carpentry")], report.CompletedResearch);
        Assert.Equal(2, city.Buildings[1].Level);
    }

    public static TheoryData<int, int, int> AutoStaffing => new()
    {
        // The other camp's level and workers (one hut provides 3), and the workers the new camp (max 2) gets.
        { 1, 0, 2 },
        { 1, 2, 1 },
        { 2, 3, 0 },
    };

    [Theory]
    [MemberData(nameof(AutoStaffing))]
    public void Advance_ConstructionCompletesMidAdvance_AutoStaffsMinOfFreeAndMaxWorkers(int otherLevel, int otherWorkers, int expected)
    {
        CityState city = City(clock: 1000, buildings:
        [
            Idle(1, "hut", 0, 0),
            Idle(2, "lumber_camp", 1, 0, level: otherLevel, workers: otherWorkers),
            Constructing(3, "lumber_camp", 2, 0, startedAt: 995, endsAt: 1005),
        ]);

        SettlementReport report = Simulator.Advance(city, TestContent, 10);

        Assert.Equal(expected, city.Buildings[2].AssignedWorkers);
        Assert.Equal([3], report.CompletedBuildings);
    }

    [Fact]
    public void Advance_HouseConstructionCompletes_ProvidesWorkersAndTakesNone()
    {
        // The first hut's 3 workers are free when the second one completes; a house's level takes none.
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Constructing(2, "hut", 1, 0, startedAt: 995, endsAt: 1005)]);

        Simulator.Advance(city, TestContent, 10);

        Assert.Equal("building 2 hut (1,0) L1 w=0 Idle 0..null target=0 labor=0/0@0", CityDump.BuildingLine(city, 2));
        Assert.Equal(6, CityQueries.FreeWorkers(city, TestContent));
    }

    public static TheoryData<int, int, int> SameSecondCompletions => new()
    {
        // The hut's id, the camp's id, and the camp's workers: only a hut with a lower id completes first and provides its workers.
        { 1, 2, 2 },
        { 2, 1, 0 },
    };

    [Theory]
    [MemberData(nameof(SameSecondCompletions))]
    public void Advance_HouseAndProducerCompleteInTheSameSecond_LowerIdCompletesFirst(int hutId, int campId, int campWorkers)
    {
        CityState city = City(clock: 1000, buildings:
        [
            Constructing(hutId, "hut", 0, 0, startedAt: 995, endsAt: 1005),
            Constructing(campId, "lumber_camp", 1, 0, startedAt: 995, endsAt: 1005),
        ]);

        Simulator.Advance(city, TestContent, 10);

        Assert.True(city.TryGetBuilding(campId, out Building? camp));
        Assert.Equal(campWorkers, camp.AssignedWorkers);
    }

    [Fact]
    public void Advance_UpgradeCompletes_RaisesTheLevelAndKeepsTheWorkers()
    {
        // Level 2 takes up to 4 workers, but only construction staffs a building automatically.
        CityState city = City(clock: 1000, buildings:
        [
            Idle(1, "hut", 0, 0),
            Idle(2, "hut", 1, 0),
            Upgrading(3, "lumber_camp", 2, 0, level: 1, workers: 2, startedAt: 990, endsAt: 1002),
        ]);

        SettlementReport report = Simulator.Advance(city, TestContent, 5);

        Building camp = city.Buildings[2];
        Assert.Equal((2, 2, 0), (camp.Level, camp.AssignedWorkers, camp.TargetLevel));
        Assert.Equal([3], report.CompletedBuildings);
    }

    [Fact]
    public void Advance_HouseUpgradeCompletes_ProvidesTheNewLevelsWorkers()
    {
        CityState city = City(clock: 1000, buildings: [Upgrading(1, "hut", 0, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003)]);

        Simulator.Advance(city, TestContent, 5);

        Assert.Equal("building 1 hut (0,0) L2 w=0 Idle 0..null target=0 labor=0/0@0", CityDump.BuildingLine(city, 1));
        Assert.Equal(5, CityQueries.WorkersProvided(city, TestContent));
    }

    [Fact]
    public void Advance_CompletionsInAnotherOrderThanTheIds_ReportListsThemByAscendingId()
    {
        CityState city = City(clock: 1000, buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 995, endsAt: 1008),
            Upgrading(2, "hut", 1, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003),
        ]);

        SettlementReport report = Simulator.Advance(city, TestContent, 10);

        Assert.Equal([1, 2], report.CompletedBuildings);
    }

    [Fact]
    public void Advance_CycleEndsMidAdvance_MovesTheOutputsToStockAndReportsThem()
    {
        CityState city = City(clock: 1000, inventory: [("wood", 5)], buildings:
        [
            Idle(1, "hut", 0, 0),
            Producing(2, "lumber_camp", 1, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010),
        ]);

        SettlementReport report = Simulator.Advance(city, TestContent, 15);

        Assert.Equal(6, city.GetQuantity(new ItemId("wood")));
        Assert.Equal(new Dictionary<ItemId, long> { [new ItemId("wood")] = 1 }, report.Produced);
        Assert.Empty(report.CompletedBuildings);
    }

    [Fact]
    public void Advance_FrozenCycle_NeverCompletes()
    {
        CityState city = City(clock: 1000, inventory: [("wood", 5)], buildings:
        [
            Idle(1, "hut", 0, 0),
            Producing(2, "lumber_camp", 1, 0, 1, workers: 0, cycleStartedAt: 980, cycleLabor: 40, laborRemaining: 20, segmentStartedAt: 990, endsAt: null),
        ]);

        SettlementReport report = Simulator.Advance(city, TestContent, 10_000);

        Assert.Equal("building 2 lumber_camp (1,0) L1 w=0 Producing 980..null target=0 labor=40/20@990", CityDump.BuildingLine(city, 2));
        Assert.Equal(5, city.GetQuantity(new ItemId("wood")));
        Assert.Empty(report.Produced);
    }
}
