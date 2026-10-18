using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>CityRules.UpgradeBuilding</c> (<c>04</c> section 8). Fixture numbers (<see cref="CityFixture.TestContent"/>): a hut's level 2 costs 20 gold
/// and 5 wood and takes 8 s; a lumber camp's level 2 (its max) costs 30 gold and takes 12 s; a workshop runs 2 wood → 1 boards, and its level 2
/// costs 30 gold and 1 wood and takes 15 s. The wood cap is 100.
/// </summary>
public sealed class UpgradeBuildingTests
{
    private static readonly ItemId _boards = new("boards");

    [Fact]
    public void UpgradeBuilding_IdleProducer_PaysAndUpgradesKeepingItsWorkers()
    {
        CityState city = City(clock: 1000, inventory: [("gold", 35)], buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2)]);

        RulesAssert.Ok(Upgrade(city, 2));

        Assert.Equal(
            """
            clock=1000 era=1 next=3
            inventory: gold=5
            building 1 hut (0,0) L1 w=0 Idle 0..null target=0 labor=0/0@0
            building 2 lumber_camp (1,0) L1 w=2 Upgrading 1000..1012 target=2 labor=0/0@0
            completed:
            active: none
            """.ReplaceLineEndings("\n"),
            CityDump.Describe(city));
    }

    [Fact]
    public void UpgradeBuilding_IdleHouse_PaysEveryItemOfTheCost()
    {
        CityState city = City(clock: 1000, inventory: [("gold", 20), ("wood", 5)], buildings: [Idle(1, "hut", 0, 0)]);

        RulesAssert.Ok(Upgrade(city, 1));

        Assert.Empty(city.Inventory);
        Assert.Equal((BuildingPhase.Upgrading, 1000L, (long?)1008, 2), (city.Buildings[0].Phase, city.Buildings[0].PhaseStartedAt, city.Buildings[0].PhaseEndsAt, city.Buildings[0].TargetLevel));
    }

    [Fact]
    public void UpgradeBuilding_ProducingBuilding_RefundsTheCycleInputsAndReleasesTheReservation()
    {
        CityState city = City(clock: 1000, inventory: [("gold", 30), ("wood", 1)], buildings: [Idle(1, "hut", 0, 0), WorkshopProducing(endsAt: 1035)]);
        Assert.Equal(1, CityQueries.Reserved(city, TestContent, _boards));

        RulesAssert.Ok(Upgrade(city, 2));

        // Refund 2 wood, then pay 30 gold and 1 wood. The cycle's fields are cleared; the two workers stay.
        Assert.Equal(
            """
            clock=1000 era=1 next=3
            inventory: wood=2
            building 1 hut (0,0) L1 w=0 Idle 0..null target=0 labor=0/0@0
            building 2 workshop (1,0) L1 w=2 Upgrading 1000..1015 target=2 labor=0/0@0
            completed:
            active: none
            """.ReplaceLineEndings("\n"),
            CityDump.Describe(city));
        Assert.Equal(0, CityQueries.Reserved(city, TestContent, _boards));
    }

    [Fact]
    public void UpgradeBuilding_FrozenCycle_RefundsItsInputsToo()
    {
        CityState city = City(inventory: [("gold", 30), ("wood", 1)], buildings: [WorkshopProducing(endsAt: null, workers: 0)]);

        RulesAssert.Ok(Upgrade(city, 2));

        Assert.Equal(2, city.GetQuantity(new ItemId("wood")));
    }

    [Fact]
    public void UpgradeBuilding_RefundAboveTheCap_KeepsAllOfIt()
    {
        CityState city = City(inventory: [("gold", 30), ("wood", 100)], buildings: [WorkshopProducing(endsAt: 1035)]);

        RulesAssert.Ok(Upgrade(city, 2));

        // 100 (the cap) + 2 refunded − 1 paid: caps limit production only.
        Assert.Equal(101, city.GetQuantity(new ItemId("wood")));
    }

    [Fact]
    public void UpgradeBuilding_CostCoveredOnlyWithTheRefund_IsRejected()
    {
        // The refund of 2 wood would pay the 1 wood, but the cost is checked against the stock before the refund.
        CityState city = City(inventory: [("gold", 30)], buildings: [WorkshopProducing(endsAt: 1035)]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, city, city => Upgrade(city, 2));
    }

    [Fact]
    public void UpgradeBuilding_CostNotAffordable_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, City(inventory: [("gold", 20), ("wood", 4)], buildings: [Idle(1, "hut", 0, 0)]),
            city => Upgrade(city, 1));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void UpgradeBuilding_UnknownId_IsRejected(int id) =>
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, City(inventory: [("gold", 100), ("wood", 100)], buildings: [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0)]),
            city => Upgrade(city, id));

    [Fact]
    public void UpgradeBuilding_Constructing_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, City(inventory: [("gold", 100), ("wood", 100)], buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003)]),
            city => Upgrade(city, 1));

    [Fact]
    public void UpgradeBuilding_AlreadyUpgrading_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, City(inventory: [("gold", 100), ("wood", 100)], buildings: [Upgrading(1, "lumber_camp", 0, 0, level: 1, workers: 1, startedAt: 995, endsAt: 1007)]),
            city => Upgrade(city, 1));

    [Fact]
    public void UpgradeBuilding_AtMaxLevel_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.MaxLevel, City(inventory: [("gold", 100), ("wood", 100)], buildings: [Idle(1, "lumber_camp", 0, 0, level: 2)]),
            city => Upgrade(city, 1));

    [Fact]
    public void UpgradeBuilding_EveryBuilderBusy_IsRejected()
    {
        CityState city = City(inventory: [("gold", 100), ("wood", 100)], buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003),
            Upgrading(2, "hut", 1, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003),
            Idle(3, "hut", 2, 0),
        ]);

        RulesAssert.Rejected(MutationErrorCode.NoFreeBuilder, city, city => Upgrade(city, 3));
    }

    [Fact]
    public void UpgradeBuilding_SeveralChecksFail_ReportsTheFirstInSpecOrder()
    {
        CityState city = City(buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003),
            Upgrading(2, "hut", 1, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003),
            Idle(3, "lumber_camp", 2, 0, level: 2),
            Idle(4, "hut", 3, 0),
        ]);

        // Busy beats max level; max level beats no free builder; no free builder beats the cost.
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, city, city => Upgrade(city, 2));
        RulesAssert.Rejected(MutationErrorCode.MaxLevel, city, city => Upgrade(city, 3));
        RulesAssert.Rejected(MutationErrorCode.NoFreeBuilder, city, city => Upgrade(city, 4));
    }

    [Fact]
    public void UpgradeBuilding_NullCityOrContent_Throws()
    {
        // The nulls are deliberate: they are programmer errors, not business rules.
        Assert.Throws<ArgumentNullException>(() => CityRules.UpgradeBuilding(null!, TestContent, 1));
        Assert.Throws<ArgumentNullException>(() => CityRules.UpgradeBuilding(City(), null!, 1));
    }

    /// <summary>Building 2, a workshop at (1,0) in a cycle that started at 990 (90 labor, all of it left).</summary>
    private static Building WorkshopProducing(long? endsAt, int workers = 2) =>
        Producing(2, "workshop", 1, 0, 1, workers, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: endsAt);

    private static MutationResult Upgrade(CityState city, int buildingId) => CityRules.UpgradeBuilding(city, TestContent, buildingId);
}
