using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// The courier side of staffing a courier station and of starting its upgrade (<c>04</c> sections 3, 8 and 19). Fixture numbers: a hut
/// provides 3 workers; a post (courier station) level 1 takes 2 couriers with 600 s of stamina; its level 2 costs 35 gold and 4 boards (in
/// <see cref="_coinsOnlyUpgrade"/> only 35 gold, so the upgrade starts at once). The test city: hut 1 at (0,4), post 2 at (2,2), lumber camp
/// 3 at (0,0) with 20 wood, clock 1000, 100 gold.
/// </summary>
public sealed class CourierStaffingTests
{
    /// <summary>The fixture content with the post's level 2 costing coins only, so <c>UpgradeBuilding</c> starts it at once.</summary>
    private static readonly GameContent _coinsOnlyUpgrade = ContentAssert.Valid(ContentFixture.ValidWith(data =>
        ContentFixture.Level(data, "post", 2).UpgradeCost = new() { ["gold"] = 35 }));

    /// <summary>Each new worker of a station is a courier at the station in the next slot, with its level's full stamina and nothing carried.</summary>
    [Fact]
    public void AssignWorkers_StationUp_AddsCouriersAtTheStationWithFullStamina()
    {
        CityState city = StationCity(workers: 0, couriers: []);

        RulesAssert.Ok(CityRules.AssignWorkers(city, TestContent, 2, 2));

        Assert.Equal(
            ["\ncourier 2/1 AtStation stamina=600 load:", "\ncourier 2/2 AtStation stamina=600 load:"],
            city.Couriers.Select(CityDump.CourierLine));
    }

    /// <summary>
    /// Fewer workers remove the highest slots' couriers: courier 2/2's 4 wood go back to the camp it came from, and the
    /// slots' routes stay (CR-0008-T1 decision 48).
    /// </summary>
    [Fact]
    public void AssignWorkers_StationDown_RemovesTheHighestSlotsAndGivesTheirLoadsBack()
    {
        CityState city = StationCity(
            workers: 2,
            couriers: [AtStation(2, 1), Walking(2, 2, 1, 990, 1010, load: ("wood", 3, 4))],
            routes: [RouteOf(2, 2, (3, [(true, "wood", "all")]), (1, [(false, "wood", "all")]))]);

        RulesAssert.Ok(CityRules.AssignWorkers(city, TestContent, 2, 1));

        Assert.Equal(["\ncourier 2/1 AtStation stamina=600 load:"], city.Couriers.Select(CityDump.CourierLine));
        Assert.Equal("store: wood=24 needs:", CityDump.StoreLine(city, 3));
        Assert.True(city.TryGetRoute(2, 2, out _));
    }

    /// <summary>The same count again changes no courier, and a producer's staffing makes none.</summary>
    [Fact]
    public void AssignWorkers_SameCountOrAProducer_LeavesTheCouriersAlone()
    {
        CityState city = StationCity(workers: 1, couriers: [Walking(2, 1, 1, 990, 1010, load: ("wood", 3, 2))],
            routes: [RouteOf(2, 1, (3, [(true, "wood", "all")]), (1, [(false, "wood", "all")]))]);
        string before = string.Concat(city.Couriers.Select(CityDump.CourierLine));

        RulesAssert.Ok(CityRules.AssignWorkers(city, TestContent, 2, 1));
        RulesAssert.Ok(CityRules.AssignWorkers(city, TestContent, 3, 1));

        Assert.Equal(before, string.Concat(city.Couriers.Select(CityDump.CourierLine)));
    }

    /// <summary>
    /// A station whose upgrade starts stops its couriers (<c>04</c> section 8): the walking one gives its load back and stands at the station,
    /// the resting one gives its load back and keeps resting.
    /// </summary>
    [Fact]
    public void UpgradeBuilding_StationStartsAtOnce_StopsItsCouriers()
    {
        var resting = new Courier(2, 2, CourierState.Resting, 0, null, null, 0, [new CourierLoad(new ItemId("wood"), 3, 1)]);
        CityState city = StationCity(
            workers: 2,
            couriers: [Walking(2, 1, 1, 990, 1010, load: ("wood", 3, 4)), resting],
            routes: [RouteOf(2, 1, (3, [(true, "wood", "all")]), (1, [(false, "wood", "all")]))]);

        RulesAssert.Moved(CityRules.UpgradeBuilding(city, _coinsOnlyUpgrade, 2), spent: "gold=35", refunded: "");

        Assert.Equal(
            ["\ncourier 2/1 AtStation stamina=600 load:", "\ncourier 2/2 Resting stamina=0 load:"],
            city.Couriers.Select(CityDump.CourierLine));
        Assert.Equal("store: wood=25 needs:", CityDump.StoreLine(city, 3));
    }

    /// <summary>An upgrade that waits for its goods leaves the station working: its couriers keep walking and carrying.</summary>
    [Fact]
    public void UpgradeBuilding_StationWaitsForGoods_KeepsItsCouriersWalking()
    {
        CityState city = StationCity(workers: 1, couriers: [Walking(2, 1, 1, 990, 1010, load: ("wood", 3, 4))],
            routes: [RouteOf(2, 1, (3, [(true, "wood", "all")]), (1, [(false, "wood", "all")]))]);

        RulesAssert.Moved(CityRules.UpgradeBuilding(city, TestContent, 2), spent: "gold=35", refunded: "");

        Assert.Equal("\ncourier 2/1 Walking to=1 990..1010 stamina=600 load: wood@3=4", CityDump.CourierLine(city.Couriers[0]));
    }

    /// <summary>A courier's load counts in the city's total of its good, beside the stores (<c>04</c> section 16).</summary>
    [Fact]
    public void Total_CourierLoads_Count()
    {
        CityState city = StationCity(workers: 1, couriers: [AtStation(2, 1, load: ("wood", 3, 4))]);

        Assert.Equal(24, CityQueries.Total(city, TestContent, new ItemId("wood")));
    }

    /// <summary>The test city of the class summary with the post's workers, couriers and routes.</summary>
    private static CityState StationCity(int workers, Courier[] couriers, Route[]? routes = null) =>
        City(
            purse: 100,
            buildings:
            [
                Idle(1, "hut", 0, 4),
                Idle(2, "post", 2, 2, workers: workers),
                Idle(3, "lumber_camp", 0, 0, store: [("wood", 20)]),
            ],
            couriers: couriers,
            routes: routes);
}
