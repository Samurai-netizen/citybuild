using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// Trip arrivals and the delivery rule (<c>04</c> sections 6, 16 and 18): a walker's goods go to the building's waiting project first (used
/// up: <c>delivered</c>), then into its store up to its room (<c>moved</c>), and the rest back to the trip's source, even above its limit; a
/// project whose last good arrives starts that second; arrivals come after building completions and in trip id order. Fixture numbers: a hut
/// provides 3 workers; a workshop (2 wood → 1 boards, 90 labor; 180 at level 2) builds in 20 s, holds 20 wood at level 1 and upgrades in 15 s;
/// a lumber camp and a depot hold 100 wood; better saws takes 200 s; the school has no store.
/// </summary>
public sealed class DeliveryRuleTests
{
    /// <summary>The good most trips here carry.</summary>
    private static readonly ItemId _wood = new("wood");
    /// <summary>The good of the upgrade and research tests.</summary>
    private static readonly ItemId _boards = new("boards");

    /// <summary>
    /// The fixture content with a second school level whose upgrade costs 10 gold and 4 boards (20 s), so the science building can have a
    /// project of its own beside the research.
    /// </summary>
    private static readonly GameContent _schoolWithLevel2 = ContentAssert.Valid(ContentFixture.ValidWith(data =>
        ContentFixture.Building(data, "school").Levels!.Add(new BuildingLevelData { Level = 2, UpgradeCost = new() { ["gold"] = 10, ["boards"] = 4 }, UpgradeSeconds = 20 })));

    /// <summary>
    /// Two trips fill a construction site's 10 wood: the first leaves 4 missing, the second completes it, the build timer runs from that second
    /// for 20 s, and the 2 wood left over go into the site's own store.
    /// </summary>
    [Fact]
    public void Delivery_LastGoodsOfAConstructionSite_StartTheBuildTimer()
    {
        CityState city = City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0), Site(3, "workshop", 5, 0, needs: [("wood", 10)])],
            trips: [Trip(1, 2, 3, "wood", 6, 990, 1008), Trip(2, 2, 3, "wood", 6, 995, 1010)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 20);

        Assert.Equal("building 3 workshop (5,0) L1 w=0 Constructing 1010..1030 target=0 labor=0/0@0", CityDump.BuildingLine(city, 3));
        Assert.Equal("store: wood=2 needs:", CityDump.StoreLine(city, 3));
        Assert.Equal((10L, 2L), (report.Delivered[_wood], report.Moved[_wood]));
        Assert.Empty(city.Trips);
        Assert.Equal([3], Simulator.Advance(city, TestContent, 10).CompletedBuildings);
    }

    /// <summary>
    /// The last good of an upgrade waiting on a producing workshop starts the upgrade at the arrival: the running cycle ends, its 2 wood come
    /// back (the report's <c>refunded</c>), and the upgrade's end replaces the cycle's in the advance, so the advance goes on past the old end.
    /// </summary>
    [Fact]
    public void Delivery_LastGoodOfAWaitingUpgrade_StartsItAndRefundsTheCycle()
    {
        CityState city = City(
            buildings:
            [
                Idle(1, "hut", 0, 4),
                Producing(2, "workshop", 1, 0, 1, workers: 1, cycleStartedAt: 1000, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 1000, endsAt: 1090,
                    store: [("wood", 1)], needs: [("boards", 2)]),
                Idle(3, "depot", 3, 0, store: [("boards", 10)]),
            ],
            trips: [Trip(1, 3, 2, "boards", 2, 996, 1004)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 100);

        // 1004: upgrading to 1019 with the 2 wood back (3); 1019: level 2, and a 180-labor cycle starts on 2 of them (1 left), ending 1199.
        Assert.Equal("building 2 workshop (1,0) L2 w=1 Producing 1019..1199 target=0 labor=180/180@1019", CityDump.BuildingLine(city, 2));
        Assert.Equal("store: wood=1 needs:", CityDump.StoreLine(city, 2));
        Assert.Equal((2L, 2L, 2L), (report.Delivered[_boards], report.Refunded[_wood], report.Consumed[_wood]));
        Assert.Equal([2], report.CompletedBuildings);
        Assert.Empty(report.Produced);
    }

    /// <summary>
    /// The research waiting at the school takes its 5 boards and its timer runs from the arrival for 200 s; the school has no store, so the
    /// sixth board goes back to the depot.
    /// </summary>
    [Fact]
    public void Delivery_ResearchGoodsAtTheScienceBuilding_StartTheResearchTimer()
    {
        CityState city = City(
            completedResearch: ["carpentry"],
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "depot", 0, 0, store: [("boards", 3)]), Idle(3, "school", 2, 0)],
            activeResearch: new ActiveResearch(new ResearchId("better_saws"), null, null, [new(_boards, 5)]),
            trips: [Trip(1, 2, 3, "boards", 6, 996, 1008)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 10);

        ActiveResearch research = city.ActiveResearch!;
        Assert.Equal((false, (long?)1008, (long?)1208), (research.IsWaiting, research.StartedAt, research.EndsAt));
        Assert.Equal(4, city.Buildings[1].GetStock(_boards));
        Assert.Equal(5, report.Delivered[_boards]);
        Assert.Empty(report.Moved);
    }

    /// <summary>
    /// At a science building with an upgrade of its own waiting, the delivery fills that upgrade first (it starts), then the research gets the
    /// rest: 4 of the 6 boards, then 2 (BL-20261102-07 decision 37).
    /// </summary>
    [Fact]
    public void Delivery_ScienceBuildingWithItsOwnProject_FillsItsOwnProjectFirst()
    {
        CityState city = City(
            completedResearch: ["carpentry"],
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "depot", 0, 0), Idle(3, "school", 2, 0, needs: [("boards", 4)])],
            activeResearch: new ActiveResearch(new ResearchId("better_saws"), null, null, [new(_boards, 5)]),
            trips: [Trip(1, 2, 3, "boards", 6, 996, 1008)]);

        SettlementReport report = Simulator.Advance(city, _schoolWithLevel2, 10);

        Assert.Equal("building 3 school (2,0) L1 w=0 Upgrading 1008..1028 target=2 labor=0/0@0", CityDump.BuildingLine(city, 3));
        Assert.Equal([("boards", 3L)], city.ActiveResearch!.Needs.Select(need => (need.Key.Value, need.Value)));
        Assert.Equal(6, report.Delivered[_boards]);
    }

    /// <summary>
    /// A store with too little room takes what fits (2 of 6); the rest goes back into the source's store at that second, even above its
    /// limit (a returned load: 100 + 4).
    /// </summary>
    [Fact]
    public void Delivery_StoreWithoutRoom_ReturnsTheRestToTheSourceEvenAboveItsLimit()
    {
        CityState city = City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0, store: [("wood", 100)]), Idle(3, "depot", 5, 0, store: [("wood", 98)])],
            trips: [Trip(1, 2, 3, "wood", 6, 990, 1010)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 10);

        Assert.Equal((104L, 100L), (city.Buildings[1].GetStock(_wood), city.Buildings[2].GetStock(_wood)));
        Assert.Equal(2, report.Moved[_wood]);
        Assert.Empty(report.Delivered);
    }

    /// <summary>
    /// Trips arriving in one second deliver in trip id order: trip 1 (from camp 3) gives 6 of the site's 8, trip 2 (from camp 2) the last 2,
    /// and its other 4 go back to camp 2, since the site's store is full.
    /// </summary>
    [Fact]
    public void Delivery_TripsArrivingTogether_DeliverInTripIdOrder()
    {
        CityState city = City(
            buildings:
            [
                Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0), Idle(3, "lumber_camp", 0, 2),
                Site(4, "workshop", 5, 0, needs: [("wood", 8)], store: [("wood", 20)]),
            ],
            trips: [Trip(1, 3, 4, "wood", 6, 990, 1010), Trip(2, 2, 4, "wood", 6, 990, 1010)]);

        SettlementReport report = Simulator.Advance(city, TestContent, 10);

        Assert.Equal((4L, 0L), (city.Buildings[1].GetStock(_wood), city.Buildings[2].GetStock(_wood)));
        Assert.Equal(8, report.Delivered[_wood]);
        Assert.Equal("building 4 workshop (5,0) L1 w=0 Constructing 1010..1030 target=0 labor=0/0@0", CityDump.BuildingLine(city, 4));
    }

    /// <summary>
    /// A building that completes in the second a trip arrives completes first (<c>04</c> section 6): the camp is staffed while the trip's
    /// worker is still away (0 free), and the worker is free only after.
    /// </summary>
    [Fact]
    public void Arrival_InTheSecondABuildingCompletes_ComesAfterTheCompletion()
    {
        CityState city = City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0, workers: 2), Constructing(3, "lumber_camp", 2, 0, 990, 1008), Idle(4, "depot", 4, 0)],
            trips: [Trip(1, 2, 4, "wood", 1, 995, 1008)]);

        Simulator.Advance(city, TestContent, 8);

        Assert.Equal("building 3 lumber_camp (2,0) L1 w=0 Idle 0..null target=0 labor=0/0@0", CityDump.BuildingLine(city, 3));
        Assert.Equal((1L, 1L), (CityQueries.FreeWorkers(city, TestContent), city.Buildings[3].GetStock(_wood)));
    }

    /// <summary>A restored city with a trip arriving at its clock delivers it in a zero-length advance (<c>04</c> section 7).</summary>
    [Fact]
    public void Arrival_AtTheClock_IsDeliveredByAZeroLengthAdvance()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0), Idle(3, "depot", 5, 0)], trips: [Trip(1, 2, 3, "wood", 6, 980, 1000)]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal((6L, 0), (city.Buildings[2].GetStock(_wood), city.Trips.Count));
    }

    /// <summary>A trip due before the city's clock makes the city invalid: the advance throws before it changes anything.</summary>
    [Fact]
    public void Advance_TripDueBeforeTheClock_Throws()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0), Idle(3, "depot", 5, 0)], trips: [Trip(1, 2, 3, "wood", 6, 980, 990)]);
        string before = CityDump.Describe(city);

        Assert.Throws<InvalidOperationException>(() => Simulator.Advance(city, TestContent, 10));
        Assert.Equal(before, CityDump.Describe(city));
    }
}
