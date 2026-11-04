using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// Courier stamina and the absence start (<c>04</c> sections 2, 8, 11 and 19; CR-0008-T1 part H): only walks after the absence start spend
/// stamina, counted at the arrival; a courier that runs out does that stop's actions, walks home and rests with its load; re-energizing
/// fills it again; the report's <c>CourierSeconds</c> and <c>TiredCouriers</c>. Fixture numbers as <see cref="CourierRuleTests"/>: a post
/// level 1 carries 5 with 600 s of stamina; 16 s from the post to the camp, 20 s between the camp and the depot. The test city: hut 1 at
/// (0,4), post 2 at (2,2) with courier 2/1 walking from the post to the camp (1000..1016) on the route camp → depot, lumber camp 3 at (0,0)
/// with 20 wood, depot 4 at (5,0), clock 1000.
/// </summary>
public sealed class StaminaRuleTests
{
    /// <summary>The camp's wood to the depot: take all, put all.</summary>
    private static readonly (int, (bool, string, string)[])[] _campToDepot = [(3, [(true, "wood", "all")]), (4, [(false, "wood", "all")])];

    /// <summary>While the player is present (no absence start) walks spend nothing, however long: the report's courier seconds stay 0.</summary>
    [Fact]
    public void Advance_PlayerPresent_SpendsNoStamina()
    {
        CityState city = StaminaCity(stamina: 10);

        SettlementReport report = Simulator.Advance(city, TestContent, 200);

        Assert.Equal(10, city.Couriers[0].Stamina);
        Assert.Equal((0L, 0), (report.CourierSeconds, report.TiredCouriers.Count));
    }

    /// <summary>
    /// Absent from the start, each arrival spends its whole walk: 16 s at the camp, 20 s at the depot; the report keeps what the courier
    /// spent in all (36 s), the most of any courier (courier 2/2 at the station without a route spends nothing).
    /// </summary>
    [Fact]
    public void Advance_AbsentFromTheStart_SpendsEachWalkAtItsArrival()
    {
        CityState city = StaminaCity(stamina: 600, second: AtStation(2, 2));

        SettlementReport report = Simulator.Advance(city, TestContent, 36, absentFrom: 1000);

        Assert.Equal((564L, 600L), (city.Couriers[0].Stamina, city.Couriers[1].Stamina));
        Assert.Equal(36, report.CourierSeconds);
        Assert.Empty(report.TiredCouriers);
    }

    /// <summary>Only the part of a walk at or after the absence start counts: absent from 1010, the walk 1000..1016 spends 6 s.</summary>
    [Fact]
    public void Advance_AbsentDuringAWalk_SpendsOnlyItsAbsentPart()
    {
        CityState city = StaminaCity(stamina: 600);

        SettlementReport report = Simulator.Advance(city, TestContent, 16, absentFrom: 1010);

        Assert.Equal(594, city.Couriers[0].Stamina);
        Assert.Equal(6, report.CourierSeconds);
    }

    /// <summary>A walk under way at the end of the window has spent nothing yet: it spends at its arrival.</summary>
    [Fact]
    public void Advance_WalkStillUnderWay_HasSpentNothing()
    {
        CityState city = StaminaCity(stamina: 600);

        SettlementReport report = Simulator.Advance(city, TestContent, 15, absentFrom: 1000);

        Assert.Equal((600L, 0L), (city.Couriers[0].Stamina, report.CourierSeconds));
    }

    /// <summary>
    /// With 10 s of stamina left the arrival at the camp (16 s) spends the last of it: the courier still takes its 5 wood, then walks home
    /// (16 s) instead of on, and rests at the station with its load; the report names it tired at 1016 and its 10 s.
    /// </summary>
    [Fact]
    public void Advance_StaminaRunsOut_DoesTheStopThenWalksHomeAndRestsWithItsLoad()
    {
        CityState walkingHome = StaminaCity(stamina: 10);
        CityState home = StaminaCity(stamina: 10);

        Simulator.Advance(walkingHome, TestContent, 20, absentFrom: 1000);
        SettlementReport report = Simulator.Advance(home, TestContent, 40, absentFrom: 1000);

        Assert.Equal("\ncourier 2/1 Returning 1016..1032 stamina=0 load: wood@3=5", CityDump.CourierLine(walkingHome.Couriers[0]));
        Assert.Equal("\ncourier 2/1 Resting stamina=0 load: wood@3=5", CityDump.CourierLine(home.Couriers[0]));
        Assert.Equal("store: wood=15 needs:", CityDump.StoreLine(home, 3));
        TiredCourier tired = Assert.Single(report.TiredCouriers);
        Assert.Equal((2, 1, 1016L, 10L), (tired.StationId, tired.Slot, tired.At, report.CourierSeconds));
    }

    /// <summary>
    /// A tired courier walking home whose route is replaced stops working (<c>04</c> section 19): its 5 wood go back to the camp and, with no
    /// stamina, it rests at the station instead of standing ready there.
    /// </summary>
    [Fact]
    public void SetRoute_TiredCourierWalkingHome_GivesItsLoadBackAndRests()
    {
        CityState city = StaminaCity(stamina: 10);
        Simulator.Advance(city, TestContent, 20, absentFrom: 1000);

        Commands.SetRoute(city, 2, 1, Stops(_campToDepot));

        Assert.Equal("\ncourier 2/1 Resting stamina=0 load:", CityDump.CourierLine(city.Couriers[0]));
        Assert.Equal("store: wood=20 needs:", CityDump.StoreLine(city, 3));
    }

    /// <summary>A resting courier never leaves, however long the player stays away.</summary>
    [Fact]
    public void Advance_Resting_StaysAtTheStation()
    {
        CityState city = StaminaCity(stamina: 10);
        Simulator.Advance(city, TestContent, 40, absentFrom: 1000);

        Simulator.Advance(city, TestContent, 3000);

        Assert.Equal("\ncourier 2/1 Resting stamina=0 load: wood@3=5", CityDump.CourierLine(city.Couriers[0]));
    }

    /// <summary>
    /// Re-energizing a resting courier fills its stamina (the level's 600 s) and it leaves at once in the request's zero-length advance, with
    /// the load it kept: for the camp, 16 s away.
    /// </summary>
    [Fact]
    public void ReenergizeCouriers_Resting_FillsAndSendsItOffWithItsLoad()
    {
        CityState city = StaminaCity(stamina: 10);
        Simulator.Advance(city, TestContent, 40, absentFrom: 1000);

        Commands.ReenergizeCouriers(city);

        Assert.Equal("\ncourier 2/1 Walking to=1 1040..1056 stamina=600 load: wood@3=5", CityDump.CourierLine(city.Couriers[0]));
    }

    /// <summary>A courier re-energized on its way home stands at the station when it arrives, and leaves again from there at that second.</summary>
    [Fact]
    public void ReenergizeCouriers_Returning_LeavesAgainWhenItArrivesHome()
    {
        CityState city = StaminaCity(stamina: 10);
        Simulator.Advance(city, TestContent, 20, absentFrom: 1000);

        Commands.ReenergizeCouriers(city);
        Simulator.Advance(city, TestContent, 12);

        Assert.Equal("\ncourier 2/1 Walking to=1 1032..1048 stamina=600 load: wood@3=5", CityDump.CourierLine(city.Couriers[0]));
    }

    /// <summary>Re-energizing tops up a walking courier too and changes nothing else about it.</summary>
    [Fact]
    public void ReenergizeCouriers_Walking_OnlyFillsTheStamina()
    {
        CityState city = StaminaCity(stamina: 100);

        Commands.ReenergizeCouriers(city);

        Assert.Equal("\ncourier 2/1 Walking to=1 1000..1016 stamina=600 load:", CityDump.CourierLine(city.Couriers[0]));
    }

    /// <summary>The test city of the class summary with courier 2/1's stamina and, when given, a second courier (the post then has 2 workers).</summary>
    private static CityState StaminaCity(long stamina, Courier? second = null) =>
        City(
            buildings:
            [
                Idle(1, "hut", 0, 4),
                Idle(2, "post", 2, 2, workers: second is null ? 1 : 2),
                Idle(3, "lumber_camp", 0, 0, store: [("wood", 20)]),
                Idle(4, "depot", 5, 0),
            ],
            couriers: second is null ? [Walking(2, 1, 1, 1000, 1016, stamina)] : [Walking(2, 1, 1, 1000, 1016, stamina), second],
            routes: [RouteOf(2, 1, _campToDepot)]);
}
