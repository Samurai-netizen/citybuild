using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// When a producer starts a cycle (<c>04</c> sections 2, 6 and 16): only when it is idle with at least 1 worker, every input in its own store
/// and room in its own store for every output that is not the currency, and only while production is allowed. Fixture numbers: a hut
/// provides 3 workers; a workshop makes 1 boards from 2 wood (90 labor, up to 2 workers, 20 s to build) and its store holds 20 wood and 80
/// boards; a lumber camp makes 1 wood (40 labor, up to 2 workers) and holds 100 wood; a mint makes 5 gold, the currency (20 labor, up to 2
/// workers), into the purse; the production cap is 3600 s.
/// </summary>
public sealed class StartRuleTests
{
    /// <summary>The workshop's input and the lumber camp's output.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>With any one start condition unmet (no worker, an input short in its store, the output at its limit), nothing changes and the stall reason names it.</summary>
    /// <param name="workers">The workshop's workers.</param>
    /// <param name="wood">The wood in its store.</param>
    /// <param name="boards">The boards in its store.</param>
    /// <param name="stallReason">The expected stall reason.</param>
    [Theory]
    [InlineData(0, 2, 79, "NoWorkers")]
    [InlineData(1, 1, 79, "MissingInput:wood")]
    [InlineData(1, 2, 80, "StorageFull:boards")]
    public void StartRule_OneConditionUnmet_StartsNoCycle(int workers, long wood, long boards, string stallReason)
    {
        // No worker; one wood short; the boards at the limit (80 + 1 > 80).
        CityState city = StartCity(workers, wood, boards);
        string before = CityDump.Describe(city);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal(before, CityDump.Describe(city));
        Assert.Equal(stallReason, StallReason.Of(city, TestContent, city.Buildings[1])?.ToString());
    }

    /// <summary>Goods in another building's store don't feed a producer: a workshop with its wood in the depot is <c>MissingInput:wood</c> (CR-0008).</summary>
    [Fact]
    public void StartRule_InputsInAnotherStore_StartsNoCycle()
    {
        CityState city = City(clock: 1000, completedResearch: ["carpentry"], buildings:
            [Idle(1, "hut", 0, 0), Idle(2, "workshop", 1, 0, workers: 1), Idle(3, "depot", 2, 0, store: [("wood", 50)])]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal("MissingInput:wood", StallReason.Of(city, TestContent, city.Buildings[1])?.ToString());
        Assert.Equal(50, city.Buildings[2].GetStock(_wood));
    }

    /// <summary>With every condition met exactly (1 worker, exactly the inputs, the output reaching exactly the limit) the cycle starts and takes the inputs from its store.</summary>
    [Fact]
    public void StartRule_EveryConditionJustMet_StartsTheCycle()
    {
        // 1 worker, exactly the 2 wood, and 79 boards + 1 = 80, the limit.
        CityState city = StartCity(workers: 1, wood: 2, boards: 79);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal("building 2 workshop (1,0) L1 w=1 Producing 1000..1090 target=0 labor=90/90@1000", CityDump.BuildingLine(city, 2));
        Assert.Equal("store: boards=79 needs:", CityDump.StoreLine(city, 2));
    }

    /// <summary>Each producer starts on its own store, whatever order the city lists the buildings in: one with its inputs starts, one without doesn't.</summary>
    [Fact]
    public void StartRule_EachProducer_StartsOnItsOwnStore()
    {
        // The city is given workshop 3 before workshop 2; each has its own store, so the order changes nothing.
        CityState city = City(clock: 1000, completedResearch: ["carpentry"], buildings:
            [Idle(3, "workshop", 2, 0, workers: 1, store: [("wood", 1)]), Idle(2, "workshop", 1, 0, workers: 1, store: [("wood", 3)]), Idle(1, "hut", 0, 0)]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal(BuildingPhase.Producing, city.Buildings[1].Phase);
        Assert.Equal("MissingInput:wood", StallReason.Of(city, TestContent, city.Buildings[2])?.ToString());
        Assert.Equal((1L, 1L), (city.Buildings[1].GetStock(_wood), city.Buildings[2].GetStock(_wood)));
    }

    /// <summary>
    /// A start takes inputs from its own store only, so it frees no room in another: the lumber camp at its limit stays full while the
    /// workshop starts on its own wood. (Under the city-wide stock, CR-0001 let the camp start in the same second; with stores that case is gone,
    /// and one pass starts everything, BL-20261102-07 decision 34.)
    /// </summary>
    [Fact]
    public void StartRule_StartInOneStore_FreesNoRoomInAnother()
    {
        CityState city = City(clock: 1000, completedResearch: ["carpentry"], buildings:
        [
            Idle(1, "hut", 0, 0),
            Idle(2, "lumber_camp", 1, 0, workers: 2, store: [("wood", 100)]),
            Idle(3, "workshop", 2, 0, workers: 1, store: [("wood", 2)]),
        ]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal("StorageFull:wood", StallReason.Of(city, TestContent, city.Buildings[1])?.ToString());
        Assert.Equal("building 3 workshop (2,0) L1 w=1 Producing 1000..1090 target=0 labor=90/90@1000", CityDump.BuildingLine(city, 3));
        Assert.Equal((100L, 0L), (city.Buildings[1].GetStock(_wood), city.Buildings[2].GetStock(_wood)));
    }

    /// <summary>A building under construction produces nothing; its store waits for it, and when it completes it is staffed and starts in that second.</summary>
    [Fact]
    public void Construction_InputsInItsStore_BlocksProductionUntilItCompletes()
    {
        CityState city = City(clock: 1000, completedResearch: ["carpentry"], buildings:
            [Idle(1, "hut", 0, 0), new Building(2, new BuildingTypeId("workshop"), 1, 0, 1, 0, BuildingPhase.Constructing, 990, 1010, 0, 0, 0, 0, Pairs([("wood", 4)]))]);

        Simulator.Advance(city, TestContent, 9);
        Assert.Equal((BuildingPhase.Constructing, 4L), (city.Buildings[1].Phase, city.Buildings[1].GetStock(_wood)));

        Simulator.Advance(city, TestContent, 1);

        // Done at 1010: it gets min(3 free, 2) workers and starts in that second.
        Assert.Equal("building 2 workshop (1,0) L1 w=2 Producing 1010..1055 target=0 labor=90/90@1010", CityDump.BuildingLine(city, 2));
        Assert.Equal(2, city.Buildings[1].GetStock(_wood));
    }

    /// <summary>The currency never blocks a start: a mint's gold goes into the purse, which has no limit, however full it is.</summary>
    [Fact]
    public void CurrencyOutput_PurseFarAboveEveryLimit_CyclesStillStart()
    {
        // The mint takes 10 s per cycle with 2 workers, and its coins go into the purse (04 section 16).
        CityState city = City(clock: 1000, purse: 1_000_000, buildings: [Idle(1, "hut", 0, 0), Idle(2, "mint", 1, 0, workers: 2)]);

        Simulator.Advance(city, TestContent, 100);

        Assert.Equal(1_000_050, city.Purse);
        Assert.Equal("building 2 mint (1,0) L1 w=2 Producing 1100..1110 target=0 labor=20/20@1100", CityDump.BuildingLine(city, 2));
    }

    /// <summary>After the production cap no cycle starts, but construction and research timers still complete in the dormant tail.</summary>
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

        // 91 cycles (1000, 1040, ... 4600), all done by 4640, in camp 2's store (limit 100). Camp 3 completed and got min(2 free, 2) workers
        // but started nothing.
        Assert.Equal(91, city.Buildings[1].GetStock(_wood));
        Assert.Equal("building 3 lumber_camp (2,0) L1 w=2 Idle 0..null target=0 labor=0/0@0", CityDump.BuildingLine(city, 3));
        Assert.Equal([3], report.CompletedBuildings);
        Assert.Equal([new ResearchId("carpentry")], report.CompletedResearch);
    }

    /// <summary>A hut and building 2, an idle workshop at 1000, with the given workers and its store's wood and boards.</summary>
    private static CityState StartCity(int workers, long wood, long boards) =>
        City(clock: 1000, completedResearch: ["carpentry"], buildings:
            [Idle(1, "hut", 0, 0), Idle(2, "workshop", 1, 0, workers: workers, store: [("wood", wood), ("boards", boards)])]);
}
