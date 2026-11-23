using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using CityBuilder.Simulation.Tests.Rules;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// The builder queue (CR-0013, <c>04</c> sections 4, 6 and 8): with every builder busy, a placement or upgrade pays its coins and waits in a
/// queue of <c>builderQueueLength</c> places; it starts at the exact second a builder frees up, in the order the orders were made. A queued
/// construction is placed (its tile, <c>maxPerCity</c>) but does nothing; a queued upgrade leaves its building working. Fixture numbers
/// (<see cref="State.CityFixture.TestContent"/> with a queue of 2): two builder slots; a hut costs 10 gold and builds in 5 s, a lumber camp 20
/// gold and 10 s (its level 2: 30 gold and 2 boards), a depot 15 gold (its level 2: 25 gold, 9 s), the workshop's level 2 30 gold and 15 s.
/// </summary>
public sealed class BuilderQueueRuleTests
{
    /// <summary>The fixture content with a builder queue of two places.</summary>
    private static readonly GameContent _queueContent = ContentAssert.Valid(ContentFixture.ValidWith(data =>
    {
        Extend(data);
        data.Rules!.BuilderQueueLength = 2;
    }));

    /// <summary>Two buildings under construction until 5000, which keep both builders busy.</summary>
    private static Building[] BothBuildersBusy() => [Constructing(1, "hut", 0, 0, 900, 5000), Constructing(2, "hut", 1, 0, 900, 5000)];

    /// <summary>With every builder busy, a placement pays its coins and joins the queue as order 1: placed, phase <c>Queued</c>, no builder.</summary>
    [Fact]
    public void PlaceBuilding_EveryBuilderBusy_PaysAndQueuesTheConstruction()
    {
        CityState city = City(purse: 100, buildings: BothBuildersBusy());

        RulesAssert.Moved(Place(city, "lumber_camp", 2, 0), spent: "gold=20", refunded: "");

        Building queued = city.Buildings[2];
        Assert.Equal((3, BuildingPhase.Queued, 1, (long?)null), (queued.Id, queued.Phase, queued.QueuedOrder, queued.PhaseEndsAt));
        Assert.Empty(queued.Needs);
        Assert.Equal((80L, 2, 1, 2), (city.Purse, CityQueries.BuildersBusy(city), CityQueries.QueuedOrders(city), city.NextQueuedOrder));
    }

    /// <summary>A queued construction takes its tile: another placement there is <see cref="MutationErrorCode.TileOccupied"/>.</summary>
    [Fact]
    public void PlaceBuilding_OnAQueuedConstructionsTile_IsTileOccupied() =>
        RulesAssert.Rejected(
            MutationErrorCode.TileOccupied,
            City(purse: 100, buildings: [.. BothBuildersBusy(), Queued(3, "hut", 2, 0, 1)]),
            city => Place(city, "hut", 2, 0));

    /// <summary>A queued construction counts toward <c>maxPerCity</c>: a second school is <see cref="MutationErrorCode.BuildingLimit"/>.</summary>
    [Fact]
    public void PlaceBuilding_SecondSchoolWhileOneIsQueued_IsBuildingLimit() =>
        RulesAssert.Rejected(
            MutationErrorCode.BuildingLimit,
            City(purse: 100, buildings: [.. BothBuildersBusy(), Queued(3, "school", 2, 0, 1)]),
            city => Place(city, "school", 3, 0));

    /// <summary>With the queue full too, a placement is <see cref="MutationErrorCode.NoFreeBuilder"/> and changes nothing.</summary>
    [Fact]
    public void PlaceBuilding_QueueFull_IsNoFreeBuilder() =>
        RulesAssert.Rejected(
            MutationErrorCode.NoFreeBuilder,
            City(purse: 100, buildings: [.. BothBuildersBusy(), Queued(3, "hut", 2, 0, 1), Queued(4, "hut", 3, 0, 2)]),
            city => Place(city, "hut", 4, 0));

    /// <summary>Content without a queue (length 0) keeps M0's rule: every builder busy is <see cref="MutationErrorCode.NoFreeBuilder"/>.</summary>
    [Fact]
    public void PlaceBuilding_EveryBuilderBusyWithoutAQueue_IsNoFreeBuilder() =>
        RulesAssert.Rejected(
            MutationErrorCode.NoFreeBuilder,
            City(purse: 100, buildings: BothBuildersBusy()),
            city => CityRules.PlaceBuilding(city, TestContent, new BuildingTypeId("hut"), 2, 0));

    /// <summary>A queued placement still needs its coins: <see cref="MutationErrorCode.InsufficientResource"/> without them.</summary>
    [Fact]
    public void PlaceBuilding_QueuedWithoutTheCoins_IsInsufficientResource() =>
        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, City(purse: 5, buildings: BothBuildersBusy()), city => Place(city, "hut", 2, 0));

    /// <summary>With every builder busy, an upgrade pays its coins and joins the queue: the building stays idle, with no needs yet.</summary>
    [Fact]
    public void UpgradeBuilding_EveryBuilderBusy_PaysAndQueuesTheUpgrade()
    {
        CityState city = City(purse: 100, buildings: [.. BothBuildersBusy(), Idle(3, "depot", 2, 0)]);

        RulesAssert.Moved(Upgrade(city, 3), spent: "gold=25", refunded: "");

        Building depot = city.Buildings[2];
        Assert.Equal((BuildingPhase.Idle, 1, 1, true, false), (depot.Phase, depot.Level, depot.QueuedOrder, depot.HasUpgradeQueued, depot.HasUpgradeWaiting));
        Assert.Empty(depot.Needs);
        Assert.Equal((2, 1), (CityQueries.BuildersBusy(city), CityQueries.QueuedOrders(city)));
    }

    /// <summary>A building whose upgrade is queued can't order another: <see cref="MutationErrorCode.BuildingBusy"/>.</summary>
    [Fact]
    public void UpgradeBuilding_UpgradeAlreadyQueued_IsBuildingBusy() =>
        RulesAssert.Rejected(
            MutationErrorCode.BuildingBusy, City(purse: 100, buildings: [.. BothBuildersBusy(), Idle(3, "depot", 2, 0, queuedOrder: 1)]), city => Upgrade(city, 3));

    /// <summary>A queued construction can't be upgraded: <see cref="MutationErrorCode.BuildingBusy"/>.</summary>
    [Fact]
    public void UpgradeBuilding_QueuedConstruction_IsBuildingBusy() =>
        RulesAssert.Rejected(
            MutationErrorCode.BuildingBusy, City(purse: 100, buildings: [.. BothBuildersBusy(), Queued(3, "depot", 2, 0, 1)]), city => Upgrade(city, 3));

    /// <summary>With the queue full, an upgrade is <see cref="MutationErrorCode.NoFreeBuilder"/> and changes nothing.</summary>
    [Fact]
    public void UpgradeBuilding_QueueFull_IsNoFreeBuilder() =>
        RulesAssert.Rejected(
            MutationErrorCode.NoFreeBuilder,
            City(purse: 100, buildings: [.. BothBuildersBusy(), Queued(3, "hut", 2, 0, 1), Queued(4, "hut", 3, 0, 2), Idle(5, "depot", 4, 0)]),
            city => Upgrade(city, 5));

    /// <summary>A queued construction takes no workers and can't be paused: both are <see cref="MutationErrorCode.BuildingBusy"/>.</summary>
    [Fact]
    public void AssignWorkersAndSetPaused_QueuedConstruction_AreBuildingBusy()
    {
        CityState city = City(purse: 100, buildings: [Idle(1, "hut", 0, 0), Queued(2, "lumber_camp", 1, 0, 1)]);

        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, city, city => CityRules.AssignWorkers(city, _queueContent, 2, 1));
        RulesAssert.Rejected(MutationErrorCode.BuildingBusy, city, city => CityRules.SetPaused(city, _queueContent, 2, true));
    }

    /// <summary>A queued construction accepts no deliveries, even of a good its store will hold: <see cref="MutationErrorCode.ItemNotAccepted"/>.</summary>
    [Fact]
    public void StartTrip_ToAQueuedConstruction_IsItemNotAccepted()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, store: [("wood", 5)]), Queued(3, "lumber_camp", 2, 0, 1)]);

        Assert.False(CityQueries.Accepts(city, _queueContent, city.Buildings[2], new ItemId("wood")));
        RulesAssert.Rejected(
            MutationErrorCode.ItemNotAccepted,
            city,
            city => TripRules.StartTrip(city, _queueContent, 2, 3, new ItemId("wood"), Amount.Exact(1)));
    }

    /// <summary>A queued house provides no workers: it isn't built yet.</summary>
    [Fact]
    public void WorkersProvided_QueuedHouse_ProvidesNone() =>
        Assert.Equal(3, CityQueries.WorkersProvided(City(buildings: [Idle(1, "hut", 0, 0), Queued(2, "hut", 1, 0, 1)]), _queueContent));

    /// <summary>A producer whose upgrade is queued keeps producing: its cycle completes and the next starts.</summary>
    [Fact]
    public void Advance_ProducerWithAQueuedUpgrade_KeepsProducing()
    {
        CityState city = City(
            buildings: [.. BothBuildersBusy(), Producing(3, "lumber_camp", 2, 0, 1, 2, 990, 40, 40, 990, 1010, queuedOrder: 1), Idle(9, "hut", 0, 1)]);

        Simulator.Advance(city, _queueContent, 10);

        Building camp = city.Buildings[2];
        Assert.Equal((BuildingPhase.Producing, 1010L, 1L, 1), (camp.Phase, camp.PhaseStartedAt, camp.GetStock(new ItemId("wood")), camp.QueuedOrder));
    }

    /// <summary>A queued construction starts at the second a construction completes, not before, as a construction with its timer.</summary>
    [Fact]
    public void Advance_ConstructionCompletes_QueuedConstructionStartsAtThatSecond()
    {
        CityState city = City(buildings: [Constructing(1, "hut", 0, 0, 900, 1010), Constructing(2, "hut", 1, 0, 900, 5000), Queued(3, "lumber_camp", 2, 0, 1)]);

        Simulator.Advance(city, _queueContent, 9);
        Assert.Equal(BuildingPhase.Queued, city.Buildings[2].Phase);
        Simulator.Advance(city, _queueContent, 1);

        Building camp = city.Buildings[2];
        Assert.Equal((BuildingPhase.Constructing, 1010L, (long?)1020, 0), (camp.Phase, camp.PhaseStartedAt, camp.PhaseEndsAt, camp.QueuedOrder));
        Assert.Equal((2, 0), (CityQueries.BuildersBusy(city), CityQueries.QueuedOrders(city)));
    }

    /// <summary>Queued orders start in the order they were made, not by building id: order 1 first, order 2 at the next free builder.</summary>
    [Fact]
    public void Advance_TwoQueuedOrders_StartInTheOrderMade()
    {
        CityState city = City(
            buildings: [Constructing(1, "hut", 0, 0, 900, 1010), Constructing(2, "hut", 1, 0, 900, 1050), Idle(3, "depot", 2, 0, queuedOrder: 2), Queued(4, "hut", 3, 0, 1)]);

        Simulator.Advance(city, _queueContent, 10);
        Assert.Equal((BuildingPhase.Idle, 2, BuildingPhase.Constructing, 0), (city.Buildings[2].Phase, city.Buildings[2].QueuedOrder, city.Buildings[3].Phase, city.Buildings[3].QueuedOrder));
        Simulator.Advance(city, _queueContent, 40);

        Building depot = city.Buildings[2];
        Assert.Equal((BuildingPhase.Upgrading, 1050L, (long?)1059, 0), (depot.Phase, depot.PhaseStartedAt, depot.PhaseEndsAt, depot.QueuedOrder));
    }

    /// <summary>A queued construction whose cost has goods starts as a construction site that waits for them.</summary>
    [Fact]
    public void Advance_QueuedConstructionWithGoods_StartsAsASite()
    {
        CityState city = City(buildings: [Constructing(1, "hut", 0, 0, 900, 1010), Constructing(2, "hut", 1, 0, 900, 5000), Queued(3, "workshop", 2, 0, 1)]);

        Simulator.Advance(city, _queueContent, 10);

        Building workshop = city.Buildings[2];
        Assert.True(workshop.IsConstructionSite);
        Assert.Equal(10, workshop.GetNeed(new ItemId("wood")));
    }

    /// <summary>A queued upgrade whose cost has goods starts as an upgrade waiting for them; the building keeps working.</summary>
    [Fact]
    public void Advance_QueuedUpgradeWithGoods_StartsWaitingForThem()
    {
        CityState city = City(buildings: [Constructing(1, "hut", 0, 0, 900, 1010), Constructing(2, "hut", 1, 0, 900, 5000), Idle(3, "lumber_camp", 2, 0, queuedOrder: 1)]);

        Simulator.Advance(city, _queueContent, 10);

        Building camp = city.Buildings[2];
        Assert.Equal((true, 0, 2L), (camp.HasUpgradeWaiting, camp.QueuedOrder, camp.GetNeed(new ItemId("boards"))));
        Assert.Equal(2, CityQueries.BuildersBusy(city));
    }

    /// <summary>A queued upgrade of a producing building without goods starts as an upgrade: the cycle ends and its inputs come back.</summary>
    [Fact]
    public void Advance_QueuedUpgradeOfAProducer_StartsAndRefundsTheCycle()
    {
        CityState city = City(
            buildings:
            [
                Constructing(1, "hut", 0, 0, 900, 1010),
                Constructing(2, "hut", 1, 0, 900, 5000),
                Producing(3, "workshop", 2, 0, 1, 1, 1000, 90, 90, 1000, 1090, queuedOrder: 1),
                Idle(9, "hut", 0, 1),
            ],
            completedResearch: ["carpentry"]);

        SettlementReport report = Simulator.Advance(city, _queueContent, 10);

        Building workshop = city.Buildings[2];
        Assert.Equal((BuildingPhase.Upgrading, 1010L, (long?)1025, 2L), (workshop.Phase, workshop.PhaseStartedAt, workshop.PhaseEndsAt, workshop.GetStock(new ItemId("wood"))));
        Assert.Equal(2, report.Refunded[new ItemId("wood")]);
    }

    /// <summary>A builder slot that research adds starts a queued order at the node's completion second, not one second before.</summary>
    [Fact]
    public void Advance_GuildCompletes_QueuedConstructionStartsAtThatSecond()
    {
        CityState city = City(
            buildings: [.. BothBuildersBusy(), Queued(3, "hut", 2, 0, 1)], activeResearch: new ActiveResearch(new ResearchId("guild"), 1000, 1060));

        Simulator.Advance(city, _queueContent, 59);
        Assert.Equal(BuildingPhase.Queued, city.Buildings[2].Phase);
        Simulator.Advance(city, _queueContent, 1);

        Assert.Equal((BuildingPhase.Constructing, 1060L, (long?)1065), (city.Buildings[2].Phase, city.Buildings[2].PhaseStartedAt, city.Buildings[2].PhaseEndsAt));
    }

    /// <summary>A restored city with a free builder and a queued order starts it in a zero-length advance, at the clock.</summary>
    [Fact]
    public void Advance_ZeroLengthWithAFreeBuilder_StartsTheQueuedOrder()
    {
        CityState city = City(buildings: [Queued(1, "hut", 0, 0, 1)]);

        Simulator.Advance(city, _queueContent, 0);

        Assert.Equal((BuildingPhase.Constructing, 1000L, (long?)1005), (city.Buildings[0].Phase, city.Buildings[0].PhaseStartedAt, city.Buildings[0].PhaseEndsAt));
    }

    /// <summary>A placement of <paramref name="type"/> with the queue content.</summary>
    private static MutationResult Place(CityState city, string type, int x, int y) => CityRules.PlaceBuilding(city, _queueContent, new BuildingTypeId(type), x, y);

    /// <summary>The upgrade of building <paramref name="buildingId"/> with the queue content.</summary>
    private static MutationResult Upgrade(CityState city, int buildingId) => CityRules.UpgradeBuilding(city, _queueContent, buildingId);
}
