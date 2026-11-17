using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// A store never goes above its limit (<c>04</c> §10 invariant 6, §16, §18, §19; M0-C-3, the owner's playtest): a running cycle's inputs
/// count against its own store's room, so the upgrade refund fits; goods that don't fit stay with their walker: a courier puts them back as
/// room appears once it has no work, a tired courier with work keeps them, a trip worker carries them back and waits at its source; a
/// station worker whose courier's load doesn't fit back can't be removed. The test city: hut 1 at (0,4) (3 workers), post 2 at (2,2)
/// (courier station, carries 5), lumber camp 3 at (0,0) and depot 4 at (5,0), each holding 100 wood; 20 s between the camp and the depot;
/// clock 1000.
/// </summary>
public sealed class StoreLimitRuleTests
{
    /// <summary>The good every test moves.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>A producing workshop (20 wood at level 1) whose cycle took 2 wood has room for 20 − 18 − 2 = 0 more.</summary>
    [Fact]
    public void Room_ProducingBuilding_CountsItsCyclesInputs()
    {
        CityState city = City(buildings:
        [
            Idle(1, "hut", 0, 4),
            Producing(2, "workshop", 1, 0, 1, workers: 1, cycleStartedAt: 1000, cycleLabor: 90, laborRemaining: 90, segmentStartedAt: 1000, endsAt: 1090,
                store: [("wood", 18)]),
        ]);

        Assert.Equal(0, CityQueries.Room(city, TestContent, city.Buildings[1], _wood));
    }

    /// <summary>
    /// A trip whose target takes 2 of its 6 carries the other 4 back (its sending ends); at the full camp it waits with them, and once a
    /// second trip takes 6 out of the camp it puts its 4 back in the request's advance and ends. The city has no courier station, so
    /// workers carry.
    /// </summary>
    [Fact]
    public void Trip_TargetWithoutRoom_CarriesTheRestBackAndWaitsAtTheFullSource()
    {
        CityState city = LimitCity(campWood: 100, depotWood: 98, station: false, trips: [Trip(1, 3, 4, "wood", 6, 990, 1010)]);

        Simulator.Advance(city, TestContent, 10);
        Assert.Equal((TripLeg.ToSource, 4L, 0L, 1030L), (city.Trips[0].Leg, city.Trips[0].Quantity, city.Trips[0].Remaining, city.Trips[0].ArrivesAt));

        Simulator.Advance(city, TestContent, 20);
        Assert.Equal((TripLeg.Waiting, 4L), (city.Trips[0].Leg, city.Trips[0].Quantity));
        Assert.Equal(100, Wood(city, 3));

        Assert.True(Amount.TryParse("all", out Amount? all));
        RulesAssert.Ok(TripRules.StartTrip(city, TestContent, 3, 4, _wood, all));
        Simulator.Advance(city, TestContent, 0);
        Assert.Equal(98, Wood(city, 3));
        Assert.Equal([2], city.Trips.Select(trip => trip.Id));
    }

    /// <summary>
    /// A courier on a line puts 2 of its 5 into the nearly full depot; the other 3 stay on it, it walks home, and at its station it keeps
    /// them while the camp they came from is full.
    /// </summary>
    [Fact]
    public void Courier_LinePutLeftover_StaysOnTheCourierWhileTheSourceIsFull()
    {
        CityState city = LimitCity(
            campWood: 100,
            depotWood: 98,
            couriers: [Walking(2, 1, 2, 990, 1010, load: ("wood", 3, 5))],
            lines: [Line(1, 2, 1, 3, 4, "wood", 5)]);

        Simulator.Advance(city, TestContent, 100);

        Assert.Equal("\ncourier 2/1 AtStation stamina=600 load: wood@3=3", CityDump.CourierLine(city.Couriers[0]));
        Assert.Equal((100L, 100L), (Wood(city, 3), Wood(city, 4)));
    }

    /// <summary>A courier without work at its station puts its load back as far as the camp has room (3 of 5) and keeps the rest.</summary>
    [Fact]
    public void Courier_WithoutWorkAtItsStation_PutsItsLoadBackAsRoomAppears()
    {
        CityState city = LimitCity(campWood: 97, couriers: [AtStation(2, 1, load: ("wood", 3, 5))]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal("\ncourier 2/1 AtStation stamina=600 load: wood@3=2", CityDump.CourierLine(city.Couriers[0]));
        Assert.Equal(100, Wood(city, 3));
    }

    /// <summary>A tired courier with a route keeps its load for the route, even with room in the camp it came from.</summary>
    [Fact]
    public void Courier_RestingWithARoute_KeepsItsLoad()
    {
        CityState city = LimitCity(
            campWood: 50,
            couriers: [new Courier(2, 1, CourierState.Resting, 0, null, null, 0, [new CourierLoad(_wood, 3, 5)])],
            routes: [RouteOf(2, 1, (3, [(true, "wood", "all")]), (4, [(false, "wood", "all")]))]);

        Simulator.Advance(city, TestContent, 0);

        Assert.Equal("\ncourier 2/1 Resting stamina=0 load: wood@3=5", CityDump.CourierLine(city.Couriers[0]));
        Assert.Equal(50, Wood(city, 3));
    }

    /// <summary>A walking courier whose route is replaced gives back what fits (2 of 5) and keeps the other 3 on its new route.</summary>
    [Fact]
    public void SetRoute_ReplacingAWalkingCouriersRoute_GivesBackOnlyWhatFits()
    {
        CityState city = LimitCity(
            campWood: 98,
            couriers: [Walking(2, 1, 2, 990, 1010, load: ("wood", 3, 5))],
            routes: [RouteOf(2, 1, (3, [(true, "wood", "all")]), (4, [(false, "wood", "all")]))]);

        RulesAssert.Ok(CourierRules.SetRoute(city, TestContent, 2, 1, Stops((4, [(false, "wood", "all")]), (1, [(false, "wood", "all")]))));

        Assert.Equal("\ncourier 2/1 AtStation stamina=600 load: wood@3=3", CityDump.CourierLine(city.Couriers[0]));
        Assert.Equal(100, Wood(city, 3));
    }

    /// <summary>Removing the station worker of a courier whose 4 wood don't fit back into the full camp is refused, and nothing changes.</summary>
    [Fact]
    public void AssignWorkers_RemovingACourierWhoseLoadDoesNotFitBack_IsRefusedCourierBusy()
    {
        CityState city = LimitCity(
            campWood: 100,
            workers: 2,
            couriers: [AtStation(2, 1), Walking(2, 2, 1, 990, 1010, load: ("wood", 3, 4))],
            routes: [RouteOf(2, 2, (3, [(true, "wood", "all")]), (1, [(false, "wood", "all")]))]);

        RulesAssert.Rejected(MutationErrorCode.CourierBusy, city, target => CityRules.AssignWorkers(target, TestContent, 2, 1));
    }

    /// <summary>The wood in the store of building <paramref name="buildingId"/>.</summary>
    private static long Wood(CityState city, int buildingId) =>
        city.TryGetBuilding(buildingId, out Building? building) ? building!.GetStock(_wood) : throw new ArgumentException("No building " + buildingId);

    /// <summary>The test city of the class summary with the stores, station workers, trips and couriers a test sets (without the post when <paramref name="station"/> is false).</summary>
    private static CityState LimitCity(
        bool station = true,
        long campWood = 0,
        long depotWood = 0,
        int workers = 1,
        Trip[]? trips = null,
        Courier[]? couriers = null,
        Route[]? routes = null,
        DeliveryLine[]? lines = null) =>
        City(
            buildings: new[]
            {
                Idle(1, "hut", 0, 4),
                station ? Idle(2, "post", 2, 2, workers: workers) : null,
                Idle(3, "lumber_camp", 0, 0, store: campWood > 0 ? [("wood", campWood)] : null),
                Idle(4, "depot", 5, 0, store: depotWood > 0 ? [("wood", depotWood)] : null),
            }.OfType<Building>().ToArray(),
            trips: trips,
            couriers: couriers ?? (station && workers > 0 ? [AtStation(2, 1)] : null),
            routes: routes,
            lines: lines);
}
