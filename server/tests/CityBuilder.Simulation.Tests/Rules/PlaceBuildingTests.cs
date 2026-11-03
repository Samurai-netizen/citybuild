using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>CityRules.PlaceBuilding</c> (<c>04</c> sections 8 and 16). Fixture numbers: the grid is 6 × 5 with two builders; gold is the currency; a
/// hut costs 10 gold and builds in 5 s; a lumber camp costs 20 gold and builds in 10 s; a workshop (unlocked by carpentry) costs 40 gold and
/// 10 wood and builds in 20 s, so it starts as a construction site waiting for the wood; the school (the science building) is one per city;
/// a manor is a house of era 2. Only coins are paid at once (CR-0008 D1).
/// </summary>
public sealed class PlaceBuildingTests
{
    /// <summary>
    /// A placement whose cost is coins only pays them from the purse and starts construction on the tile from the clock, with the next id,
    /// level 1, no workers and an empty store.
    /// </summary>
    [Fact]
    public void PlaceBuilding_CostOfCoinsOnly_PaysAndStartsConstructionWithTheNextId()
    {
        CityState city = City(clock: 1000, purse: 50, buildings: [Idle(4, "hut", 0, 0)]);

        RulesAssert.Ok(Place(city, "lumber_camp", 2, 3));

        Assert.Equal(
            """
            clock=1000 era=1 next=6 purse=30
            building 4 hut (0,0) L1 w=0 Idle 0..null target=0 labor=0/0@0 store: needs:
            building 5 lumber_camp (2,3) L1 w=0 Constructing 1000..1010 target=0 labor=0/0@0 store: needs:
            completed:
            active: none
            """.ReplaceLineEndings("\n"),
            CityDump.Describe(city));
    }

    /// <summary>
    /// A cost with goods pays only its coins: the result names the coins as spent (for the ledger) and nothing as refunded, and the goods are
    /// the site's needs, never taken from any store (CR-0008 D1).
    /// </summary>
    [Fact]
    public void PlaceBuilding_CostWithGoods_SpendsOnlyTheCoinsAndLeavesTheStoresAlone()
    {
        CityState city = City(purse: 50, buildings: [Idle(1, "depot", 5, 4, store: [("wood", 30)])], completedResearch: ["carpentry"]);

        RulesAssert.Moved(Place(city, "workshop", 0, 0), spent: "gold=40", refunded: "");

        Assert.Equal((10L, 30L), (city.Purse, city.Buildings[0].GetStock(new ItemId("wood"))));
    }

    /// <summary>A purse of exactly the coins is enough and is empty afterwards; the goods don't need to be anywhere yet.</summary>
    [Fact]
    public void PlaceBuilding_PurseExactlyTheCoins_LeavesItEmpty()
    {
        CityState city = City(purse: 40, completedResearch: ["carpentry"]);

        RulesAssert.Ok(Place(city, "workshop", 0, 0));

        Assert.Equal(0, city.Purse);
    }

    /// <summary>
    /// A type unlocked by completed research can be placed; with goods in its cost it is a construction site: constructing without a start or
    /// an end, waiting for the wood, its builder slot taken (<c>04</c> section 16).
    /// </summary>
    [Fact]
    public void PlaceBuilding_CostWithGoods_StartsAConstructionSite()
    {
        CityState city = City(purse: 40, completedResearch: ["carpentry"]);

        RulesAssert.Ok(Place(city, "workshop", 5, 4));

        Building workshop = Assert.Single(city.Buildings);
        Assert.Equal(
            ("workshop", BuildingPhase.Constructing, 0L, (long?)null, true),
            (workshop.TypeId.Value, workshop.Phase, workshop.PhaseStartedAt, workshop.PhaseEndsAt, workshop.IsConstructionSite));
        Assert.Equal([("wood", 10L)], workshop.Needs.Select(need => (need.Key.Value, need.Value)));
        Assert.Equal(1, CityQueries.BuildersBusy(city));
    }

    /// <summary>A second science building is <see cref="MutationErrorCode.BuildingLimit"/>, also while the first is still constructing (content rule 10).</summary>
    [Fact]
    public void PlaceBuilding_SecondScienceBuilding_IsRejectedAsBuildingLimit()
    {
        CityState city = City(purse: 100, buildings: [Constructing(1, "school", 0, 0, startedAt: 998, endsAt: 1003)]);

        RulesAssert.Rejected(MutationErrorCode.BuildingLimit, city, city => Place(city, "school", 1, 0));
        RulesAssert.Ok(Place(City(purse: 30), "school", 1, 0));
    }

    /// <summary>A type of a later era than the city's is rejected as <see cref="MutationErrorCode.BuildingLocked"/> (OD-10).</summary>
    [Fact]
    public void PlaceBuilding_TypeOfALaterEra_IsRejectedAsLocked() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, City(era: 1, purse: 100), city => Place(city, "manor", 0, 0));

    /// <summary>A type of the city's own era can be placed.</summary>
    [Fact]
    public void PlaceBuilding_TypeOfTheCitysEra_IsPlaced()
    {
        CityState city = City(era: 2, purse: 10);

        RulesAssert.Ok(Place(city, "manor", 0, 0));

        Assert.Equal("manor", Assert.Single(city.Buildings).TypeId.Value);
    }

    /// <summary>A type of an earlier era stays placeable.</summary>
    [Fact]
    public void PlaceBuilding_TypeOfAnEarlierEra_IsPlaced() => RulesAssert.Ok(Place(City(era: 2, purse: 10), "hut", 0, 0));

    /// <summary>With one of two builders busy, a placement takes the second.</summary>
    [Fact]
    public void PlaceBuilding_OneBuilderBusy_UsesTheSecond()
    {
        CityState city = City(purse: 10, buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003)]);

        RulesAssert.Ok(Place(city, "hut", 1, 0));

        Assert.Equal(2, CityQueries.BuildersBusy(city));
    }

    /// <summary>A type the content doesn't know is rejected as <see cref="MutationErrorCode.UnknownBuildingType"/>.</summary>
    [Fact]
    public void PlaceBuilding_UnknownType_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.UnknownBuildingType, City(purse: 100), city => Place(city, "castle", 0, 0));

    /// <summary>A type whose unlocking research is only running is still locked.</summary>
    [Fact]
    public void PlaceBuilding_LockedType_IsRejected()
    {
        CityState city = City(purse: 100, activeResearch: new ActiveResearch(new ResearchId("carpentry"), 990, 1090));

        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, city, city => Place(city, "workshop", 0, 0));
    }

    /// <summary>A tile outside the grid (negative, or at the width or height, or extreme values) is rejected as <see cref="MutationErrorCode.OutOfBounds"/>.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(6, 0)]
    [InlineData(0, 5)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void PlaceBuilding_TileOutsideTheGrid_IsRejected(int x, int y) =>
        RulesAssert.Rejected(MutationErrorCode.OutOfBounds, City(purse: 100), city => Place(city, "hut", x, y));

    /// <summary>The last column and row of the grid are inside it.</summary>
    [Theory]
    [InlineData(5, 4)]
    [InlineData(0, 4)]
    public void PlaceBuilding_LastTileOfTheGrid_IsInside(int x, int y) => RulesAssert.Ok(Place(City(purse: 10), "hut", x, y));

    /// <summary>A tile with a building on it, in any phase, is rejected as <see cref="MutationErrorCode.TileOccupied"/>.</summary>
    [Fact]
    public void PlaceBuilding_TileTaken_IsRejected()
    {
        CityState city = City(purse: 100, buildings: [Constructing(1, "hut", 3, 2, startedAt: 998, endsAt: 1003)]);

        RulesAssert.Rejected(MutationErrorCode.TileOccupied, city, city => Place(city, "lumber_camp", 3, 2));
    }

    /// <summary>A tile with a road on it is rejected as <see cref="MutationErrorCode.TileOccupied"/> too (CR-0008): a building never stands on a road.</summary>
    [Fact]
    public void PlaceBuilding_TileWithARoad_IsRejected()
    {
        CityState city = City(purse: 100, roads: [new RoadTile(3, 2, 1)]);

        RulesAssert.Rejected(MutationErrorCode.TileOccupied, city, city => Place(city, "hut", 3, 2));
    }

    /// <summary>
    /// With every builder busy (a construction site and an upgrade waiting for goods count too) a placement is rejected as
    /// <see cref="MutationErrorCode.NoFreeBuilder"/>.
    /// </summary>
    [Fact]
    public void PlaceBuilding_EveryBuilderBusy_IsRejected()
    {
        CityState city = City(purse: 100, buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003),
            Upgrading(2, "hut", 1, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003),
        ]);
        CityState projects = City(purse: 100, buildings: [Site(1, "workshop", 0, 0, needs: [("wood", 10)]), Idle(2, "hut", 1, 0, needs: [("wood", 5)])]);

        RulesAssert.Rejected(MutationErrorCode.NoFreeBuilder, city, city => Place(city, "hut", 2, 0));
        RulesAssert.Rejected(MutationErrorCode.NoFreeBuilder, projects, city => Place(city, "hut", 2, 0));
    }

    /// <summary>Coins the purse can't cover, even by one, are rejected as <see cref="MutationErrorCode.InsufficientResource"/>; goods never are.</summary>
    [Fact]
    public void PlaceBuilding_CoinsNotAffordable_IsRejected()
    {
        CityState city = City(purse: 39, buildings: [Idle(1, "depot", 5, 4, store: [("wood", 100)])], completedResearch: ["carpentry"]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, city, city => Place(city, "workshop", 0, 0));
    }

    /// <summary>When several checks fail, the first in the order of <c>04</c> section 8 is reported: the lock before bounds, the tile before the limit, builders and cost.</summary>
    [Fact]
    public void PlaceBuilding_SeveralChecksFail_ReportsTheFirstInSpecOrder()
    {
        // Locked, out of bounds and unaffordable at once: the lock is checked first.
        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, City(), city => Place(city, "workshop", -1, 0));

        // Tile taken, the type's limit reached, no free builder and unaffordable: the tile comes first, then the limit, and the cost last.
        CityState busy = City(buildings:
        [
            Constructing(1, "school", 0, 0, startedAt: 998, endsAt: 1003),
            Constructing(2, "hut", 1, 0, startedAt: 998, endsAt: 1003),
        ]);
        RulesAssert.Rejected(MutationErrorCode.TileOccupied, busy, city => Place(city, "school", 1, 0));
        RulesAssert.Rejected(MutationErrorCode.BuildingLimit, busy, city => Place(city, "school", 2, 0));
        RulesAssert.Rejected(MutationErrorCode.NoFreeBuilder, busy, city => Place(city, "hut", 2, 0));
    }

    /// <summary>A null city or content is a programmer error and throws, unlike a business rule.</summary>
    [Fact]
    public void PlaceBuilding_NullCityOrContent_Throws()
    {
        // The nulls are deliberate: they are programmer errors, not business rules.
        Assert.Throws<ArgumentNullException>(() => CityRules.PlaceBuilding(null!, TestContent, new BuildingTypeId("hut"), 0, 0));
        Assert.Throws<ArgumentNullException>(() => CityRules.PlaceBuilding(City(), null!, new BuildingTypeId("hut"), 0, 0));
    }

    /// <summary>Tries to place a building of the type on the tile, with the fixture content.</summary>
    private static MutationResult Place(CityState city, string type, int x, int y) =>
        CityRules.PlaceBuilding(city, TestContent, new BuildingTypeId(type), x, y);
}
