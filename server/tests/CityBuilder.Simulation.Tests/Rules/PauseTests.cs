using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// Pausing a building (CR-0008 amendment 2, D23–D25; <c>04</c> sections 4, 6, 8 and 19): <see cref="CityRules.SetPaused"/>, a paused producer
/// finishing its cycle, a paused courier station's couriers finishing their stop and going home, the open store, and the display helpers
/// <see cref="PauseDisplay"/> and <see cref="StoreDisplay"/>. Fixture numbers: a lumber camp makes 1 wood from 40 labor and holds 100 wood; a
/// post (courier station) level 1 has couriers carrying 5. The courier city: hut 1 at (0,4), post 2 at (2,2), lumber camp 3 at (0,0), depot 4
/// at (5,0), clock 1000. Walks: post → camp 16 s, camp → post 16 s, camp ↔ depot 20 s, depot → post 20 s.
/// </summary>
public sealed class PauseTests
{
    /// <summary>The wood item of the fixture.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>A paused producer finishes its running cycle (its wood comes out) and starts no new one until it is resumed.</summary>
    [Fact]
    public void SetPaused_Producer_FinishesTheCycleThenStartsNoNewOne()
    {
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0, workers: 1)]);
        Simulator.Advance(city, TestContent, 0);
        Assert.Equal(BuildingPhase.Producing, city.Buildings[1].Phase);

        RulesAssert.Moved(CityRules.SetPaused(city, TestContent, 2, true), spent: "", refunded: "");
        Assert.Null(StallReason.Of(city, TestContent, city.Buildings[1]));
        Simulator.Advance(city, TestContent, 200);

        Assert.Equal((BuildingPhase.Idle, 1L, 1), (city.Buildings[1].Phase, city.Buildings[1].GetStock(_wood), city.Buildings[1].AssignedWorkers));
        Assert.Equal("Paused", StallReason.Of(city, TestContent, city.Buildings[1])?.ToString());

        RulesAssert.Ok(CityRules.SetPaused(city, TestContent, 2, false));
        Simulator.Advance(city, TestContent, 0);
        Assert.Equal(BuildingPhase.Producing, city.Buildings[1].Phase);
    }

    /// <summary>Setting the value a building already has is Ok and changes nothing.</summary>
    [Fact]
    public void SetPaused_SameValue_ChangesNothing()
    {
        CityState city = CourierCity(routed: false);
        string before = CityDump.Describe(city);

        RulesAssert.Ok(CityRules.SetPaused(city, TestContent, 2, false));

        Assert.Equal(before, CityDump.Describe(city));
    }

    /// <summary>Only producers and courier stations are paused, never while constructing, and the building must exist.</summary>
    [Fact]
    public void SetPaused_Refusals()
    {
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, CourierCity(routed: false), city => CityRules.SetPaused(city, TestContent, 9, true));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, CourierCity(routed: false), city => CityRules.SetPaused(city, TestContent, 1, true));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, CourierCity(routed: false), city => CityRules.SetPaused(city, TestContent, 4, true));
        RulesAssert.Rejected(
            MutationErrorCode.BuildingBusy,
            City(clock: 1000, buildings: [Constructing(1, "lumber_camp", 0, 0, 990, 2000)]),
            city => CityRules.SetPaused(city, TestContent, 1, true));
    }

    /// <summary>A building may be paused while upgrading; the flag stays when the upgrade completes, so the producer still starts nothing.</summary>
    [Fact]
    public void SetPaused_WhileUpgrading_StaysThroughTheUpgrade()
    {
        CityState city = City(clock: 1000, buildings: [Idle(1, "hut", 0, 4), Upgrading(2, "lumber_camp", 0, 0, 1, 1, 990, 1010)]);

        RulesAssert.Ok(CityRules.SetPaused(city, TestContent, 2, true));
        Simulator.Advance(city, TestContent, 100);

        Assert.Equal((2, BuildingPhase.Idle, true), (city.Buildings[1].Level, city.Buildings[1].Phase, city.Buildings[1].Paused));
    }

    /// <summary>
    /// On a route: the courier walking to the camp when the post is paused does that stop (takes 5 wood), gives the load back and walks home
    /// (1016 + 16 = 1032, as the wait timer said); at home it stays, its route kept; resumed, it leaves for stop 1 again.
    /// </summary>
    [Fact]
    public void PausedStation_RouteCourier_FinishesTheStopGoesHomeEmptyAndWaits()
    {
        CityState city = CourierCity(routed: true);
        Simulator.Advance(city, TestContent, 0);
        Assert.Equal(CourierState.Walking, city.Couriers[0].State);

        RulesAssert.Ok(CityRules.SetPaused(city, TestContent, 2, true));
        Assert.Equal(1032L, PauseDisplay.LastCourierHomeAt(city, TestContent, 2));
        Assert.Equal("Paused", StallReason.Of(city, TestContent, city.Buildings[1])?.ToString());
        Simulator.Advance(city, TestContent, 16);

        Assert.Equal((CourierState.Returning, 0L, 20L), (city.Couriers[0].State, city.Couriers[0].Carried, city.Buildings[2].GetStock(_wood)));
        Assert.Equal(1032L, PauseDisplay.LastCourierHomeAt(city, TestContent, 2));
        Simulator.Advance(city, TestContent, 200);
        Assert.Equal(CourierState.AtStation, city.Couriers[0].State);
        Assert.Null(PauseDisplay.LastCourierHomeAt(city, TestContent, 2));
        Assert.True(city.TryGetRoute(2, 1, out _));

        RulesAssert.Ok(CityRules.SetPaused(city, TestContent, 2, false));
        Simulator.Advance(city, TestContent, 0);
        Assert.Equal((CourierState.Walking, 1), (city.Couriers[0].State, city.Couriers[0].ToStop));
    }

    /// <summary>
    /// On lines, walking to a source: at the camp it takes nothing and walks home; every line waits. Walking to a target: it puts its 5 wood at
    /// the depot, that line ends, and it walks home (1036 + 20 = 1056) while the other lines wait.
    /// </summary>
    [Fact]
    public void PausedStation_LineCourier_TakesNothingAtTheSource_PutsAtTheTarget_ThenGoesHome()
    {
        CityState atSource = CourierCity(routed: false);
        RulesAssert.Ok(TripRules.StartTrip(atSource, TestContent, 3, 4, _wood, Amount.Preset(new AmountPresetId("all"))));
        Simulator.Advance(atSource, TestContent, 0);
        RulesAssert.Ok(CityRules.SetPaused(atSource, TestContent, 2, true));
        Simulator.Advance(atSource, TestContent, 16);

        Assert.Equal((CourierState.Returning, 0L, 20L), (atSource.Couriers[0].State, atSource.Couriers[0].Carried, atSource.Buildings[2].GetStock(_wood)));
        Assert.Equal(4, atSource.DeliveryLines.Count);

        CityState atTarget = CourierCity(routed: false);
        RulesAssert.Ok(TripRules.StartTrip(atTarget, TestContent, 3, 4, _wood, Amount.Preset(new AmountPresetId("all"))));
        Simulator.Advance(atTarget, TestContent, 20);
        RulesAssert.Ok(CityRules.SetPaused(atTarget, TestContent, 2, true));
        Assert.Equal(1056L, PauseDisplay.LastCourierHomeAt(atTarget, TestContent, 2));
        Simulator.Advance(atTarget, TestContent, 16);

        Assert.Equal((CourierState.Returning, 5L), (atTarget.Couriers[0].State, atTarget.Buildings[3].GetStock(_wood)));
        Assert.Equal(3, atTarget.DeliveryLines.Count);
        Simulator.Advance(atTarget, TestContent, 500);
        Assert.Equal((CourierState.AtStation, 3), (atTarget.Couriers[0].State, atTarget.DeliveryLines.Count));
    }

    /// <summary>A paused building's store stays open: another station's courier still takes from a paused lumber camp (D25).</summary>
    [Fact]
    public void PausedProducer_StoreStaysOpenToCouriers()
    {
        CityState city = CourierCity(routed: true);
        RulesAssert.Ok(CityRules.SetPaused(city, TestContent, 3, true));

        Simulator.Advance(city, TestContent, 16);

        Assert.Equal((5L, 15L), (city.Couriers[0].Carried, city.Buildings[2].GetStock(_wood)));
    }

    /// <summary>The store gauge's fullness: the fullest good's stock against its limit, in permille, rounded down and at most 1000.</summary>
    /// <param name="wood">The camp's wood (its limit is 100).</param>
    /// <param name="permille">The expected fullness.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(75, 750)]
    [InlineData(76, 760)]
    [InlineData(100, 1000)]
    [InlineData(130, 1000)]
    public void StoreDisplay_FullestPermille(long wood, int permille)
    {
        CityState city = City(clock: 1000, buildings: [Idle(1, "lumber_camp", 0, 0, store: [("wood", wood)])]);

        Assert.Equal(permille, StoreDisplay.FullestPermille(city, TestContent, city.Buildings[0], out ItemId item));
        Assert.Equal(wood > 0 ? _wood : default, item);
    }

    /// <summary>The courier city with one courier at the post and 20 wood in the camp; with <paramref name="routed"/> a route camp (take all) → depot (put all).</summary>
    private static CityState CourierCity(bool routed) =>
        City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "post", 2, 2, workers: 1), Idle(3, "lumber_camp", 0, 0, store: [("wood", 20)]), Idle(4, "depot", 5, 0)],
            couriers: [AtStation(2, 1, 600)],
            routes: routed ? [RouteOf(2, 1, (3, [(true, "wood", "all")]), (4, [(false, "wood", "all")]))] : null);
}
