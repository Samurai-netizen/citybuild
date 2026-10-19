using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// When a producer starts a cycle (<c>04</c> sections 2 and 6): only when it is idle with at least 1 worker, every input in stock and room for
/// every capped output, reservations included, and only while production is allowed; in one second, lower ids start first. Fixture numbers:
/// a hut provides 3 workers; a workshop makes 1 boards from 2 wood (90 labor, up to 2 workers, 20 s to build); a lumber camp makes 1 wood
/// (40 labor, up to 2 workers); a mint makes 5 of the uncapped gold (20 labor, up to 2 workers); the caps are wood 100 and boards 80; the
/// production cap is 3600 s.
/// </summary>
public sealed class StartRuleTests
{
    private static readonly ItemId _wood = new("wood");

    [Theory]
    [InlineData(0, 2, 79, false, "NoWorkers")]
    [InlineData(1, 1, 79, false, "MissingInput:wood")]
    [InlineData(1, 2, 80, false, "StorageFull:boards")]
    [InlineData(1, 2, 79, true, "StorageFull:boards")]
    public void StartRule_OneConditionUnmet_StartsNoCycle(int workers, long wood, long boards, bool otherCycleRuns, string stallReason)
    {
        // No worker; one wood short; the boards at the cap (80 + 1 > 80); room taken by the other cycle's board (79 + 1 + 1 > 80).
        CityState city = StartCity(workers, wood, boards, otherCycleRuns);
        string before = CityDump.Describe(city);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal(before, CityDump.Describe(city));
        Assert.Equal(stallReason, StallReason.Of(city, TestContent, city.Buildings[1])?.ToString());
    }

    [Fact]
    public void StartRule_EveryConditionJustMet_StartsTheCycle()
    {
        // 1 worker, exactly the 2 wood, and 79 boards + 0 reserved + 1 = 80, the cap.
        CityState city = StartCity(workers: 1, wood: 2, boards: 79, otherCycleRuns: false);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal("building 2 workshop (1,0) L1 w=1 Producing 1000..1090 target=0 labor=90/90@1000", CityDump.BuildingLine(city, 2));
    }

    [Fact]
    public void ScarceInputs_TwoProducersCanStart_TheLowerIdTakesThem()
    {
        // Wood for one cycle. The city is given workshop 3 before workshop 2; the ids decide, not the order of the list.
        CityState city = City(clock: 1000, inventory: [("wood", 3)], completedResearch: ["carpentry"], buildings:
            [Idle(3, "workshop", 2, 0, workers: 1), Idle(2, "workshop", 1, 0, workers: 1), Idle(1, "hut", 0, 0)]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal(BuildingPhase.Producing, city.Buildings[1].Phase);
        Assert.Equal("MissingInput:wood", StallReason.Of(city, TestContent, city.Buildings[2])?.ToString());
        Assert.Equal(1, city.GetQuantity(_wood));
    }

    [Fact]
    public void Construction_InputsInStock_BlocksProductionUntilItCompletes()
    {
        CityState city = City(clock: 1000, inventory: [("wood", 4)], completedResearch: ["carpentry"], buildings:
            [Idle(1, "hut", 0, 0), Constructing(2, "workshop", 1, 0, startedAt: 990, endsAt: 1010)]);

        Simulator.Advance(city, TestContent, 9);
        Assert.Equal((BuildingPhase.Constructing, 4L), (city.Buildings[1].Phase, city.GetQuantity(_wood)));

        Simulator.Advance(city, TestContent, 1);

        // Done at 1010: it gets min(3 free, 2) workers and starts in that second.
        Assert.Equal("building 2 workshop (1,0) L1 w=2 Producing 1010..1055 target=0 labor=90/90@1010", CityDump.BuildingLine(city, 2));
        Assert.Equal(2, city.GetQuantity(_wood));
    }

    [Fact]
    public void UncappedOutput_StockFarAboveEveryCap_CyclesStillStart()
    {
        // "Coins have no cap": the fixture's coins are gold, an uncapped item. The mint takes 10 s per cycle with 2 workers.
        CityState city = City(clock: 1000, inventory: [("gold", 1_000_000)], buildings: [Idle(1, "hut", 0, 0), Idle(2, "mint", 1, 0, workers: 2)]);

        Simulator.Advance(city, TestContent, 100);

        Assert.Equal(1_000_050, city.GetQuantity(new ItemId("gold")));
        Assert.Equal("building 2 mint (1,0) L1 w=2 Producing 1100..1110 target=0 labor=20/20@1100", CityDump.BuildingLine(city, 2));
    }

    [Fact]
    public void ProductionCap_DormantTail_StartsNoCycleWhileTimersStillComplete()
    {
        // The window is 1000..8200, and cycles may start until 1000 + 3600 = 4600. Camp 2 (1 worker, 40 s cycles) starts its last cycle at
        // 4600 exactly. Camp 3's construction ends one second later, and carpentry in the middle of the tail; both are longer than the
        // fixture's durations, which are all shorter than the cap (the engine reads only the end times).
        CityState city = City(
            clock: 1000,
            buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 1), Constructing(3, "lumber_camp", 2, 0, startedAt: 1000, endsAt: 4601)],
            activeResearch: new ActiveResearch(new ResearchId("carpentry"), 1000, 6000));

        SettlementReport report = Simulator.Advance(city, TestContent, 7200);

        // 91 cycles (1000, 1040, ... 4600), all done by 4640. Camp 3 completed and got min(2 free, 2) workers but started nothing.
        Assert.Equal(91, city.GetQuantity(_wood));
        Assert.Equal("building 3 lumber_camp (2,0) L1 w=2 Idle 0..null target=0 labor=0/0@0", CityDump.BuildingLine(city, 3));
        Assert.Equal([3], report.CompletedBuildings);
        Assert.Equal([new ResearchId("carpentry")], report.CompletedResearch);
    }

    /// <summary>
    /// A hut and building 2, an idle workshop at 1000, with the given workers and stock; when <paramref name="otherCycleRuns"/> is set,
    /// workshop 3 runs a cycle (1 worker) that reserves 1 board.
    /// </summary>
    private static CityState StartCity(int workers, long wood, long boards, bool otherCycleRuns)
    {
        Building hut = Idle(1, "hut", 0, 0);
        Building workshop = Idle(2, "workshop", 1, 0, workers: workers);
        Building[] buildings = otherCycleRuns
            ? [hut, workshop, Producing(3, "workshop", 2, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: 1080)]
            : [hut, workshop];
        return City(clock: 1000, inventory: [("wood", wood), ("boards", boards)], completedResearch: ["carpentry"], buildings: buildings);
    }
}
