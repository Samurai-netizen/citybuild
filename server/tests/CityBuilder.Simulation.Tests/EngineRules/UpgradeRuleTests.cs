using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// Upgrading a producer (<c>04</c> sections 3 and 8): the running cycle is refunded, nothing is produced while the upgrade runs, the workers
/// stay assigned, and the next level's worker limit counts from the upgrade's end; a refund may exceed a cap, which then blocks new cycles.
/// Fixture numbers: a hut provides 3 workers; a workshop makes 1 boards from 2 wood, with 90 labor and up to 2 workers at level 1 and 180
/// labor and up to 6 workers at level 2, whose upgrade costs 30 gold and 1 wood and takes 15 s; a lumber camp makes 1 wood from 40 labor
/// with up to 2 workers; the wood cap is 100.
/// </summary>
public sealed class UpgradeRuleTests
{
    /// <summary>The workshop's input, refunded by the upgrade.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>Upgrading mid-cycle refunds the cycle's inputs, drops its reservation, and keeps both workers assigned.</summary>
    [Fact]
    public void Upgrade_ProducerMidCycle_RefundsTheCycleAndKeepsTheWorkers()
    {
        CityState city = WorkshopUpgradedMidCycle();

        // The cycle's 2 wood came back before the upgrade took 1 (2 + 2 − 1), its board is no longer reserved, and both workers stay.
        Assert.Equal("building 3 workshop (2,0) L1 w=2 Upgrading 1020..1035 target=2 labor=0/0@0", CityDump.BuildingLine(city, 3));
        Assert.Equal((3L, 0L), (city.GetQuantity(_wood), CityQueries.Reserved(city, TestContent, new ItemId("boards"))));
    }

    /// <summary>During the upgrade nothing is produced and the old level's worker limit holds.</summary>
    [Fact]
    public void Upgrade_WhileItRuns_BlocksProductionAndKeepsTheOldWorkerLimit()
    {
        CityState city = WorkshopUpgradedMidCycle();

        // Level 1's limit of 2 holds until the end, and no cycle starts although the wood and the workers are there.
        RulesAssert.Rejected(MutationErrorCode.WorkerLimit, city, target => CityRules.AssignWorkers(target, TestContent, 3, 3));
        SettlementReport report = Simulator.Advance(city, TestContent, 14);

        Assert.Equal((BuildingPhase.Upgrading, 3L), (city.Buildings[2].Phase, city.GetQuantity(_wood)));
        Assert.Empty(report.Consumed);
    }

    /// <summary>When the upgrade completes, the kept workers start the new level's cycle in that second, and the new limit applies.</summary>
    [Fact]
    public void Upgrade_Completes_RaisesMaxWorkersAndTheKeptWorkersStartAtOnce()
    {
        CityState city = WorkshopUpgradedMidCycle();

        Simulator.Advance(city, TestContent, 15);

        // Level 2 at 1035: the 2 kept workers start its 180-labor cycle in that second (90 s), and up to 6 workers may now be assigned.
        Assert.Equal("building 3 workshop (2,0) L2 w=2 Producing 1035..1125 target=0 labor=180/180@1035", CityDump.BuildingLine(city, 3));
        Commands.AssignWorkers(city, 3, 6);
        Assert.Equal(6, city.Buildings[2].AssignedWorkers);
    }

    /// <summary>A refund may push the stock above the cap; it is kept, and it blocks new cycles until the stock is back under the cap.</summary>
    [Fact]
    public void Refund_AboveTheCap_IsKeptButBlocksNewCycles()
    {
        // Wood is at its cap of 100 and camp 4 waits. The upgrade refunds the workshop's 2 wood and takes 1: 101, above the cap.
        CityState city = City(clock: 1000, inventory: [("gold", 30), ("wood", 100)], completedResearch: ["carpentry"], buildings:
        [
            Idle(1, "hut", 0, 0),
            Idle(2, "hut", 1, 0),
            Producing(3, "workshop", 2, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: 1035),
            Idle(4, "lumber_camp", 3, 0, workers: 2),
        ]);

        Commands.UpgradeBuilding(city, 3);

        Assert.Equal((101L, "StorageFull:wood"), (city.GetQuantity(_wood), StallReason.Of(city, TestContent, city.Buildings[3])?.ToString()));

        // At 1015 the upgraded workshop (the lower id) takes 2 wood; then 99 + 1 fits, and camp 4 starts in the same second.
        Simulator.Advance(city, TestContent, 15);
        Assert.Equal(99, city.GetQuantity(_wood));
        Assert.Equal("building 4 lumber_camp (3,0) L1 w=2 Producing 1015..1035 target=0 labor=40/40@1015", CityDump.BuildingLine(city, 4));
    }

    /// <summary>
    /// Two huts (6 workers) and a level-1 workshop with 2 workers that starts a 45 s cycle at 1000 with 4 wood in stock; at 1020 the player
    /// upgrades it (30 gold and 1 wood, until 1035).
    /// </summary>
    private static CityState WorkshopUpgradedMidCycle()
    {
        CityState city = City(clock: 1000, inventory: [("gold", 30), ("wood", 4)], completedResearch: ["carpentry"],
            buildings: [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0), Idle(3, "workshop", 2, 0, workers: 2)]);
        Simulator.Advance(city, TestContent, 20);
        Commands.UpgradeBuilding(city, 3);
        return city;
    }
}
