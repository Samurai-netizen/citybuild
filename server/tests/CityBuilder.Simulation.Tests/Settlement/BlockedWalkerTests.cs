using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using CityBuilder.Simulation.Tests.Rules;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Settlement;

/// <summary>
/// Stuck walkers (<c>04</c> sections 17 to 19, M1-03 decision 4): a worker's delivery or a courier whose next walk has no path (a removed
/// bridge) waits where it is with its load, spends no stamina, and walks again from the first second a path exists (a bridge placed). The
/// town stands on the Era I map with area C open: a Cottage (5, 5), a Woodcutter's Hut with 50 logs (7, 9), a Warehouse on the east bank
/// (14, 3), a Courier Station (4, 6); the river is x = 12, a bridge goes at (12, 5). The clock starts at 1000.
/// </summary>
public sealed class BlockedWalkerTests
{
    /// <summary>The server's Era I content, with the map.</summary>
    private static readonly GameContent _content = ContentAssert.Valid(ContentFile.ReadServer("era1-content.json"));

    /// <summary>The logs.</summary>
    private static readonly ItemId _logs = new("logs");

    /// <summary>
    /// A delivery whose load reaches the Warehouse after the bridge went delivers it, then waits at the target (its leg
    /// <see cref="TripLeg.BlockedAtTarget"/>) instead of walking back, for as long as no path exists.
    /// </summary>
    [Fact]
    public void Trip_NoPathBackFromTheTarget_DeliversAndWaitsThere()
    {
        CityState city = Town(trips: [new Trip(1, 2, 3, _logs, 10, 1000, 1100, remaining: 40)], woodcutterLogs: 40);

        Simulator.Advance(city, _content, 100_000);

        Trip trip = Assert.Single(city.Trips);
        Assert.Equal(TripLeg.BlockedAtTarget, trip.Leg);
        Assert.Equal((0L, 40L), (trip.Quantity, trip.Remaining));
        Assert.Equal(10, city.Buildings[2].GetStock(_logs));
        Assert.Equal(40, city.Buildings[1].GetStock(_logs));
    }

    /// <summary>The waiting worker walks back from the second a bridge is placed (the zero-length advance after the mutation).</summary>
    [Fact]
    public void Trip_BlockedAtTarget_WalksBackFromTheSecondABridgeIsPlaced()
    {
        CityState city = Town(trips: [new Trip(1, 2, 3, _logs, 10, 1000, 1100, remaining: 40)], woodcutterLogs: 40);
        Simulator.Advance(city, _content, 5000);

        PlaceBridge(city);

        Trip trip = Assert.Single(city.Trips);
        Assert.Equal(TripLeg.ToSource, trip.Leg);
        Assert.Equal(6000, trip.DepartedAt);
        Assert.True(trip.ArrivesAt > 6000);
    }

    /// <summary>
    /// A worker back at the source with no path to the target waits there (<see cref="TripLeg.BlockedAtSource"/>) and takes nothing: the
    /// goods stay in the source's store.
    /// </summary>
    [Fact]
    public void Trip_NoPathFromTheSource_WaitsThereAndLeavesTheGoodsInStore()
    {
        CityState city = Town(trips: [new Trip(1, 2, 3, _logs, 0, 1000, 1100, remaining: 40, leg: TripLeg.ToSource)], woodcutterLogs: 40);

        Simulator.Advance(city, _content, 100_000);

        Trip trip = Assert.Single(city.Trips);
        Assert.Equal(TripLeg.BlockedAtSource, trip.Leg);
        Assert.Equal((0L, 40L), (trip.Quantity, trip.Remaining));
        Assert.Equal(40, city.Buildings[1].GetStock(_logs));
    }

    /// <summary>With a bridge placed, the waiting worker takes its load and walks to the target from that second.</summary>
    [Fact]
    public void Trip_BlockedAtSource_TakesItsLoadAndWalksFromTheSecondABridgeIsPlaced()
    {
        CityState city = Town(trips: [new Trip(1, 2, 3, _logs, 0, 1000, 1100, remaining: 40, leg: TripLeg.ToSource)], woodcutterLogs: 40);
        Simulator.Advance(city, _content, 5000);

        PlaceBridge(city);

        Trip trip = Assert.Single(city.Trips);
        Assert.Equal(TripLeg.ToTarget, trip.Leg);
        Assert.Equal((10L, 30L, 6000L), (trip.Quantity, trip.Remaining, trip.DepartedAt));
        Assert.Equal(30, city.Buildings[1].GetStock(_logs));
    }

    /// <summary>A worker back at the source with nothing left to take ends its delivery, path or not: it never waits for nothing.</summary>
    [Fact]
    public void Trip_BackAtSourceWithNothingToTake_EndsWithoutAPath()
    {
        CityState city = Town(trips: [new Trip(1, 2, 3, _logs, 0, 1000, 1100, remaining: 40, leg: TripLeg.ToSource)], woodcutterLogs: 0);

        Simulator.Advance(city, _content, 5000);

        Assert.Empty(city.Trips);
    }

    /// <summary>
    /// A courier that took its load at the Woodcutter's Hut and finds no path to the Warehouse waits there (<see cref="CourierState.Blocked"/>
    /// at building 2, walking next to stop 2) with its load, and spends no stamina while it waits, though its player is away.
    /// </summary>
    [Fact]
    public void Courier_NoPathToItsNextStop_WaitsAtItsStopWithItsLoadAndSpendsNoStamina()
    {
        CityState city = Town(station: true, route: true, couriers: [Walking(6, 1, toStop: 1, departedAt: 1000, arrivesAt: 1100, stamina: 600)]);

        Simulator.Advance(city, _content, 100_000, absentFrom: 1000);

        Courier courier = Assert.Single(city.Couriers);
        Assert.Equal(CourierState.Blocked, courier.State);
        Assert.Equal((2, 2), (courier.WaitsAt, courier.ToStop));
        Assert.Equal((long?)null, courier.ArrivesAt);
        Assert.Equal(10, courier.CarriedOf(_logs));
        Assert.Equal(500, courier.Stamina);
    }

    /// <summary>The waiting courier walks on to its next stop from the second a bridge is placed.</summary>
    [Fact]
    public void Courier_Blocked_WalksOnFromTheSecondABridgeIsPlaced()
    {
        CityState city = Town(station: true, route: true, couriers: [Walking(6, 1, toStop: 1, departedAt: 1000, arrivesAt: 1100, stamina: 600)]);
        Simulator.Advance(city, _content, 5000, absentFrom: 1000);

        PlaceBridge(city);

        Courier courier = Assert.Single(city.Couriers);
        Assert.Equal(CourierState.Walking, courier.State);
        Assert.Equal((2, 0, 6000L), (courier.ToStop, courier.WaitsAt, courier.DepartedAt!.Value));
        Assert.Equal(10, courier.CarriedOf(_logs));
    }

    /// <summary>
    /// A courier on a delivery line that put its logs into the Warehouse and has no path home waits there, walking home next (stop 0); with a
    /// bridge it walks home from that second.
    /// </summary>
    [Fact]
    public void Courier_NoPathHome_WaitsAtTheTargetThenReturnsWhenABridgeIsPlaced()
    {
        CityState city = Town(
            station: true,
            lines: [Line(1, 6, 1, 2, 3, "logs", 10)],
            couriers: [Walking(6, 1, toStop: 2, departedAt: 1000, arrivesAt: 1100, stamina: 600, load: ("logs", 2, 10))]);
        Simulator.Advance(city, _content, 5000);

        Courier waiting = Assert.Single(city.Couriers);
        Assert.Equal(CourierState.Blocked, waiting.State);
        Assert.Equal((3, 0), (waiting.WaitsAt, waiting.ToStop));
        Assert.Equal(10, city.Buildings[2].GetStock(_logs));

        PlaceBridge(city);

        Assert.Equal(CourierState.Returning, waiting.State);
        Assert.Equal(6000, waiting.DepartedAt);
    }

    /// <summary>
    /// Removing the bridge while a delivery crosses to the east bank: the worker finishes the walk it is on, delivers, and waits at the
    /// Warehouse; nothing throws.
    /// </summary>
    [Fact]
    public void RemoveRoads_TheBridgeUnderADelivery_LeavesItsWorkerWaitingOnTheFarSide()
    {
        CityState city = Town(bridge: true, woodcutterLogs: 50);
        RulesAssert.Ok(TripRules.StartTrip(city, _content, 2, 3, _logs, Amount.Preset(new AmountPresetId("all"))));
        Simulator.Advance(city, _content, 0);

        RulesAssert.Ok(RoadRules.RemoveRoads(city, _content, [(12, 5)]));
        Simulator.Advance(city, _content, 0);
        Simulator.Advance(city, _content, 100_000);

        Assert.Equal(TripLeg.BlockedAtTarget, Assert.Single(city.Trips).Leg);
        Assert.Equal(10, city.Buildings[2].GetStock(_logs));
    }

    /// <summary>A waiting courier is restored only with the building it waits at; no other state names one.</summary>
    [Fact]
    public void Courier_BlockedNeedsTheBuildingItWaitsAt_AndOnlyBlockedHasOne()
    {
        Assert.Throws<ArgumentException>(() => new Courier(6, 1, CourierState.Blocked, 2, null, null, 600));
        Assert.Throws<ArgumentException>(() => new Courier(6, 1, CourierState.Blocked, 2, 1000, 1100, 600, waitsAt: 2));
        Assert.Throws<ArgumentException>(() => new Courier(6, 1, CourierState.AtStation, 0, null, null, 600, waitsAt: 2));
        Assert.Equal(2, new Courier(6, 1, CourierState.Blocked, 0, null, null, 600, waitsAt: 2).WaitsAt);
    }

    /// <summary>A blocked trip keeps the shape of its direction: waiting at the source it carries nothing and has goods to fetch.</summary>
    [Fact]
    public void Trip_BlockedAtSourceCarriesNothingAndHasGoodsToFetch()
    {
        Assert.Throws<ArgumentException>(() => new Trip(1, 2, 3, _logs, 5, 1000, 1100, remaining: 40, leg: TripLeg.BlockedAtSource));
        Assert.Throws<ArgumentException>(() => new Trip(1, 2, 3, _logs, 0, 1000, 1100, remaining: 0, leg: TripLeg.BlockedAtSource));
        Assert.Equal(TripLeg.BlockedAtTarget, new Trip(1, 2, 3, _logs, 5, 1000, 1100, leg: TripLeg.BlockedAtTarget).Leg);
    }

    /// <summary>
    /// Cancelling the line a blocked courier waits to walk on stops it, as it stops a walking one: its load goes back and it stands at its
    /// station.
    /// </summary>
    [Fact]
    public void CancelDelivery_TheLineABlockedCourierWaitsFor_StandsItAtItsStation()
    {
        CityState city = Town(
            station: true,
            lines: [Line(1, 6, 1, 2, 3, "logs", 10)],
            couriers: [new Courier(6, 1, CourierState.Blocked, 2, null, null, 600, [new CourierLoad(_logs, 2, 10)], waitsAt: 2)],
            woodcutterLogs: 0);

        RulesAssert.Ok(CourierRules.CancelDelivery(city, _content, 6, 1, 1));

        Courier courier = Assert.Single(city.Couriers);
        Assert.Equal((CourierState.AtStation, 0), (courier.State, courier.WaitsAt));
        Assert.Equal(10, city.Buildings[1].GetStock(_logs));
        Assert.Empty(courier.Loads);
    }

    /// <summary>A blocked courier is restored only waiting at a building of the city, for a stop its route has (or home).</summary>
    [Fact]
    public void City_BlockedCourierAtAnUnknownBuildingOrForAMissingStop_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => Town(station: true, route: true, couriers: [new Courier(6, 1, CourierState.Blocked, 2, null, null, 600, waitsAt: 9)]));
        Assert.Throws<ArgumentException>(() => Town(station: true, route: true, couriers: [new Courier(6, 1, CourierState.Blocked, 3, null, null, 600, waitsAt: 2)]));
        Assert.Equal(CourierState.Blocked, Town(station: true, route: true, couriers: [new Courier(6, 1, CourierState.Blocked, 0, null, null, 600, waitsAt: 3)]).Couriers[0].State);
    }

    /// <summary>Places the bridge at (12, 5) and runs the zero-length advance that ends every city request.</summary>
    private static void PlaceBridge(CityState city)
    {
        RulesAssert.Ok(RoadRules.PlaceRoads(city, _content, [(12, 5)]));
        Simulator.Advance(city, _content, 0);
    }

    /// <summary>
    /// The town of the class summary; with <paramref name="station"/> the Courier Station (6) with one worker, with <paramref name="route"/>
    /// its route (take 10 logs at the Woodcutter's Hut, put all logs into the Warehouse), with <paramref name="bridge"/> the bridge.
    /// </summary>
    private static CityState Town(
        bool station = false,
        bool route = false,
        bool bridge = false,
        long woodcutterLogs = 50,
        Trip[]? trips = null,
        Courier[]? couriers = null,
        DeliveryLine[]? lines = null)
    {
        var buildings = new List<Building>
        {
            Idle(1, "house", 5, 5),
            Idle(2, "woodcutter", 7, 9, store: [("logs", woodcutterLogs)]),
            Idle(3, "warehouse", 14, 3),
        };
        if (station)
        {
            buildings.Add(Idle(6, "courier_station", 4, 6, workers: 1));
        }

        return City(
            purse: 1000,
            buildings: [.. buildings],
            completedResearch: ["bridge_building", "open_fields", "stone_cutting", "trade_charter"],
            roads: bridge ? [new RoadTile(12, 5, 1, isBridge: true)] : null,
            trips: trips,
            couriers: couriers,
            routes: route ? [RouteOf(6, 1, (2, [(true, "logs", "10")]), (3, [(false, "logs", "all")]))] : null,
            lines: lines);
    }
}
