using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>CityRules.AssignWorkers</c> (<c>04</c> sections 3 and 8). Fixture numbers (<see cref="CityFixture.TestContent"/>): a hut provides 3
/// workers at level 1; a lumber camp takes up to 2 workers at level 1 and 4 at level 2.
/// </summary>
public sealed class AssignWorkersTests
{
    [Fact]
    public void AssignWorkers_MoreWorkersMidCycle_KeepsProgressAndEndsSooner()
    {
        // The spec's example: a 180-labor cycle ran 20 s with 3 workers, so 120 labor is left; with 4 workers it ends 30 s later.
        CityState city = TwoHutsAnd(LumberCampProducing(workers: 3, laborRemaining: 180, segmentStartedAt: 1000, endsAt: 1060), clock: 1020);

        RulesAssert.Ok(Assign(city, 3, 4));

        Assert.Equal("building 3 lumber_camp (2,0) L2 w=4 Producing 1000..1050 target=0 labor=180/120@1020", CityDump.BuildingLine(city, 3));
    }

    [Fact]
    public void AssignWorkers_ZeroWorkersMidCycle_FreezesTheCycleAndKeepsItsReservation()
    {
        CityState city = TwoHutsAnd(LumberCampProducing(workers: 3, laborRemaining: 180, segmentStartedAt: 1000, endsAt: 1060), clock: 1020);

        RulesAssert.Ok(Assign(city, 3, 0));

        Assert.Equal("building 3 lumber_camp (2,0) L2 w=0 Producing 1000..null target=0 labor=180/120@1020", CityDump.BuildingLine(city, 3));
        Assert.Equal(1, CityQueries.Reserved(city, TestContent, new ItemId("wood")));
    }

    [Fact]
    public void AssignWorkers_WorkersOnAFrozenCycle_ResumeWhereItStopped()
    {
        // Frozen since 1010 with 120 labor left: no progress while frozen, then 3 workers need ceil(120 / 3) = 40 s.
        CityState city = TwoHutsAnd(LumberCampProducing(workers: 0, laborRemaining: 120, segmentStartedAt: 1010, endsAt: null), clock: 1020);

        RulesAssert.Ok(Assign(city, 3, 3));

        Assert.Equal("building 3 lumber_camp (2,0) L2 w=3 Producing 1000..1060 target=0 labor=180/120@1020", CityDump.BuildingLine(city, 3));
    }

    [Fact]
    public void AssignWorkers_LaborNotDivisible_RoundsTheDurationUp()
    {
        CityState city = TwoHutsAnd(LumberCampProducing(workers: 0, laborRemaining: 100, segmentStartedAt: 1010, endsAt: null), clock: 1020);

        RulesAssert.Ok(Assign(city, 3, 3));

        // ceil(100 / 3) = 34.
        Assert.Equal((long?)1054, city.Buildings[2].PhaseEndsAt);
    }

    [Fact]
    public void AssignWorkers_SameCountMidCycle_KeepsTheEnd()
    {
        // 100 labor with 3 workers from 1000 ends at 1034; at 1001, 97 labor is left, and ceil(97 / 3) = 33 more seconds is again 1034.
        CityState city = TwoHutsAnd(LumberCampProducing(workers: 3, laborRemaining: 100, segmentStartedAt: 1000, endsAt: 1034), clock: 1001);

        RulesAssert.Ok(Assign(city, 3, 3));

        Assert.Equal("building 3 lumber_camp (2,0) L2 w=3 Producing 1000..1034 target=0 labor=180/97@1001", CityDump.BuildingLine(city, 3));
    }

    [Fact]
    public void AssignWorkers_IdleBuilding_SetsOnlyTheCount()
    {
        CityState city = TwoHutsAnd(Idle(3, "lumber_camp", 2, 0, level: 2), clock: 1020);

        RulesAssert.Ok(Assign(city, 3, 4));

        Assert.Equal("building 3 lumber_camp (2,0) L2 w=4 Idle 0..null target=0 labor=0/0@0", CityDump.BuildingLine(city, 3));
        Assert.Equal(2, CityQueries.FreeWorkers(city, TestContent));
    }

    [Fact]
    public void AssignWorkers_UpgradingBuilding_SetsOnlyTheCount()
    {
        CityState city = TwoHutsAnd(Upgrading(3, "lumber_camp", 2, 0, level: 1, workers: 1, startedAt: 1010, endsAt: 1022), clock: 1020);

        RulesAssert.Ok(Assign(city, 3, 2));

        Assert.Equal("building 3 lumber_camp (2,0) L1 w=2 Upgrading 1010..1022 target=2 labor=0/0@0", CityDump.BuildingLine(city, 3));
    }

    [Fact]
    public void AssignWorkers_FewerWorkersWithNoneFree_IsAllowed()
    {
        // One hut (3 workers), all of them on the camp: lowering the count needs no free worker.
        CityState city = City(buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, level: 2, workers: 3)]);

        RulesAssert.Ok(Assign(city, 2, 1));

        Assert.Equal(2, CityQueries.FreeWorkers(city, TestContent));
    }

    [Fact]
    public void AssignWorkers_AllFreeWorkers_IsAllowed()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, level: 2, workers: 1)]);

        RulesAssert.Ok(Assign(city, 2, 3));

        Assert.Equal(0, CityQueries.FreeWorkers(city, TestContent));
    }

    [Fact]
    public void AssignWorkers_UnknownId_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, TwoHutsAnd(Idle(3, "lumber_camp", 2, 0), clock: 1000), city => Assign(city, 4, 1));

    [Fact]
    public void AssignWorkers_House_IsRejectedAsInvalidArgument() =>
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, TwoHutsAnd(Idle(3, "lumber_camp", 2, 0), clock: 1000), city => Assign(city, 1, 1));

    [Fact]
    public void AssignWorkers_ConstructingHouse_IsRejectedAsInvalidArgument() =>
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, TwoHutsAnd(Constructing(3, "hut", 2, 0, startedAt: 998, endsAt: 1003), clock: 1000),
            city => Assign(city, 3, 0));

    [Fact]
    public void AssignWorkers_ConstructingProducer_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, TwoHutsAnd(Constructing(3, "lumber_camp", 2, 0, startedAt: 995, endsAt: 1005), clock: 1000),
            city => Assign(city, 3, 1));

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void AssignWorkers_CountOutsideTheLevelsLimit_IsRejected(int workers) =>
        RulesAssert.Rejected(MutationErrorCode.WorkerLimit, TwoHutsAnd(Idle(3, "lumber_camp", 2, 0), clock: 1000), city => Assign(city, 3, workers));

    [Fact]
    public void AssignWorkers_MoreThanTheFreeWorkers_IsRejected()
    {
        // One hut (3 workers), 2 on another camp: 1 free, and going from 0 to 2 needs 2.
        CityState city = City(buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2), Idle(3, "lumber_camp", 2, 0)]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientWorkers, city, city => Assign(city, 3, 2));
    }

    [Fact]
    public void AssignWorkers_OnlyAHouseUnderConstruction_HasNoWorkersToAssign()
    {
        CityState city = City(buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003), Idle(2, "lumber_camp", 1, 0)]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientWorkers, city, city => Assign(city, 2, 1));
    }

    [Fact]
    public void AssignWorkers_SeveralChecksFail_ReportsTheFirstInSpecOrder()
    {
        CityState city = City(buildings: [Constructing(1, "lumber_camp", 0, 0, startedAt: 995, endsAt: 1005), Idle(2, "lumber_camp", 1, 0)]);

        // Constructing beats the limit and the free workers; the limit beats the free workers (none are free here).
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, city, city => Assign(city, 1, 5));
        RulesAssert.Rejected(MutationErrorCode.WorkerLimit, city, city => Assign(city, 2, 5));
    }

    [Fact]
    public void AssignWorkers_NullCityOrContent_Throws()
    {
        // The nulls are deliberate: they are programmer errors, not business rules.
        Assert.Throws<ArgumentNullException>(() => CityRules.AssignWorkers(null!, TestContent, 1, 1));
        Assert.Throws<ArgumentNullException>(() => CityRules.AssignWorkers(City(), null!, 1, 1));
    }

    /// <summary>Building 3, a level-2 lumber camp at (2,0) in a 180-labor cycle that started at 1000.</summary>
    private static Building LumberCampProducing(int workers, long laborRemaining, long segmentStartedAt, long? endsAt) =>
        Producing(3, "lumber_camp", 2, 0, 2, workers, cycleStartedAt: 1000, cycleLabor: 180, laborRemaining, segmentStartedAt, endsAt);

    /// <summary>Two completed huts (6 workers) and the given building 3.</summary>
    private static CityState TwoHutsAnd(Building third, long clock) =>
        City(clock: clock, buildings: [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0), third]);

    private static MutationResult Assign(CityState city, int buildingId, int workers) => CityRules.AssignWorkers(city, TestContent, buildingId, workers);
}
