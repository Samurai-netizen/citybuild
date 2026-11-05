using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// Upgrading a producer (<c>04</c> sections 3 and 8): the running cycle is refunded, nothing is produced while the upgrade runs, the workers
/// stay assigned, and the next level's worker limit counts from the upgrade's end; a refund goes into the building's own store and may exceed
/// its limit. Fixture numbers: a hut provides 3 workers; a workshop makes 1 boards from 2 wood, with 90 labor, up to 2 workers and a store of
/// 20 wood at level 1 and 180 labor, up to 6 workers and 40 wood at level 2, whose upgrade costs 30 gold (coins only, so it starts at once)
/// and takes 15 s.
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

        // The cycle's 2 wood came back into its store (2 + 2), its board is no longer reserved, and both workers stay.
        Assert.Equal("building 3 workshop (2,0) L1 w=2 Upgrading 1020..1035 target=2 labor=0/0@0", CityDump.BuildingLine(city, 3));
        Assert.Equal((4L, 0L), (city.Buildings[2].GetStock(_wood), CityQueries.Reserved(TestContent, city.Buildings[2], new ItemId("boards"))));
    }

    /// <summary>During the upgrade nothing is produced and the old level's worker limit holds.</summary>
    [Fact]
    public void Upgrade_WhileItRuns_BlocksProductionAndKeepsTheOldWorkerLimit()
    {
        CityState city = WorkshopUpgradedMidCycle();

        // Level 1's limit of 2 holds until the end, and no cycle starts although the wood and the workers are there.
        RulesAssert.Rejected(MutationErrorCode.WorkerLimit, city, target => CityRules.AssignWorkers(target, TestContent, 3, 3));
        SettlementReport report = Simulator.Advance(city, TestContent, 14);

        Assert.Equal((BuildingPhase.Upgrading, 4L), (city.Buildings[2].Phase, city.Buildings[2].GetStock(_wood)));
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

    /// <summary>
    /// A refund may push the building's store above its limit; it is kept in full (limits stop production only), and the upgraded building
    /// uses it. (Under the city-wide stock the refund also blocked another producer of the same item; with stores a refund goes back into the
    /// building's own store only, CR-0008.)
    /// </summary>
    [Fact]
    public void Refund_AboveTheLimit_IsKeptAndUsedAfterTheUpgrade()
    {
        // The workshop's store is at its limit of 20 wood; the upgrade refunds the running cycle's 2 wood: 22, above the limit.
        CityState city = City(clock: 1000, purse: 30, completedResearch: ["carpentry"], buildings:
        [
            Idle(1, "hut", 0, 0),
            Producing(2, "workshop", 1, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: 1035,
                store: [("wood", 20)]),
        ]);

        Commands.UpgradeBuilding(city, 2);

        Assert.Equal((22L, 0L), (city.Buildings[1].GetStock(_wood), CityQueries.Room(city, TestContent, city.Buildings[1], _wood)));

        // At 1015 the workshop reaches level 2 (40 wood) and its 2 workers start a cycle with 2 of the 22 wood in that second.
        Simulator.Advance(city, TestContent, 15);
        Assert.Equal("building 2 workshop (1,0) L2 w=2 Producing 1015..1105 target=0 labor=180/180@1015", CityDump.BuildingLine(city, 2));
        Assert.Equal(20, city.Buildings[1].GetStock(_wood));
    }

    /// <summary>
    /// Two huts (6 workers) and a level-1 workshop with 2 workers that starts a 45 s cycle at 1000 with 4 wood in its store; at 1020 the player
    /// upgrades it (30 gold, until 1035).
    /// </summary>
    private static CityState WorkshopUpgradedMidCycle()
    {
        CityState city = City(clock: 1000, purse: 30, completedResearch: ["carpentry"],
            buildings: [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0), Idle(3, "workshop", 2, 0, workers: 2, store: [("wood", 4)])]);
        Simulator.Advance(city, TestContent, 20);
        Commands.UpgradeBuilding(city, 3);
        return city;
    }
}
