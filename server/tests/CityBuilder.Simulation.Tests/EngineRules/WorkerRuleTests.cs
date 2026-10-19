using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// Workers and labor (<c>04</c> section 3): a cycle keeps its progress when its workers change, 0 workers freeze it, only a completed
/// construction staffs a building automatically, and a house provides workers once it is built. Fixture numbers: a hut provides 3 workers;
/// a lumber camp makes 1 wood from 40 labor with up to 2 workers (4 at level 2) and costs 20 gold; a workshop's level 2 makes 1 boards from
/// 2 wood with 180 labor and up to 6 workers; the wood cap is 100.
/// </summary>
public sealed class WorkerRuleTests
{
    private static readonly ItemId _wood = new("wood");

    [Fact]
    public void Reassigning_MidCycle_KeepsTheProgress()
    {
        // The spec's example: 180 labor with 3 workers; after 20 s, 120 labor is left, and 4 workers do it in 30 s.
        CityState city = City(clock: 1000, inventory: [("wood", 2)], completedResearch: ["carpentry"], buildings:
            [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0), Idle(3, "workshop", 2, 0, level: 2, workers: 3)]);
        Simulator.Advance(city, TestContent, 20);

        Commands.AssignWorkers(city, 3, 4);

        Assert.Equal("building 3 workshop (2,0) L2 w=4 Producing 1000..1050 target=0 labor=180/120@1020", CityDump.BuildingLine(city, 3));
        Simulator.Advance(city, TestContent, 29);
        Assert.Equal(0, city.GetQuantity(new ItemId("boards")));
        Simulator.Advance(city, TestContent, 1);
        Assert.Equal(1, city.GetQuantity(new ItemId("boards")));
    }

    [Fact]
    public void ZeroWorkers_MidCycle_FreezeItAndKeepItsReservedOutput()
    {
        // 99 wood: camp 2 starts (99 + 0 + 1 = 100). At 1010 its workers move to camp 3, which may not start: 99 + 1 reserved + 1 > 100.
        CityState city = City(clock: 1000, inventory: [("wood", 99)], buildings:
            [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2), Idle(3, "lumber_camp", 2, 0)]);
        Simulator.Advance(city, TestContent, 10);

        Commands.AssignWorkers(city, 2, 0);
        Commands.AssignWorkers(city, 3, 2);
        Simulator.Advance(city, TestContent, 10_000);

        // No end and no progress: 20 of its 40 labor are still to do, and its reserved wood still holds camp 3 back.
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=0 Producing 1000..null target=0 labor=40/20@1010", CityDump.BuildingLine(city, 2));
        Assert.Equal(1, CityQueries.Reserved(city, TestContent, _wood));
        Assert.Equal((99L, "StorageFull:wood"), (city.GetQuantity(_wood), StallReason.Of(city, TestContent, city.Buildings[2])?.ToString()));
    }

    [Fact]
    public void ReaddingWorkers_FrozenCycle_ResumesWhereItStopped()
    {
        // 10 s with 2 workers do 20 of the 40 labor; the cycle then stands frozen for 500 s.
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2)]);
        Simulator.Advance(city, TestContent, 10);
        Commands.AssignWorkers(city, 2, 0);
        Simulator.Advance(city, TestContent, 500);

        Commands.AssignWorkers(city, 2, 1);

        // The 20 labor that were left are still left: 1 worker does them in 20 s.
        Assert.Equal("building 2 lumber_camp (1,0) L1 w=1 Producing 1000..1530 target=0 labor=40/20@1510", CityDump.BuildingLine(city, 2));
        Simulator.Advance(city, TestContent, 20);
        Assert.Equal(1, city.GetQuantity(_wood));
    }

    [Fact]
    public void AutoStaffing_SeveralCompletions_OnlyConstructionsGetMinOfFreeAndMax()
    {
        // Camp 2 has no workers while hut 3 is built and camp 4 (1 worker) is upgraded to level 2 (up to 4); both are done at 1005.
        CityState city = City(clock: 1000, inventory: [("gold", 40)], buildings:
        [
            Idle(1, "hut", 0, 0),
            Idle(2, "lumber_camp", 1, 0),
            Constructing(3, "hut", 2, 0, startedAt: 1000, endsAt: 1005),
            Upgrading(4, "lumber_camp", 3, 0, level: 1, workers: 1, startedAt: 993, endsAt: 1005),
        ]);
        Simulator.Advance(city, TestContent, 5);

        // The hut's 3 new workers and the higher limit staffed nobody: 5 of the 6 workers are free.
        Assert.Equal((0, 1, 5L), (city.Buildings[1].AssignedWorkers, city.Buildings[3].AssignedWorkers, CityQueries.FreeWorkers(city, TestContent)));

        // The player puts 2 more on camp 4 and places camps 5 and 6; both are done at 1015, with 3 workers free.
        Commands.AssignWorkers(city, 4, 3);
        Commands.PlaceBuilding(city, "lumber_camp", 4, 0);
        Commands.PlaceBuilding(city, "lumber_camp", 5, 0);
        Simulator.Advance(city, TestContent, 10);

        // In id order: camp 5 gets min(3, 2) = 2, camp 6 gets min(1, 2) = 1. Camp 2 still has none.
        Assert.Equal((0, 2, 1), (city.Buildings[1].AssignedWorkers, city.Buildings[4].AssignedWorkers, city.Buildings[5].AssignedWorkers));
    }

    [Fact]
    public void HouseCompleting_ConstructionEnds_ProvidesWorkersFromThatSecond()
    {
        // The only hut is done at 1005; until then no worker can be assigned.
        CityState city = City(clock: 1000, buildings: [Constructing(1, "hut", 0, 0, startedAt: 1000, endsAt: 1005), Idle(2, "lumber_camp", 1, 0)]);
        Simulator.Advance(city, TestContent, 4);
        RulesAssert.Rejected(MutationErrorCode.InsufficientWorkers, city, target => CityRules.AssignWorkers(target, TestContent, 2, 1));

        Simulator.Advance(city, TestContent, 1);

        Assert.Equal(3, CityQueries.FreeWorkers(city, TestContent));
        Commands.AssignWorkers(city, 2, 2);
        Assert.Equal(BuildingPhase.Producing, city.Buildings[1].Phase);
    }
}
