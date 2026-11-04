using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>CourierRules.SetRoute</c> (<c>04</c> sections 8 and 19). Fixture numbers: a post (courier station) level 1 takes 2 couriers; routes of
/// up to 6 stops with up to 3 actions each; presets all and half, percentages after tallying; gold is the currency. The test city: hut 1 at
/// (0,4), post 2 at (2,2) with 1 worker (courier 2/1 at the station), lumber camp 3 at (0,0) with 20 wood, depot 4 at (5,0), clock 1000.
/// </summary>
public sealed class SetRouteTests
{
    /// <summary>The camp's wood to the depot and back: a valid two-stop route.</summary>
    private static readonly (int, (bool, string, string)[])[] _campToDepot =
        [(3, [(true, "wood", "all")]), (4, [(false, "wood", "all")])];

    /// <summary>A route of two stops is stored for the slot, and nothing is spent or refunded.</summary>
    [Fact]
    public void SetRoute_TwoStops_StoresTheSlotsRoute()
    {
        CityState city = RouteCity();

        RulesAssert.Moved(Set(city, 2, 1, Stops(_campToDepot)), spent: "", refunded: "");

        Assert.Contains("\nroute 2/1: 3[Take wood all] 4[Put wood all]", CityDump.Describe(city), StringComparison.Ordinal);
    }

    /// <summary>A slot without a courier (the post has one worker, the level takes two) still gets its route (CR-0008-T1 decision 48).</summary>
    [Fact]
    public void SetRoute_SlotWithoutACourier_StoresTheRoute()
    {
        CityState city = RouteCity();

        RulesAssert.Ok(Set(city, 2, 2, Stops(_campToDepot)));

        Assert.True(city.TryGetRoute(2, 2, out _));
        Assert.Single(city.Couriers);
    }

    /// <summary>No stops clears the slot's route; clearing a slot that has none changes nothing.</summary>
    [Fact]
    public void SetRoute_NoStops_ClearsTheRoute()
    {
        CityState city = RouteCity(withRoute: true);

        RulesAssert.Ok(Set(city, 2, 1, []));
        RulesAssert.Ok(Set(city, 2, 2, []));

        Assert.Empty(city.Routes);
    }

    /// <summary>A new route stops the slot's walking courier: its load goes back to the camp it came from, and it stands at its station.</summary>
    [Fact]
    public void SetRoute_SlotsCourierWalking_GivesItsLoadBackAndStandsItAtTheStation()
    {
        CityState city = RouteCity(withRoute: true, courier: Walking(2, 1, 2, 990, 1010, load: ("wood", 3, 4)));

        RulesAssert.Ok(Set(city, 2, 1, Stops(_campToDepot)));

        Assert.Equal("\ncourier 2/1 AtStation stamina=600 load:", CityDump.CourierLine(city.Couriers[0]));
        Assert.Equal("store: wood=24 needs:", CityDump.StoreLine(city, 3));
    }

    /// <summary>A resting courier gives its load back too, but keeps resting (<c>04</c> section 19).</summary>
    [Fact]
    public void SetRoute_SlotsCourierResting_GivesItsLoadBackAndKeepsResting()
    {
        var resting = new Courier(2, 1, CourierState.Resting, 0, null, null, 0, [new CourierLoad(new ItemId("wood"), 3, 2)]);
        CityState city = RouteCity(courier: resting);

        RulesAssert.Ok(Set(city, 2, 1, Stops(_campToDepot)));

        Assert.Equal("\ncourier 2/1 Resting stamina=0 load:", CityDump.CourierLine(city.Couriers[0]));
        Assert.Equal("store: wood=22 needs:", CityDump.StoreLine(city, 3));
    }

    /// <summary>A station the city doesn't have is <c>BUILDING_NOT_FOUND</c>; a building that is no courier station is <c>INVALID_ARGUMENT</c>.</summary>
    [Fact]
    public void SetRoute_NoCourierStation_IsRejected()
    {
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, RouteCity(), city => Set(city, 9, 1, Stops(_campToDepot)));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, RouteCity(), city => Set(city, 1, 1, Stops(_campToDepot)));
    }

    /// <summary>A slot outside 1 to the level's couriers (2) is <c>INVALID_ROUTE</c>.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void SetRoute_SlotOutsideTheLevel_IsInvalidRoute(int slot) =>
        RulesAssert.Rejected(MutationErrorCode.InvalidRoute, RouteCity(), city => Set(city, 2, slot, Stops(_campToDepot)));

    /// <summary>One stop, more than six, more than three actions at a stop, and two consecutive stops at one building are <c>INVALID_ROUTE</c>.</summary>
    [Fact]
    public void SetRoute_ShapeTheContentForbids_IsInvalidRoute()
    {
        (bool, string, string)[] take = [(true, "wood", "all")];
        (int, (bool, string, string)[])[] seven = Enumerable.Range(0, 7).Select(index => (index % 2 == 0 ? 3 : 4, take)).ToArray();
        (int, (bool, string, string)[])[][] shapes =
        [
            [(3, take)],
            seven,
            [(3, [(true, "wood", "all"), (true, "wood", "half"), (true, "boards", "all"), (true, "boards", "half")]), (4, take)],
            [(3, take), (3, take), (4, take)],
            [(3, take), (4, take), (3, take)],
        ];

        foreach ((int, (bool, string, string)[])[] shape in shapes)
        {
            RulesAssert.Rejected(MutationErrorCode.InvalidRoute, RouteCity(), city => Set(city, 2, 1, Stops(shape)));
        }
    }

    /// <summary>A stop at a building the city doesn't have is <c>BUILDING_NOT_FOUND</c>, after the route's shape is checked.</summary>
    [Fact]
    public void SetRoute_StopAtAMissingBuilding_IsBuildingNotFoundAfterTheShape()
    {
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, RouteCity(), city => Set(city, 2, 1, Stops((3, [(true, "wood", "all")]), (99, [(false, "wood", "all")]))));
        RulesAssert.Rejected(MutationErrorCode.InvalidRoute, RouteCity(), city => Set(city, 2, 1, Stops((99, [(true, "wood", "all")]), (99, [(false, "wood", "all")]))));
    }

    /// <summary>An unknown good is <c>UNKNOWN_ITEM</c>, the currency <c>INVALID_ARGUMENT</c>, both before any amount is judged.</summary>
    [Fact]
    public void SetRoute_GoodsItCantCarry_AreRejectedBeforeTheAmounts()
    {
        RulesAssert.Rejected(MutationErrorCode.UnknownItem, RouteCity(), city => Set(city, 2, 1, Stops((3, [(true, "wood", "50%")]), (4, [(false, "stone", "all")]))));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, RouteCity(), city => Set(city, 2, 1, Stops((3, [(true, "wood", "50%")]), (4, [(false, "gold", "all")]))));
    }

    /// <summary>An unknown preset is <c>INVALID_ARGUMENT</c>; a percentage before tallying is <c>AMOUNT_MODE_LOCKED</c>, and after it is allowed.</summary>
    [Fact]
    public void SetRoute_Amounts_FollowTheUnlockedModes()
    {
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, RouteCity(), city => Set(city, 2, 1, Stops((3, [(true, "wood", "most")]), (4, [(false, "wood", "all")]))));
        RulesAssert.Rejected(MutationErrorCode.AmountModeLocked, RouteCity(), city => Set(city, 2, 1, Stops((3, [(true, "wood", "50%")]), (4, [(false, "wood", "all")]))));

        CityState tallied = RouteCity(research: ["carpentry", "tallying"]);
        RulesAssert.Ok(Set(tallied, 2, 1, Stops((3, [(true, "wood", "50%")]), (4, [(false, "wood", "all")]))));
    }

    /// <summary>Sets a route on the test content.</summary>
    private static MutationResult Set(CityState city, int stationId, int slot, RouteStop[] stops) =>
        CourierRules.SetRoute(city, TestContent, stationId, slot, stops);

    /// <summary>The test city of the class summary, with the given route of slot 1, courier and completed research.</summary>
    private static CityState RouteCity(bool withRoute = false, Courier? courier = null, string[]? research = null) =>
        City(
            buildings:
            [
                Idle(1, "hut", 0, 4),
                Idle(2, "post", 2, 2, workers: 1),
                Idle(3, "lumber_camp", 0, 0, store: [("wood", 20)]),
                Idle(4, "depot", 5, 0),
            ],
            completedResearch: research,
            couriers: [courier ?? AtStation(2, 1)],
            routes: withRoute ? [RouteOf(2, 1, _campToDepot)] : null);
}
