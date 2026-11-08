using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// Couriers and routes in the city state (<c>04</c> section 19): the values a courier, a load part and a route keep, the checks of their
/// constructors, and the restoring constructor's checks and order. The test city: hut 1 at (0,4), post 2 at (2,2), lumber camp 3 at (0,0).
/// </summary>
public sealed class CityCouriersTests
{
    /// <summary>The camp's wood to the hut: a valid two-stop route.</summary>
    private static readonly (int, (bool, string, string)[])[] _campToHut = [(3, [(true, "wood", "all")]), (1, [(false, "wood", "all")])];

    /// <summary>A courier keeps every value; its load is sorted by good, then origin, and counted all together and per good.</summary>
    [Fact]
    public void Courier_Values_AreKeptAndTheLoadSorted()
    {
        var courier = new Courier(2, 1, CourierState.Walking, 2, 990, 1010, 400,
            [new CourierLoad(new ItemId("wood"), 3, 4), new CourierLoad(new ItemId("boards"), 3, 1), new CourierLoad(new ItemId("wood"), 1, 2)]);

        Assert.Equal("\ncourier 2/1 Walking to=2 990..1010 stamina=400 load: boards@3=1 wood@1=2 wood@3=4", CityDump.CourierLine(courier));
        Assert.Equal((7L, 6L), (courier.Carried, courier.CarriedOf(new ItemId("wood"))));
    }

    /// <summary>Values that form no courier are refused: ids below 1, negative stamina, times or a stop that don't fit the state.</summary>
    [Fact]
    public void Courier_InvalidValues_AreRefused()
    {
        Assert.Throws<ArgumentException>(() => new Courier(0, 1, CourierState.AtStation, 0, null, null, 10));
        Assert.Throws<ArgumentException>(() => new Courier(2, 0, CourierState.AtStation, 0, null, null, 10));
        Assert.Throws<ArgumentException>(() => new Courier(2, 1, CourierState.AtStation, 0, null, null, -1));
        Assert.Throws<ArgumentException>(() => new Courier(2, 1, CourierState.AtStation, 0, 5, 10, 10));
        Assert.Throws<ArgumentException>(() => new Courier(2, 1, CourierState.Walking, 0, 5, 10, 10));
        Assert.Throws<ArgumentException>(() => new Courier(2, 1, CourierState.Walking, 1, 10, 10, 10));
        Assert.Throws<ArgumentException>(() => new Courier(2, 1, CourierState.Returning, 1, 5, 10, 10));
        Assert.Throws<ArgumentException>(() => new Courier(2, 1, CourierState.Resting, 0, null, 10, 10));
        Assert.Throws<ArgumentException>(() => new Courier(2, 1, CourierState.AtStation, 0, null, null, 10,
            [new CourierLoad(new ItemId("wood"), 3, 1), new CourierLoad(new ItemId("wood"), 3, 2)]));
        Assert.Throws<ArgumentException>(() => new CourierLoad(new ItemId("wood"), 0, 1));
        Assert.Throws<ArgumentException>(() => new CourierLoad(new ItemId("wood"), 3, 0));
    }

    /// <summary>A route needs two or more stops and no two consecutive ones at one building, the last and the first included; a stop an action.</summary>
    [Fact]
    public void Route_InvalidShapes_AreRefused()
    {
        Assert.Throws<ArgumentException>(() => RouteOf(2, 1, (3, [(true, "wood", "all")])));
        Assert.Throws<ArgumentException>(() => RouteOf(2, 1, (3, [(true, "wood", "all")]), (3, [(false, "wood", "all")])));
        Assert.Throws<ArgumentException>(() => RouteOf(2, 1, (3, [(true, "wood", "all")]), (1, [(false, "wood", "all")]), (3, [(true, "wood", "all")])));
        Assert.Throws<ArgumentException>(() => RouteOf(0, 1, _campToHut));
        Assert.Throws<ArgumentException>(() => new RouteStop(3, []));
        Assert.Throws<ArgumentException>(() => new RouteStop(0, Stops(_campToHut)[0].Actions));
    }

    /// <summary>The restoring constructor sorts couriers and routes by (station, slot).</summary>
    [Fact]
    public void Constructor_CouriersAndRoutesInAnyOrder_SortsThemByStationAndSlot()
    {
        CityState city = CourierCity(
            couriers: [AtStation(2, 2), AtStation(2, 1)],
            routes: [RouteOf(2, 2, _campToHut), RouteOf(2, 1, _campToHut)]);

        Assert.Equal([1, 2], city.Couriers.Select(courier => courier.Slot));
        Assert.Equal([1, 2], city.Routes.Select(route => route.Slot));
        Assert.True(city.TryGetRoute(2, 2, out Route? route));
        Assert.Equal(2, route!.Slot);
        Assert.False(city.TryGetRoute(2, 3, out _));
    }

    /// <summary>
    /// The restoring constructor refuses a courier or route of a building the city doesn't have, a slot listed twice, a load from a missing
    /// building, a stop at one, and a walking courier whose route lacks its stop.
    /// </summary>
    [Fact]
    public void Constructor_InvalidCouriersOrRoutes_AreRefused()
    {
        Assert.Throws<ArgumentException>(() => CourierCity(couriers: [AtStation(9, 1)]));
        Assert.Throws<ArgumentException>(() => CourierCity(couriers: [AtStation(2, 1), AtStation(2, 1)]));
        Assert.Throws<ArgumentException>(() => CourierCity(couriers: [AtStation(2, 1, load: ("wood", 9, 1))]));
        Assert.Throws<ArgumentException>(() => CourierCity(routes: [RouteOf(9, 1, _campToHut)]));
        Assert.Throws<ArgumentException>(() => CourierCity(routes: [RouteOf(2, 1, (3, [(true, "wood", "all")]), (9, [(false, "wood", "all")]))]));
        Assert.Throws<ArgumentException>(() => CourierCity(routes: [RouteOf(2, 1, _campToHut), RouteOf(2, 1, _campToHut)]));
        Assert.Throws<ArgumentException>(() => CourierCity(couriers: [Walking(2, 1, 1, 990, 1010)]));
        Assert.Throws<ArgumentException>(() => CourierCity(couriers: [Walking(2, 1, 3, 990, 1010)], routes: [RouteOf(2, 1, _campToHut)]));
    }

    /// <summary>The test city of the class summary with the given couriers and routes.</summary>
    private static CityState CourierCity(Courier[]? couriers = null, Route[]? routes = null) =>
        City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "post", 2, 2, workers: 2), Idle(3, "lumber_camp", 0, 0)],
            couriers: couriers,
            routes: routes);
}
