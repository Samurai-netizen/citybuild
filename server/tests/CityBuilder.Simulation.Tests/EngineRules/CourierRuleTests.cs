using System.Globalization;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using CityBuilder.Simulation.Tests.Rules;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// Couriers in the advance (<c>04</c> sections 6, 16 and 19): leaving the station, the loop over the route, a stop's takes and puts with
/// their amounts and the carry limit, puts to projects, a station that upgrades, a long advance. Fixture
/// numbers: a post (courier station) level 1 carries 5 with 600 s of stamina; walking 4 000 ms per ground tile, so 16 s from the post to the
/// camp (four tiles) and 20 s between the camp and the depot (five). The test city: hut 1 at (0,4) (3
/// workers), post 2 at (2,2) with 1 worker (courier 2/1), lumber camp 3 at (0,0) with 20 wood and no workers, depot 4 at (5,0), clock 1000.
/// </summary>
public sealed class CourierRuleTests
{
    /// <summary>The good of most routes here.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>The camp's wood to the depot: take all, put all.</summary>
    private static readonly (int, (bool, string, string)[])[] _campToDepot = [(3, [(true, "wood", "all")]), (4, [(false, "wood", "all")])];

    /// <summary>The fixture content with a research that unlocks exact amounts (<c>counting</c>), which the fixture otherwise lacks.</summary>
    private static readonly GameContent _exactContent = ContentAssert.Valid(ContentFixture.ValidWith(data => data.Research!.Add(new ResearchData
    {
        Id = "counting",
        Name = "Counting",
        Era = 1,
        Cost = new() { ["gold"] = 5 },
        DurationSeconds = 10,
        Effects = [new ResearchEffectData { Type = "unlockAmountMode", Mode = "exact" }],
    })));

    /// <summary>A courier whose slot gets a route leaves in the request's zero-length advance, for the first stop: the camp, 16 s away.</summary>
    [Fact]
    public void Courier_RouteSet_LeavesForTheFirstStopAtOnce()
    {
        CityState city = CourierCity();

        Commands.SetRoute(city, 2, 1, Stops(_campToDepot));

        Assert.Equal("\ncourier 2/1 Walking to=1 1000..1016 stamina=600 load:", CourierOf(city));
    }

    /// <summary>
    /// The loop: at the camp it takes 5 of the 20 wood (its carry limit), walks 20 s to the depot, puts them there (<c>moved</c>), walks
    /// back to the camp (after the last stop, the first) and takes 5 more.
    /// </summary>
    [Fact]
    public void Courier_Route_LoopsTakingAndPutting()
    {
        CityState city = CourierCity();
        Commands.SetRoute(city, 2, 1, Stops(_campToDepot));

        SettlementReport report = Simulator.Advance(city, TestContent, 56);

        Assert.Equal("\ncourier 2/1 Walking to=2 1056..1076 stamina=600 load: wood@3=5", CourierOf(city));
        Assert.Equal("store: wood=10 needs:", CityDump.StoreLine(city, 3));
        Assert.Equal("store: wood=5 needs:", CityDump.StoreLine(city, 4));
        Assert.Equal(5, report.Moved[_wood]);
        Assert.Empty(report.Delivered);
    }

    /// <summary>A take of half rounds down (half of 7 is 3), at least 1 of a stock of 1, and nothing of an empty store.</summary>
    [Theory]
    [InlineData(7, 3)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Take_Half_RoundsDownButTakesAtLeastOne(long stock, long taken)
    {
        CityState city = CourierCity(campWood: stock);
        Commands.SetRoute(city, 2, 1, Stops((3, [(true, "wood", "half")]), (4, [(false, "wood", "all")])));

        Simulator.Advance(city, TestContent, 16);

        Assert.Equal(taken, city.Couriers[0].CarriedOf(_wood));
        Assert.Equal(stock - taken, city.Buildings[2].GetStock(_wood));
    }

    /// <summary>A take never goes past the carry limit: carrying 4 boards already, the courier takes 1 wood of the camp's 20.</summary>
    [Fact]
    public void Take_WithALoad_StopsAtTheCarryLimit()
    {
        // The route is already set: setting it now would give the boards back (a new route stops the courier, 04 section 19).
        CityState city = CourierCity(couriers: [AtStation(2, 1, load: ("boards", 4, 4))], routes: [RouteOf(2, 1, _campToDepot)]);

        Simulator.Advance(city, TestContent, 0);
        Simulator.Advance(city, TestContent, 16);

        Assert.Equal("\ncourier 2/1 Walking to=2 1016..1036 stamina=600 load: boards@4=4 wood@3=1", CourierOf(city));
    }

    /// <summary>An exact amount takes that many (once its mode is researched), and never more than the stock.</summary>
    [Theory]
    [InlineData(20, 3, 3)]
    [InlineData(2, 3, 2)]
    public void Take_Exact_TakesThatManyAtMostTheStock(long stock, int exact, long taken)
    {
        CityState city = CourierCity(campWood: stock, research: ["counting"]);
        RulesAssert.Ok(CourierRules.SetRoute(city, _exactContent, 2, 1, Stops((3, [(true, "wood", exact.ToString(CultureInfo.InvariantCulture))]), (4, [(false, "wood", "all")]))));

        Simulator.Advance(city, _exactContent, 16);

        Assert.Equal(taken, city.Couriers[0].CarriedOf(_wood));
    }

    /// <summary>
    /// A put fills the building's waiting project first: a workshop site needing 3 wood gets 3 of the 5 (<c>delivered</c>) and starts
    /// building, and the other 2 go into its store (<c>moved</c>).
    /// </summary>
    [Fact]
    public void Put_ToAConstructionSite_FillsItsNeedsFirstThenTheStore()
    {
        CityState city = CourierCity(target: Site(4, "workshop", 5, 0, needs: [("wood", 3)]));
        Commands.SetRoute(city, 2, 1, Stops(_campToDepot));

        SettlementReport report = Simulator.Advance(city, TestContent, 36);

        Assert.Equal((3L, 2L), (report.Delivered[_wood], report.Moved[_wood]));
        Assert.Equal("building 4 workshop (5,0) L1 w=0 Constructing 1036..1056 target=0 labor=0/0@0", CityDump.BuildingLine(city, 4));
        Assert.Equal("store: wood=2 needs:", CityDump.StoreLine(city, 4));
        Assert.Equal(0, city.Couriers[0].Carried);
    }

    /// <summary>A put into a full store takes nothing: the courier keeps its load and walks on to the next stop.</summary>
    [Fact]
    public void Put_IntoAFullStore_KeepsTheLoadAndWalksOn()
    {
        CityState city = CourierCity(target: Idle(4, "depot", 5, 0, store: [("wood", 100)]));
        Commands.SetRoute(city, 2, 1, Stops(_campToDepot));

        Simulator.Advance(city, TestContent, 36);

        Assert.Equal("\ncourier 2/1 Walking to=1 1036..1056 stamina=600 load: wood@3=5", CourierOf(city));
        Assert.Equal("store: wood=100 needs:", CityDump.StoreLine(city, 4));
    }

    /// <summary>
    /// A put that delivers its own station's last upgrade goods starts the upgrade, which stops the courier: it stands at the station, the
    /// boards it didn't hand over go back to the depot, and it doesn't leave while the station upgrades.
    /// </summary>
    [Fact]
    public void Put_LastGoodsOfItsOwnStationsUpgrade_StopsTheCourier()
    {
        CityState city = City(
            purse: 100,
            buildings:
            [
                Idle(1, "hut", 0, 4),
                Idle(2, "post", 2, 2, workers: 1, needs: [("boards", 4)]),
                Idle(4, "depot", 5, 0, store: [("boards", 10)]),
            ],
            couriers: [AtStation(2, 1)]);
        Commands.SetRoute(city, 2, 1, Stops((4, [(true, "boards", "all")]), (2, [(false, "boards", "all")])));

        // The post to the depot is 3 + 2 tiles (20 s); back the same.
        Simulator.Advance(city, TestContent, 40);

        Assert.Equal("building 2 post (2,2) L1 w=1 Upgrading 1040..1050 target=2 labor=0/0@0", CityDump.BuildingLine(city, 2));
        Assert.Equal("\ncourier 2/1 AtStation stamina=600 load:", CourierOf(city));
        Assert.Equal("store: boards=6 needs:", CityDump.StoreLine(city, 4));
    }

    /// <summary>A courier at a station that is upgrading doesn't leave; once the upgrade completes it does, at that second.</summary>
    [Fact]
    public void Courier_StationUpgrading_LeavesWhenTheUpgradeCompletes()
    {
        CityState city = CourierCity(station: Upgrading(2, "post", 2, 2, 1, 1, 990, 1010), routes: [RouteOf(2, 1, _campToDepot)]);

        Simulator.Advance(city, TestContent, 0);
        Assert.Equal("\ncourier 2/1 AtStation stamina=600 load:", CourierOf(city));

        Simulator.Advance(city, TestContent, 10);
        Assert.Equal("\ncourier 2/1 Walking to=1 1010..1026 stamina=600 load:", CourierOf(city));
    }

    /// <summary>
    /// A courier keeps working through a long advance while the player is present (no offline cap since part H, <c>04</c> section 2): after
    /// 4 000 s it has carried all 20 wood to the depot and spent no stamina.
    /// </summary>
    [Fact]
    public void Courier_LongAdvanceWhilePresent_KeepsWorkingAndSpendsNoStamina()
    {
        CityState city = CourierCity();
        Commands.SetRoute(city, 2, 1, Stops(_campToDepot));

        SettlementReport report = Simulator.Advance(city, TestContent, 4000);

        Assert.Equal((0L, 20L), (city.Buildings[2].GetStock(_wood), city.Buildings[3].GetStock(_wood)));
        Assert.Equal(600, city.Couriers[0].Stamina);
        Assert.Equal(0, report.CourierSeconds);
    }

    /// <summary>
    /// Two couriers that arrive at the camp in the same second act in slot order (<c>04</c> section 6): slot 1 takes the last 5 wood and
    /// slot 2 finds none.
    /// </summary>
    [Fact]
    public void Couriers_ArrivingTogether_ActInSlotOrder()
    {
        CityState city = CourierCity(
            campWood: 5,
            workers: 2,
            couriers: [AtStation(2, 1), AtStation(2, 2)],
            routes: [RouteOf(2, 1, _campToDepot), RouteOf(2, 2, _campToDepot)]);

        Simulator.Advance(city, TestContent, 0);
        Simulator.Advance(city, TestContent, 16);

        Assert.Equal((5L, 0L), (city.Couriers[0].CarriedOf(_wood), city.Couriers[1].CarriedOf(_wood)));
    }

    /// <summary>One advance gives the same city as one-second steps over a courier's loop (split invariance, <c>04</c> section 10).</summary>
    [Fact]
    public void Courier_Loop_IsTheSameInOneAdvanceAsInSecondSteps()
    {
        CityState whole = CourierCity();
        CityState steps = CourierCity();
        Commands.SetRoute(whole, 2, 1, Stops(_campToDepot));
        Commands.SetRoute(steps, 2, 1, Stops(_campToDepot));

        Simulator.Advance(whole, TestContent, 200);
        for (int second = 0; second < 200; second++)
        {
            Simulator.Advance(steps, TestContent, 1);
        }

        Assert.Equal(CityDump.Describe(whole), CityDump.Describe(steps));
    }

    /// <summary>The courier line of the city's first courier.</summary>
    private static string CourierOf(CityState city) => CityDump.CourierLine(city.Couriers[0]);

    /// <summary>The test city of the class summary, with what a test changes.</summary>
    private static CityState CourierCity(
        long campWood = 20,
        Building? target = null,
        Building? station = null,
        int workers = 1,
        Courier[]? couriers = null,
        Route[]? routes = null,
        string[]? research = null) =>
        City(
            buildings:
            [
                Idle(1, "hut", 0, 4),
                station ?? Idle(2, "post", 2, 2, workers: workers),
                Idle(3, "lumber_camp", 0, 0, store: campWood > 0 ? [("wood", campWood)] : null),
                target ?? Idle(4, "depot", 5, 0),
            ],
            completedResearch: research,
            couriers: couriers ?? [AtStation(2, 1)],
            routes: routes);
}
