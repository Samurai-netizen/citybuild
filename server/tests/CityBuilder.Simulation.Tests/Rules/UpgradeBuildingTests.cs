using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>CityRules.UpgradeBuilding</c> (<c>04</c> sections 8 and 16; CR-0008 D1, D12). Fixture numbers (<see cref="CityFixture.TestContent"/>): a
/// hut's level 2 costs 20 gold and 5 wood and takes 8 s; a lumber camp's level 2 (its max) costs 30 gold and 2 boards and takes 12 s; a
/// workshop runs 2 wood → 1 boards, its store holds 20 wood, and its level 2 costs 30 gold and takes 15 s. Only coins are paid at once: an
/// upgrade without goods in its cost starts at once, one with goods waits for them while the building keeps working.
/// </summary>
public sealed class UpgradeBuildingTests
{
    /// <summary>The workshop's output, reserved by a running cycle.</summary>
    private static readonly ItemId _boards = new("boards");

    /// <summary>The workshop's input.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>Upgrading an idle producer whose next level costs coins only pays them and starts the upgrade from the clock, keeping its workers.</summary>
    [Fact]
    public void UpgradeBuilding_IdleProducerCoinsOnly_PaysAndUpgradesKeepingItsWorkers()
    {
        CityState city = City(clock: 1000, purse: 35, buildings: [Idle(1, "hut", 0, 0), Idle(2, "workshop", 1, 0, workers: 2)], completedResearch: ["carpentry"]);

        RulesAssert.Ok(Upgrade(city, 2));

        Assert.Equal(
            """
            clock=1000 era=1 next=3 purse=5
            building 1 hut (0,0) L1 w=0 Idle 0..null target=0 labor=0/0@0 store: needs:
            building 2 workshop (1,0) L1 w=2 Upgrading 1000..1015 target=2 labor=0/0@0 store: needs:
            completed: carpentry
            active: none
            """.ReplaceLineEndings("\n"),
            CityDump.Describe(city));
    }

    /// <summary>
    /// Upgrading a house whose next level costs gold and wood pays the gold and waits for the wood: the house stays idle at level 1 with the
    /// wood as its needs, its builder slot taken, and no store is touched (CR-0008 D12).
    /// </summary>
    [Fact]
    public void UpgradeBuilding_CostWithGoods_PaysTheCoinsAndWaitsForTheGoods()
    {
        CityState city = City(clock: 1000, purse: 20, buildings: [Idle(1, "hut", 0, 0), Idle(2, "depot", 1, 0, store: [("wood", 50)])]);

        RulesAssert.Moved(Upgrade(city, 1), spent: "gold=20", refunded: "");

        Building hut = city.Buildings[0];
        Assert.Equal((BuildingPhase.Idle, 1, true), (hut.Phase, hut.Level, hut.HasUpgradeWaiting));
        Assert.Equal([("wood", 5L)], hut.Needs.Select(need => (need.Key.Value, need.Value)));
        Assert.Equal((0L, 50L, 1), (city.Purse, city.Buildings[1].GetStock(_wood), CityQueries.BuildersBusy(city)));
    }

    /// <summary>A producer whose upgrade waits for goods keeps its running cycle: the upgrade starts only when the goods are in (CR-0008 D12).</summary>
    [Fact]
    public void UpgradeBuilding_ProducingWithAGoodsUpgrade_KeepsProducing()
    {
        CityState city = City(purse: 30, buildings:
            [Producing(1, "lumber_camp", 0, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010)]);

        RulesAssert.Moved(Upgrade(city, 1), spent: "gold=30", refunded: "");

        Building camp = city.Buildings[0];
        Assert.Equal((BuildingPhase.Producing, (long?)1010, 2L), (camp.Phase, camp.PhaseEndsAt, camp.GetNeed(_boards)));
    }

    /// <summary>A second order while an upgrade waits for goods is <see cref="MutationErrorCode.BuildingBusy"/>.</summary>
    [Fact]
    public void UpgradeBuilding_UpgradeAlreadyWaiting_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, City(purse: 100, buildings: [Idle(1, "hut", 0, 0, needs: [("wood", 5)])]), city => Upgrade(city, 1));

    /// <summary>
    /// Upgrading mid-cycle at once refunds the cycle's inputs into the building's own store, clears the cycle and its reservation, and keeps
    /// the workers.
    /// </summary>
    [Fact]
    public void UpgradeBuilding_ProducingBuilding_RefundsTheCycleInputsIntoItsStore()
    {
        CityState city = City(clock: 1000, purse: 30, buildings: [Idle(1, "hut", 0, 0), WorkshopProducing(endsAt: 1035)]);
        Assert.Equal(1, CityQueries.Reserved(TestContent, city.Buildings[1], _boards));

        RulesAssert.Ok(Upgrade(city, 2));

        // Refund 2 wood into the workshop's store, then pay 30 gold. The cycle's fields are cleared; the two workers stay.
        Assert.Equal(
            """
            clock=1000 era=1 next=3 purse=0
            building 1 hut (0,0) L1 w=0 Idle 0..null target=0 labor=0/0@0 store: needs:
            building 2 workshop (1,0) L1 w=2 Upgrading 1000..1015 target=2 labor=0/0@0 store: wood=2 needs:
            completed:
            active: none
            """.ReplaceLineEndings("\n"),
            CityDump.Describe(city));
        Assert.Equal(0, CityQueries.Reserved(TestContent, city.Buildings[1], _boards));
    }

    /// <summary>A frozen cycle is refunded like a running one, into the building's store.</summary>
    [Fact]
    public void UpgradeBuilding_FrozenCycle_RefundsItsInputsToo()
    {
        CityState city = City(purse: 30, buildings: [WorkshopProducing(endsAt: null, workers: 0, store: [("wood", 1)])]);

        RulesAssert.Ok(Upgrade(city, 2));

        Assert.Equal(3, city.Buildings[0].GetStock(_wood));
    }

    /// <summary>Upgrading over a running or frozen cycle names its inputs as refunded, besides the coins as spent.</summary>
    /// <param name="endsAt">The cycle's end, null for a frozen one.</param>
    /// <param name="workers">The workshop's workers.</param>
    [Theory]
    [InlineData(1035L, 2)]
    [InlineData(null, 0)]
    public void UpgradeBuilding_RunningOrFrozenCycle_NamesTheCycleInputsAsRefunded(long? endsAt, int workers)
    {
        CityState city = City(purse: 30, buildings: [Idle(1, "hut", 0, 0), WorkshopProducing(endsAt, workers)]);

        RulesAssert.Moved(Upgrade(city, 2), spent: "gold=30", refunded: "wood=2");
    }

    /// <summary>A refund that lifts the store above its limit is kept in full: limits stop production only (<c>04</c> section 16).</summary>
    [Fact]
    public void UpgradeBuilding_RefundAboveTheLimit_KeepsAllOfIt()
    {
        CityState city = City(purse: 30, buildings: [WorkshopProducing(endsAt: 1035, store: [("wood", 20)])]);

        RulesAssert.Ok(Upgrade(city, 2));

        // 20 (the limit) + 2 refunded.
        Assert.Equal(22, city.Buildings[0].GetStock(_wood));
    }

    /// <summary>Coins the purse can't cover are rejected as <see cref="MutationErrorCode.InsufficientResource"/>; goods in stores don't help.</summary>
    [Fact]
    public void UpgradeBuilding_CoinsNotAffordable_IsRejected() =>
        RulesAssert.Rejected(
            MutationErrorCode.InsufficientResource,
            City(purse: 19, buildings: [Idle(1, "hut", 0, 0), Idle(2, "depot", 1, 0, store: [("wood", 100)])]),
            city => Upgrade(city, 1));

    /// <summary>An id with no building is rejected as <see cref="MutationErrorCode.BuildingNotFound"/>.</summary>
    /// <param name="id">The id asked for.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void UpgradeBuilding_UnknownId_IsRejected(int id) =>
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, City(purse: 100, buildings: [Idle(1, "hut", 0, 0), Idle(2, "hut", 1, 0)]),
            city => Upgrade(city, id));

    /// <summary>A building under construction, a construction site included, can't be upgraded: <see cref="MutationErrorCode.BuildingBusy"/>.</summary>
    [Fact]
    public void UpgradeBuilding_Constructing_IsRejected()
    {
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, City(purse: 100, buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003)]),
            city => Upgrade(city, 1));
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, City(purse: 100, buildings: [Site(1, "workshop", 0, 0, needs: [("wood", 10)])]),
            city => Upgrade(city, 1));
    }

    /// <summary>A building already upgrading can't start another upgrade: <see cref="MutationErrorCode.BuildingBusy"/>.</summary>
    [Fact]
    public void UpgradeBuilding_AlreadyUpgrading_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, City(purse: 100, buildings: [Upgrading(1, "lumber_camp", 0, 0, level: 1, workers: 1, startedAt: 995, endsAt: 1007)]),
            city => Upgrade(city, 1));

    /// <summary>A building at its max level is rejected as <see cref="MutationErrorCode.MaxLevel"/>.</summary>
    [Fact]
    public void UpgradeBuilding_AtMaxLevel_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.MaxLevel, City(purse: 100, buildings: [Idle(1, "lumber_camp", 0, 0, level: 2)]),
            city => Upgrade(city, 1));

    /// <summary>With every builder busy (a waiting upgrade takes one too) an upgrade is rejected as <see cref="MutationErrorCode.NoFreeBuilder"/>.</summary>
    [Fact]
    public void UpgradeBuilding_EveryBuilderBusy_IsRejected()
    {
        CityState city = City(purse: 100, buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003),
            Idle(2, "hut", 1, 0, needs: [("wood", 5)]),
            Idle(3, "hut", 2, 0),
        ]);

        RulesAssert.Rejected(MutationErrorCode.NoFreeBuilder, city, city => Upgrade(city, 3));
    }

    /// <summary>When several checks fail, the first in spec order wins: busy before max level, max level before no builder, no builder before the cost.</summary>
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

    /// <summary>A null city or content is a programmer error and throws.</summary>
    [Fact]
    public void UpgradeBuilding_NullCityOrContent_Throws()
    {
        // The nulls are deliberate: they are programmer errors, not business rules.
        Assert.Throws<ArgumentNullException>(() => CityRules.UpgradeBuilding(null!, TestContent, 1));
        Assert.Throws<ArgumentNullException>(() => CityRules.UpgradeBuilding(City(), null!, 1));
    }

    /// <summary>Building 2, a workshop at (1,0) in a cycle that started at 990 (90 labor, all of it left), with its store.</summary>
    private static Building WorkshopProducing(long? endsAt, int workers = 2, (string Item, long Quantity)[]? store = null) =>
        Producing(2, "workshop", 1, 0, 1, workers, cycleStartedAt: 990, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 990, endsAt: endsAt, store: store);

    /// <summary>Tries to upgrade a building, with the fixture content.</summary>
    private static MutationResult Upgrade(CityState city, int buildingId) => CityRules.UpgradeBuilding(city, TestContent, buildingId);
}
