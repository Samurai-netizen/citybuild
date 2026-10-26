using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Settlement;

/// <summary>
/// Production during <c>Simulator.Advance</c> (<c>04</c> sections 2, 3, 5 and 6): the start rule, cycles, chains, research effects and the
/// production cap. Fixture numbers: a hut provides 3 workers; a lumber camp makes 1 wood from 40 labor (2 workers: 20 s), 30 labor once
/// <c>better_saws</c> is done; a workshop makes 1 boards from 2 wood and 90 labor; the wood cap is 100; the production cap is 3600 s.
/// </summary>
public sealed class AdvanceProductionTests
{
    /// <summary>The lumber camp's output and the workshop's input.</summary>
    private static readonly ItemId _wood = new("wood");
    /// <summary>The workshop's output.</summary>
    private static readonly ItemId _boards = new("boards");

    /// <summary>A raw producer makes one output per finished cycle: five cycles ended by 1100, the sixth still runs.</summary>
    [Fact]
    public void Advance_RawProducerOverSeveralCycles_MakesOneOutputPerCycle()
    {
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 100);

        // Cycles start at 1000, 1020, ... 1100; the five that ended by 1100 delivered their wood, the sixth runs.
        Assert.Equal(5, city.GetQuantity(_wood));
        Assert.Equal(new Dictionary<ItemId, long> { [_wood] = 5 }, report.Produced);
        Assert.Empty(report.Consumed);
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1100..1120 target=0 labor=40/40@1100", CityDump.BuildingLine(city, 2));
    }

    /// <summary>In a chain the second producer starts as soon as the first has made enough of its input, all within one advance.</summary>
    [Fact]
    public void Advance_TwoStepChain_TheSecondStepRunsOnTheFirstStepsOutput()
    {
        CityState city = City(clock: 1000, buildings:
        [
            Idle(1, "hut", 0, 0),
            Idle(2, "hut", 1, 0),
            Idle(3, "lumber_camp", 2, 0, workers: 2),
            Idle(4, "workshop", 3, 0, workers: 2),
        ]);

        SettlementReport report = Simulator.Advance(city, TestContent, 100);

        // Wood arrives at 1020, 1040, 1060, 1080 and 1100. The workshop starts when it has 2 wood: at 1040 (ends 1085) and again at 1085.
        Assert.Equal((1L, 1L), (city.GetQuantity(_wood), city.GetQuantity(_boards)));
        Assert.Equal(new Dictionary<ItemId, long> { [_boards] = 1, [_wood] = 5 }, report.Produced);
        Assert.Equal(new Dictionary<ItemId, long> { [_wood] = 4 }, report.Consumed);
        Assert.Equal(["boards", "wood"], report.Produced.Keys.Select(item => item.Value));
        Assert.Equal("building 3 lumber_camp (2,0) L1 w=2 Producing 1100..1120 target=0 labor=40/40@1100", CityDump.BuildingLine(city, 3));
        Assert.Equal("building 4 workshop (3,0) L1 w=2 Producing 1085..1130 target=0 labor=90/90@1085", CityDump.BuildingLine(city, 4));
    }

    /// <summary>A labor reduction completing mid-cycle leaves the running cycle at 40 labor; the next one starts with 30.</summary>
    [Fact]
    public void Advance_ResearchCompletesMidCycle_SpeedsUpOnlyTheNextCycle()
    {
        CityState city = SawmillResearchCity(researchEndsAt: 1030);

        // The cycle that runs when better_saws completes at 1030 keeps its 40 labor.
        Simulator.Advance(city, TestContent, 35);
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1020..1040 target=0 labor=40/40@1020", CityDump.BuildingLine(city, 2));

        // The next one starts with 30 labor: 15 s with 2 workers.
        Simulator.Advance(city, TestContent, 10);
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1040..1055 target=0 labor=30/30@1040", CityDump.BuildingLine(city, 2));
    }

    /// <summary>When research and a cycle end share a second, the research completes first, so the next cycle already has the new labor.</summary>
    [Fact]
    public void Advance_ResearchAndCycleEndInTheSameSecond_TheNextCycleUsesTheNewLabor()
    {
        CityState city = SawmillResearchCity(researchEndsAt: 1020);

        SettlementReport report = Simulator.Advance(city, TestContent, 20);

        Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1020..1035 target=0 labor=30/30@1020", CityDump.BuildingLine(city, 2));
        Assert.Equal([new ResearchId("better_saws")], report.CompletedResearch);
    }

    /// <summary>A cycle ending exactly at the target completes in this advance, and the next cycle starts at the target.</summary>
    [Fact]
    public void Advance_CycleEndsExactlyAtTheTarget_CompletesItAndStartsTheNext()
    {
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2)]);

        Simulator.Advance(city, TestContent, 20);

        Assert.Equal(1, city.GetQuantity(_wood));
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1020..1040 target=0 labor=40/40@1020", CityDump.BuildingLine(city, 2));
    }

    /// <summary>A zero-length advance moves no time but starts the cycles that can start at the clock.</summary>
    [Fact]
    public void Advance_ZeroLength_StartsTheCyclesThatCanStartAtTheClock()
    {
        CityState city = City(clock: 1000, inventory: [("wood", 2)], buildings: [Idle(1, "hut", 0, 0), Idle(2, "workshop", 1, 0, workers: 1)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 0);

        Assert.Equal("building 2 workshop (1,0) L1 w=1 Producing 1000..1090 target=0 labor=90/90@1000", CityDump.BuildingLine(city, 2));
        Assert.Equal(0, city.GetQuantity(_wood));
        Assert.Equal(new Dictionary<ItemId, long> { [_wood] = 2 }, report.Consumed);
        Assert.Equal(1000, city.Clock);
    }

    /// <summary>A completed construction is staffed and starts its first cycle in the same second.</summary>
    [Fact]
    public void Advance_ConstructionCompletes_TheStaffedProducerStartsInTheSameSecond()
    {
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Constructing(2, "lumber_camp", 1, 0, startedAt: 995, endsAt: 1005)]);

        Simulator.Advance(city, TestContent, 10);

        Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1005..1025 target=0 labor=40/40@1005", CityDump.BuildingLine(city, 2));
    }

    /// <summary>A cycle's duration rounds up: 40 labor with 3 workers takes 14 s.</summary>
    [Fact]
    public void Advance_LaborNotDivisibleByWorkers_RoundsTheDurationUp()
    {
        // ceil(40 / 3) = 14 s on a level-2 camp.
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0), Idle(3, "lumber_camp", 2, 0, level: 2, workers: 3)]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal("building 3 lumber_camp (2,0) L2 w=3 Producing 1000..1014 target=0 labor=40/40@1000", CityDump.BuildingLine(city, 3));
    }

    /// <summary>A producer without workers never starts a cycle.</summary>
    [Fact]
    public void Advance_ProducerWithoutWorkers_StartsNoCycle()
    {
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 100);

        Assert.Equal(BuildingPhase.Idle, city.Buildings[1].Phase);
        Assert.Empty(report.Produced);
    }

    /// <summary>A start counts what running cycles already reserve: with 99 wood one camp starts, and the second waits (99 + 1 + 1 > 100).</summary>
    [Fact]
    public void Advance_OutputFitsTheStockButNotTheReservation_TheSecondProducerWaits()
    {
        // 99 wood: camp 3 starts (99 + 0 + 1 ≤ 100), then camp 4 would make 99 + 1 + 1 > 100.
        CityState city = City(clock: 1000, inventory: [("wood", 99)], buildings:
        [
            Idle(1, "hut", 0, 0),
            Idle(2, "hut", 1, 0),
            Idle(3, "lumber_camp", 2, 0, workers: 2),
            Idle(4, "lumber_camp", 3, 0, workers: 2),
        ]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal(BuildingPhase.Producing, city.Buildings[2].Phase);
        Assert.Equal(BuildingPhase.Idle, city.Buildings[3].Phase);
        Assert.Equal("StorageFull:wood", StallReason.Of(city, TestContent, city.Buildings[3])?.ToString());
    }

    /// <summary>No cycle starts after the production cap; the one started at the cap's last second still finishes in the dormant tail.</summary>
    [Fact]
    public void Advance_BeyondTheProductionCap_StartsNoCycleButFinishesTheRunningOne()
    {
        // One worker: 40 s cycles. They may start until 1000 + 3600 = 4600, the last one at 4600 (the 91st); it ends at 4640, in the dormant
        // tail. Without the cap the camp would run on to the wood cap of 100.
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 1)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 7200);

        Assert.Equal(91, city.GetQuantity(_wood));
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=1 Idle 0..null target=0 labor=0/0@0", CityDump.BuildingLine(city, 2));
        Assert.Equal((8200L, 3600L), (city.Clock, report.ProductionSeconds));
    }

    /// <summary>The next request opens a new window, so a zero-length advance after the tail starts production again.</summary>
    [Fact]
    public void Advance_AfterADormantTail_AZeroLengthAdvanceRestartsProduction()
    {
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 1)]);
        Simulator.Advance(city, TestContent, 7200);

        // The next request opens a new window at 8200.
        Simulator.Advance(city, TestContent, 0);

        Assert.Equal("building 2 lumber_camp (1,0) L1 w=1 Producing 8200..8240 target=0 labor=40/40@8200", CityDump.BuildingLine(city, 2));
    }

    /// <summary>A hut and an idle lumber camp with 2 workers at 1000; carpentry is done and better_saws (−250 ‰ camp labor) runs.</summary>
    private static CityState SawmillResearchCity(long researchEndsAt) => City(
        clock: 1000,
        buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2)],
        completedResearch: ["carpentry"],
        activeResearch: new ActiveResearch(new ResearchId("better_saws"), 900, researchEndsAt));
}
