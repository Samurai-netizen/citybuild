using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>TripRules.StartTrip</c> (<c>04</c> sections 8, 17, 18 and 19). Fixture numbers: a hut provides 3 workers; a lumber camp's store holds
/// wood; a depot (warehouse) holds wood and boards; trips carry at most 6; presets all (100 %) and half (50 %); percentages after tallying;
/// walking 4 000 ms per ground tile, so a walk of five free tiles takes 20 s (18 s with boots, −100 ‰). The test city: hut 1 at (0,4),
/// lumber camp 2 at (0,0), depot 3 at (5,0), clock 1000.
/// </summary>
public sealed class StartTripTests
{
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

    /// <summary>A trip takes its load out of the source's store at once, keeps a worker busy, and arrives after the walk: 6 wood, 20 s.</summary>
    [Fact]
    public void StartTrip_FromACampToADepot_TakesTheGoodsAndSendsAWorker()
    {
        CityState city = TripCity(wood: 50);

        RulesAssert.Moved(Start(city, 2, 3, "wood", "all"), spent: "", refunded: "");

        Assert.Equal("store: wood=44 needs:", CityDump.StoreLine(city, 2));
        Assert.EndsWith("\nnext trip=2\ntrip 1 2->3 wood=6 1000..1020", CityDump.Describe(city));
        Assert.Equal(2, CityQueries.FreeWorkers(city, TestContent));
        Assert.Equal(50, CityQueries.Total(city, TestContent, new ItemId("wood")));
    }

    /// <summary>The walk uses the path rule with roads (a level-2 path: 8 s instead of 20 s) and the walk bonus (boots: 18 s).</summary>
    [Fact]
    public void StartTrip_RoadsAndWalkBonus_ShortenTheWalk()
    {
        CityState onRoads = TripCity(wood: 50, roads: [new RoadTile(1, 0, 2), new RoadTile(2, 0, 2), new RoadTile(3, 0, 2), new RoadTile(4, 0, 2)]);
        CityState withBoots = TripCity(wood: 50, completedResearch: ["boots"]);

        RulesAssert.Ok(Start(onRoads, 2, 3, "wood", "all"));
        RulesAssert.Ok(Start(withBoots, 2, 3, "wood", "all"));

        // 4 000 + 1 000, 3 × (1 000 + 1 000), 1 000 + 4 000 = 16 000 → 8 s; 40 000 less 100 ‰ = 36 000 → 18 s.
        Assert.Equal((1008L, 1018L), (onRoads.Trips[0].ArrivesAt, withBoots.Trips[0].ArrivesAt));
    }

    /// <summary>A preset gives its share of the stock rounded down, at least 1 of a stock of 1 or more, at most the stock and the carry limit.</summary>
    /// <param name="stock">The camp's wood.</param>
    /// <param name="amount">The preset.</param>
    /// <param name="carried">The expected load.</param>
    [Theory]
    [InlineData(50, "all", 6)] // the carry limit
    [InlineData(3, "all", 3)] // the stock
    [InlineData(9, "half", 4)] // 4.5 rounded down
    [InlineData(1, "half", 1)] // 0.5, at least 1
    public void StartTrip_Preset_CarriesItsShareOfTheStock(long stock, string amount, long carried)
    {
        CityState city = TripCity(wood: stock);

        RulesAssert.Ok(Start(city, 2, 3, "wood", amount));

        Assert.Equal(carried, city.Trips[0].Quantity);
        Assert.Equal(stock - carried, city.Buildings[1].GetStock(new ItemId("wood")));
    }

    /// <summary>After tallying a percentage works like a preset: 10 % of 50 is 5, 1 % of 50 is at least 1, 100 % of 5 is 5.</summary>
    /// <param name="stock">The camp's wood.</param>
    /// <param name="amount">The percentage.</param>
    /// <param name="carried">The expected load.</param>
    [Theory]
    [InlineData(50, "10%", 5)]
    [InlineData(50, "1%", 1)]
    [InlineData(5, "100%", 5)]
    public void StartTrip_PercentageAfterItsResearch_CarriesItsShareOfTheStock(long stock, string amount, long carried)
    {
        CityState city = TripCity(wood: stock, completedResearch: ["carpentry", "tallying"]);

        RulesAssert.Ok(Start(city, 2, 3, "wood", amount));

        Assert.Equal(carried, city.Trips[0].Quantity);
    }

    /// <summary>An exact number, once unlocked, gives itself, then at most the stock and the carry limit.</summary>
    /// <param name="stock">The camp's wood.</param>
    /// <param name="amount">The exact number.</param>
    /// <param name="carried">The expected load.</param>
    [Theory]
    [InlineData(50, "4", 4)]
    [InlineData(50, "9", 6)] // the carry limit
    [InlineData(3, "5", 3)] // the stock
    public void StartTrip_ExactNumberAfterItsResearch_CarriesThatNumberWithinTheLimits(long stock, string amount, long carried)
    {
        CityState city = TripCity(wood: stock, completedResearch: ["counting"]);

        RulesAssert.Ok(TripRules.StartTrip(city, _exactContent, 2, 3, new ItemId("wood"), Parse(amount)));

        Assert.Equal(carried, city.Trips[0].Quantity);
    }

    /// <summary>A target accepts a good its waiting project needs, also without a store: a hut's upgrade, a site, the research at the school.</summary>
    [Fact]
    public void StartTrip_GoodAWaitingProjectNeeds_IsAccepted()
    {
        CityState toHut = City(buildings: [Idle(1, "hut", 0, 4, needs: [("wood", 5)]), Idle(2, "lumber_camp", 0, 0, store: [("wood", 9)])]);
        CityState toSite = TripCity(wood: 9, extra: [Site(4, "workshop", 3, 3, needs: [("wood", 10)])]);
        CityState toSchool = City(
            completedResearch: ["carpentry"],
            buildings: [Idle(1, "hut", 0, 4), Idle(3, "depot", 5, 0, store: [("boards", 9)]), Idle(9, "school", 2, 2)],
            activeResearch: new ActiveResearch(new ResearchId("better_saws"), null, null, [new(new ItemId("boards"), 5)]));

        RulesAssert.Ok(Start(toHut, 2, 1, "wood", "all"));
        RulesAssert.Ok(Start(toSite, 2, 4, "wood", "all"));
        RulesAssert.Ok(Start(toSchool, 3, 9, "boards", "all"));
    }

    /// <summary>A trip to its own source is <see cref="MutationErrorCode.InvalidArgument"/>.</summary>
    [Fact]
    public void StartTrip_SourceIsTheTarget_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, TripCity(wood: 50), city => Start(city, 2, 2, "wood", "all"));

    /// <summary>A source or target the city doesn't have is <see cref="MutationErrorCode.BuildingNotFound"/>.</summary>
    [Fact]
    public void StartTrip_UnknownBuilding_IsRejected()
    {
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, TripCity(wood: 50), city => Start(city, 7, 3, "wood", "all"));
        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, TripCity(wood: 50), city => Start(city, 2, 7, "wood", "all"));
    }

    /// <summary>An item the content doesn't have is <see cref="MutationErrorCode.UnknownItem"/>.</summary>
    [Fact]
    public void StartTrip_UnknownItem_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.UnknownItem, TripCity(wood: 50), city => Start(city, 2, 3, "stone", "all"));

    /// <summary>The currency is never carried, and a preset the content lacks is no amount: both <see cref="MutationErrorCode.InvalidArgument"/>.</summary>
    [Fact]
    public void StartTrip_CurrencyOrUnknownPreset_IsRejected()
    {
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, TripCity(wood: 50), city => Start(city, 2, 3, "gold", "all"));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, TripCity(wood: 50), city => Start(city, 2, 3, "wood", "quarter"));
    }

    /// <summary>A percentage before tallying, and an exact number the fixture never unlocks, are <see cref="MutationErrorCode.AmountModeLocked"/>.</summary>
    [Fact]
    public void StartTrip_AmountModeNotResearched_IsRejected()
    {
        RulesAssert.Rejected(MutationErrorCode.AmountModeLocked, TripCity(wood: 50), city => Start(city, 2, 3, "wood", "10%"));
        RulesAssert.Rejected(MutationErrorCode.AmountModeLocked, TripCity(wood: 50, completedResearch: ["carpentry", "tallying"]), city => Start(city, 2, 3, "wood", "4"));
    }

    /// <summary>
    /// A target that neither needs the good for a project nor holds it in its store is <see cref="MutationErrorCode.ItemNotAccepted"/>: a hut,
    /// a camp for boards, the school without a research waiting for them.
    /// </summary>
    [Fact]
    public void StartTrip_TargetDoesNotTakeTheGood_IsRejected()
    {
        CityState withBoards = City(buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0), Idle(3, "depot", 5, 0, store: [("boards", 9)]), Idle(9, "school", 2, 2)]);

        RulesAssert.Rejected(MutationErrorCode.ItemNotAccepted, TripCity(wood: 50), city => Start(city, 2, 1, "wood", "all"));
        RulesAssert.Rejected(MutationErrorCode.ItemNotAccepted, withBoards, city => Start(city, 3, 2, "boards", "all"));
        RulesAssert.Rejected(MutationErrorCode.ItemNotAccepted, withBoards, city => Start(city, 3, 9, "boards", "all"));
    }

    /// <summary>Without a free worker (all 3 assigned or on trips) the trip is <see cref="MutationErrorCode.InsufficientWorkers"/>.</summary>
    [Fact]
    public void StartTrip_NoFreeWorker_IsRejected()
    {
        CityState city = City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0, workers: 2, store: [("wood", 50)]), Idle(3, "depot", 5, 0)],
            trips: [Trip(1, 2, 3, "wood", 6, 990, 1010)]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientWorkers, city, city => Start(city, 2, 3, "wood", "all"));
    }

    /// <summary>A source with none of the good gives nothing to carry: <see cref="MutationErrorCode.NothingToCarry"/>.</summary>
    [Fact]
    public void StartTrip_SourceHasNoneOfTheGood_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.NothingToCarry, TripCity(wood: 0), city => Start(city, 2, 3, "wood", "all"));

    /// <summary>
    /// With several checks failing the first in spec order wins: building, item, currency, amount mode, acceptance, workers, then the load.
    /// </summary>
    [Fact]
    public void StartTrip_SeveralChecksFail_ReportsTheFirstInSpecOrder()
    {
        CityState busy = City(buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0, workers: 2), Idle(3, "depot", 5, 0)], trips: [Trip(1, 2, 3, "wood", 6, 990, 1010)]);

        RulesAssert.Rejected(MutationErrorCode.BuildingNotFound, TripCity(wood: 0), city => Start(city, 7, 3, "stone", "all"));
        RulesAssert.Rejected(MutationErrorCode.UnknownItem, TripCity(wood: 0), city => Start(city, 2, 3, "stone", "10%"));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, TripCity(wood: 0), city => Start(city, 2, 3, "gold", "10%"));
        RulesAssert.Rejected(MutationErrorCode.AmountModeLocked, TripCity(wood: 0), city => Start(city, 2, 1, "wood", "10%"));
        RulesAssert.Rejected(MutationErrorCode.ItemNotAccepted, busy, city => Start(city, 2, 1, "wood", "all"));
        RulesAssert.Rejected(MutationErrorCode.InsufficientWorkers, busy, city => Start(city, 2, 3, "wood", "all"));
    }

    /// <summary>A null city, content or amount is a programmer error and throws.</summary>
    [Fact]
    public void StartTrip_NullArguments_Throw()
    {
        Amount all = Parse("all");
        Assert.Throws<ArgumentNullException>(() => TripRules.StartTrip(null!, TestContent, 2, 3, new ItemId("wood"), all));
        Assert.Throws<ArgumentNullException>(() => TripRules.StartTrip(TripCity(wood: 1), null!, 2, 3, new ItemId("wood"), all));
        Assert.Throws<ArgumentNullException>(() => TripRules.StartTrip(TripCity(wood: 1), TestContent, 2, 3, new ItemId("wood"), null!));
    }

    /// <summary>The test city: hut 1 at (0,4), lumber camp 2 at (0,0) with <paramref name="wood"/>, depot 3 at (5,0), and the given extras, research and roads.</summary>
    private static CityState TripCity(long wood, string[]? completedResearch = null, RoadTile[]? roads = null, Building[]? extra = null) =>
        City(
            completedResearch: completedResearch,
            roads: roads,
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0, store: [("wood", wood)]), Idle(3, "depot", 5, 0), .. extra ?? []]);

    /// <summary>Tries to start a trip with the fixture content and an amount in the API's text form.</summary>
    private static MutationResult Start(CityState city, int source, int target, string item, string amount) =>
        TripRules.StartTrip(city, TestContent, source, target, new ItemId(item), Parse(amount));

    /// <summary>The amount of the API's text form; the test's text is always one.</summary>
    private static Amount Parse(string text) => Amount.TryParse(text, out Amount? amount) ? amount : throw new ArgumentException(text);
}
