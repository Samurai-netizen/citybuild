using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// Deliveries of CR-0008 amendment 1 (<c>04</c> sections 8, 18 and 19; D14–D21): a courier's delivery lines, cancelling them, the courier
/// pick, the route gate and a worker's shuttle. Fixture numbers: walking 4 000 ms per ground tile; a post (courier station) level 1 takes 2
/// couriers carrying 5 each, with 600 s of stamina; a worker carries 6; 20 lines per courier. The test city: hut 1 at (0,4), post 2 at (2,2),
/// lumber camp 3 at (0,0), depot 4 at (5,0), clock 1000. Walks: post → camp 16 s, camp ↔ depot 20 s, depot → post 20 s.
/// </summary>
public sealed class DeliveryTests
{
    /// <summary>The wood item of the fixture.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>The fixture content with the school as the route building (the fixture's own content names none).</summary>
    private static readonly GameContent _gatedContent = ContentAssert.Valid(ContentFixture.ValidWith(data => data.Rules!.RouteBuilding = "school"));

    /// <summary>With a built post, a delivery of 20 wood becomes four lines of 5 for its courier, and no wood leaves the camp at the send.</summary>
    [Fact]
    public void StartTrip_WithACourierStation_QueuesLinesOfTheCarryLimit()
    {
        CityState city = PostCity(wood: 20);

        RulesAssert.Moved(Send(city, "all"), spent: "", refunded: "");

        Assert.Equal([5L, 5L, 5L, 5L], city.DeliveryLines.Select(line => line.Quantity));
        Assert.All(city.DeliveryLines, line => Assert.Equal((2, 1, 3, 4), (line.StationId, line.Slot, line.SourceId, line.TargetId)));
        Assert.Equal(20, city.Buildings[2].GetStock(_wood));
        Assert.Empty(city.Trips);
    }

    /// <summary>The amount is counted at the send: half of 18 is 9, one line of 5 and one of 4.</summary>
    [Fact]
    public void StartTrip_Half_CountsTheAmountWhenSent()
    {
        CityState city = PostCity(wood: 18);

        RulesAssert.Ok(Send(city, "half"));

        Assert.Equal([5L, 4L], city.DeliveryLines.Select(line => line.Quantity));
    }

    /// <summary>
    /// The courier leaves its station, runs every line source → target and walks home after the last: takes at 1016, 1056, 1096, 1136, puts
    /// at 1036, 1076, 1116, 1156, home at 1176; all 20 wood are in the depot and the lines are gone.
    /// </summary>
    [Fact]
    public void Advance_CourierWithLines_DeliversEveryLineAndWalksHome()
    {
        CityState city = PostCity(wood: 20);
        RulesAssert.Ok(Send(city, "all"));

        Simulator.Advance(city, TestContent, 155);
        Assert.Equal((15L, 5L), (city.Buildings[3].GetStock(_wood), city.Couriers[0].Carried));
        Assert.Single(city.DeliveryLines);
        Simulator.Advance(city, TestContent, 21);

        Assert.Equal((0L, 20L), (city.Buildings[2].GetStock(_wood), city.Buildings[3].GetStock(_wood)));
        Assert.Empty(city.DeliveryLines);
        Assert.Equal(CourierState.AtStation, city.Couriers[0].State);
    }

    /// <summary>A line whose source is empty when the courier gets there ends without a load, and the courier goes on with the next one.</summary>
    [Fact]
    public void Advance_SourceEmptyAtPickup_EndsTheLineEmpty()
    {
        CityState city = PostCity(wood: 0, lines: [Line(1, 2, 1, 3, 4, "wood", 5)]);

        Simulator.Advance(city, TestContent, 16);

        Assert.Empty(city.DeliveryLines);
        Assert.Equal(CourierState.Returning, city.Couriers[0].State);
    }

    /// <summary>Cancelling one queued line of four delivers the other three: 15 of 20 wood.</summary>
    [Fact]
    public void CancelDelivery_OneQueuedLine_DeliversTheRest()
    {
        CityState city = PostCity(wood: 20);
        RulesAssert.Ok(Send(city, "all"));

        RulesAssert.Moved(Cancel(city, city.DeliveryLines[1].Id), spent: "", refunded: "");
        Simulator.Advance(city, TestContent, 1000);

        Assert.Equal((5L, 15L), (city.Buildings[2].GetStock(_wood), city.Buildings[3].GetStock(_wood)));
    }

    /// <summary>Cancelling the running line gives its load back to the camp and stands the courier at its station; it leaves for the next line.</summary>
    [Fact]
    public void CancelDelivery_RunningLine_GivesTheLoadBackAndStartsAgainFromTheStation()
    {
        CityState city = PostCity(wood: 20);
        RulesAssert.Ok(Send(city, "all"));
        Simulator.Advance(city, TestContent, 20);
        Assert.Equal(5, city.Couriers[0].Carried);

        RulesAssert.Ok(Cancel(city, city.DeliveryLines[0].Id));

        Assert.Equal((20L, 0L), (city.Buildings[2].GetStock(_wood), city.Couriers[0].Carried));
        Assert.Equal(CourierState.AtStation, city.Couriers[0].State);
        Assert.Equal(3, city.DeliveryLines.Count);
        Simulator.Advance(city, TestContent, 0);
        Assert.Equal((CourierState.Walking, 1), (city.Couriers[0].State, city.Couriers[0].ToStop));
    }

    /// <summary>"Cancel all" removes every line and the courier's route.</summary>
    [Fact]
    public void CancelDelivery_All_RemovesEveryLineAndTheRoute()
    {
        CityState lined = PostCity(wood: 20);
        RulesAssert.Ok(Send(lined, "all"));
        CityState routed = PostCity(wood: 20, routes: [RouteOf(2, 1, (3, [(true, "wood", "all")]), (4, [(false, "wood", "all")]))]);

        RulesAssert.Ok(Cancel(lined, null));
        RulesAssert.Ok(Cancel(routed, null));

        Assert.Empty(lined.DeliveryLines);
        Assert.Empty(routed.Routes);
    }

    /// <summary>A line of another courier, or one that doesn't exist, is <see cref="MutationErrorCode.DeliveryNotFound"/>; a slot without a courier is invalid.</summary>
    [Fact]
    public void CancelDelivery_NotThatCouriersLine_IsRejected()
    {
        RulesAssert.Rejected(MutationErrorCode.DeliveryNotFound, PostCity(wood: 20, lines: [Line(1, 2, 1, 3, 4, "wood", 5)]), city => Cancel(city, 7));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, PostCity(wood: 20), city => CourierRules.CancelDelivery(city, TestContent, 2, 2, null));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, PostCity(wood: 20), city => CourierRules.CancelDelivery(city, TestContent, 3, 1, null));
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, PostCity(wood: 20), city => CourierRules.CancelDelivery(city, TestContent, 9, 1, null));
    }

    /// <summary>A delivery goes to the courier with the fewest lines: the second courier when the first has one.</summary>
    [Fact]
    public void StartTrip_TwoCouriers_PicksTheOneWithFewerLines()
    {
        CityState city = PostCity(wood: 20, workers: 2, lines: [Line(1, 2, 1, 3, 4, "wood", 5)]);

        RulesAssert.Ok(Send(city, "half"));

        Assert.Equal(2, city.DeliveryLines[^1].Slot);
    }

    /// <summary>With every courier on a route there is no free courier; workers don't take over once a station is built.</summary>
    [Fact]
    public void StartTrip_EveryCourierOnARoute_IsNoFreeCourier()
    {
        RulesAssert.Rejected(
            MutationErrorCode.NoFreeCourier,
            PostCity(wood: 20, routes: [RouteOf(2, 1, (3, [(true, "wood", "all")]), (4, [(false, "wood", "all")]))]),
            city => Send(city, "all"));
    }

    /// <summary>Lines that don't fit in the courier's queue (20) are <see cref="MutationErrorCode.DeliveryQueueFull"/>: 105 wood is 21 lines of 5.</summary>
    [Fact]
    public void StartTrip_MoreLinesThanTheQueueHolds_IsDeliveryQueueFull()
    {
        RulesAssert.Rejected(MutationErrorCode.DeliveryQueueFull, PostCity(wood: 105), city => Send(city, "all"));
        RulesAssert.Ok(Send(PostCity(wood: 100), "all"));
    }

    /// <summary>Before any courier station is built (a post still constructing), a worker shuttles: 20 wood as 6 + 6 + 6 + 2, done at 1140.</summary>
    [Fact]
    public void Advance_WorkerDelivery_ShuttlesUntilEverythingIsDelivered()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 4), Constructing(2, "post", 2, 2, 990, 5000), Idle(3, "lumber_camp", 0, 0, store: [("wood", 20)]), Idle(4, "depot", 5, 0)]);

        RulesAssert.Ok(Send(city, "all"));
        Assert.Equal((6L, 14L), (city.Trips[0].Quantity, city.Trips[0].Remaining));
        Simulator.Advance(city, TestContent, 139);
        Assert.Equal((TripLeg.ToTarget, 2L, 0L), (city.Trips[0].Leg, city.Trips[0].Quantity, city.Trips[0].Remaining));
        Simulator.Advance(city, TestContent, 1);

        Assert.Empty(city.Trips);
        Assert.Equal((0L, 20L), (city.Buildings[2].GetStock(_wood), city.Buildings[3].GetStock(_wood)));
        Assert.Equal(3, CityQueries.FreeWorkers(city, TestContent));
    }

    /// <summary>A worker who finds the source empty on the way back for the next load ends the trip there.</summary>
    [Fact]
    public void Advance_WorkerFindsTheSourceEmpty_EndsTheTrip()
    {
        CityState city = City(
            buildings: [Idle(1, "hut", 0, 4), Idle(3, "lumber_camp", 0, 0), Idle(4, "depot", 5, 0)],
            trips: [new Trip(1, 3, 4, _wood, 0, 990, 1010, remaining: 8, leg: TripLeg.ToSource)]);

        Simulator.Advance(city, TestContent, 10);

        Assert.Empty(city.Trips);
    }

    /// <summary>A route needs the content's route building built: refused before the school exists or while it is a site, stored once it is built.</summary>
    [Fact]
    public void SetRoute_RouteBuilding_GatesTheRoute()
    {
        RouteStop[] stops = Stops((3, [(true, "wood", "all")]), (4, [(false, "wood", "all")]));
        CityState none = PostCity(wood: 0);
        CityState site = PostCity(wood: 0, extra: [Constructing(5, "school", 7, 7, 990, 5000)]);
        CityState built = PostCity(wood: 0, extra: [Idle(5, "school", 7, 7)]);

        Assert.Equal(MutationErrorCode.RoutesLocked, CourierRules.SetRoute(none, _gatedContent, 2, 1, stops).ErrorCode);
        Assert.Equal(MutationErrorCode.RoutesLocked, CourierRules.SetRoute(site, _gatedContent, 2, 1, stops).ErrorCode);
        RulesAssert.Ok(CourierRules.SetRoute(built, _gatedContent, 2, 1, stops));
        RulesAssert.Ok(CourierRules.SetRoute(none, _gatedContent, 2, 1, []));
    }

    /// <summary>A courier with delivery lines takes no route (<see cref="MutationErrorCode.CourierBusy"/>); clearing its route still works.</summary>
    [Fact]
    public void SetRoute_CourierWithLines_IsCourierBusy()
    {
        RouteStop[] stops = Stops((3, [(true, "wood", "all")]), (4, [(false, "wood", "all")]));

        RulesAssert.Rejected(MutationErrorCode.CourierBusy, PostCity(wood: 20, lines: [Line(1, 2, 1, 3, 4, "wood", 5)]), city => CourierRules.SetRoute(city, TestContent, 2, 1, stops));
        RulesAssert.Ok(CourierRules.SetRoute(PostCity(wood: 20, lines: [Line(1, 2, 1, 3, 4, "wood", 5)]), TestContent, 2, 1, []));
    }

    /// <summary>Removing the worker of a courier removes its lines with it.</summary>
    [Fact]
    public void AssignWorkers_CourierRemoved_RemovesItsLines()
    {
        CityState city = PostCity(wood: 20, lines: [Line(1, 2, 1, 3, 4, "wood", 5), Line(2, 2, 1, 3, 4, "wood", 5)]);

        RulesAssert.Ok(CityRules.AssignWorkers(city, TestContent, 2, 0));

        Assert.Empty(city.DeliveryLines);
        Assert.Empty(city.Couriers);
    }

    /// <summary>A courier that tires at a pickup walks home with its load, and its lines wait for it.</summary>
    [Fact]
    public void Advance_CourierTiresAtThePickup_WalksHomeAndKeepsItsLines()
    {
        CityState city = PostCity(wood: 20, stamina: 10);
        RulesAssert.Ok(Send(city, "all"));

        Simulator.Advance(city, TestContent, 16, absentFrom: 1000);

        Assert.Equal((CourierState.Returning, 5L), (city.Couriers[0].State, city.Couriers[0].Carried));
        Assert.Equal(4, city.DeliveryLines.Count);
    }

    /// <summary>The test city of the class summary: the post with <paramref name="workers"/> couriers at their station, the camp with wood.</summary>
    private static CityState PostCity(
        long wood, int workers = 1, long stamina = 600, DeliveryLine[]? lines = null, Route[]? routes = null, Building[]? extra = null) =>
        City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "post", 2, 2, workers: workers), Idle(3, "lumber_camp", 0, 0, store: [("wood", wood)]), Idle(4, "depot", 5, 0), .. extra ?? []],
            couriers: [.. Enumerable.Range(1, workers).Select(slot => AtStation(2, slot, stamina))],
            routes: routes,
            lines: lines);

    /// <summary>Sends wood from the camp to the depot with the fixture content and a preset amount.</summary>
    private static MutationResult Send(CityState city, string preset) =>
        TripRules.StartTrip(city, TestContent, 3, 4, _wood, Amount.Preset(new AmountPresetId(preset)));

    /// <summary>Cancels a line of courier 2/1, or all of its lines with null.</summary>
    private static MutationResult Cancel(CityState city, int? lineId) => CourierRules.CancelDelivery(city, TestContent, 2, 1, lineId);
}
