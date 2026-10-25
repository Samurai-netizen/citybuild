using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>CityRules.PlaceBuilding</c> (<c>04</c> section 8). Fixture numbers: the grid is 6 × 5 with two builders; a hut costs 10 gold and builds
/// in 5 s; a lumber camp costs 20 gold and builds in 10 s; a workshop (unlocked by carpentry) costs 40 gold and 10 wood and builds in 20 s;
/// a manor is a house of era 2.
/// </summary>
public sealed class PlaceBuildingTests
{
    /// <summary>A valid placement pays the cost and starts construction on the tile from the clock, with the next id, level 1 and no workers.</summary>
    [Fact]
    public void PlaceBuilding_ValidPlacement_PaysAndStartsConstructionWithTheNextId()
    {
        CityState city = City(clock: 1000, inventory: [("gold", 50), ("wood", 3)], buildings: [Idle(4, "hut", 0, 0)]);

        RulesAssert.Ok(Place(city, "lumber_camp", 2, 3));

        Assert.Equal(
            """
            clock=1000 era=1 next=6
            inventory: gold=30 wood=3
            building 4 hut (0,0) L1 w=0 Idle 0..null target=0 labor=0/0@0
            building 5 lumber_camp (2,3) L1 w=0 Constructing 1000..1010 target=0 labor=0/0@0
            completed:
            active: none
            """.ReplaceLineEndings("\n"),
            CityDump.Describe(city));
    }

    /// <summary>The result names the cost line by line as spent and nothing as refunded, for the ledger.</summary>
    [Fact]
    public void PlaceBuilding_ValidPlacement_NamesTheCostAsSpentAndNothingRefunded()
    {
        CityState city = City(inventory: [("gold", 50), ("wood", 12)], completedResearch: ["carpentry"]);

        RulesAssert.Moved(Place(city, "workshop", 0, 0), spent: "gold=40 wood=10", refunded: "");
    }

    /// <summary>A stock of exactly the cost is enough, and the item then leaves the stock entirely (no 0 entries are kept).</summary>
    [Fact]
    public void PlaceBuilding_StockExactlyTheCost_LeavesNoneOfTheItem()
    {
        CityState city = City(inventory: [("gold", 40), ("wood", 10)], completedResearch: ["carpentry"]);

        RulesAssert.Ok(Place(city, "workshop", 0, 0));

        Assert.Empty(city.Inventory);
    }

    /// <summary>A type unlocked by completed research can be placed.</summary>
    [Fact]
    public void PlaceBuilding_TypeUnlockedByCompletedResearch_IsPlaced()
    {
        CityState city = City(inventory: [("gold", 40), ("wood", 10)], completedResearch: ["carpentry"]);

        RulesAssert.Ok(Place(city, "workshop", 5, 4));

        Building workshop = Assert.Single(city.Buildings);
        Assert.Equal(("workshop", BuildingPhase.Constructing, (long?)1020), (workshop.TypeId.Value, workshop.Phase, workshop.PhaseEndsAt));
    }

    /// <summary>A type of a later era than the city's is rejected as <see cref="MutationErrorCode.BuildingLocked"/> (OD-10).</summary>
    [Fact]
    public void PlaceBuilding_TypeOfALaterEra_IsRejectedAsLocked() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, City(era: 1, inventory: [("gold", 100)]), city => Place(city, "manor", 0, 0));

    /// <summary>A type of the city's own era can be placed.</summary>
    [Fact]
    public void PlaceBuilding_TypeOfTheCitysEra_IsPlaced()
    {
        CityState city = City(era: 2, inventory: [("gold", 10)]);

        RulesAssert.Ok(Place(city, "manor", 0, 0));

        Assert.Equal("manor", Assert.Single(city.Buildings).TypeId.Value);
    }

    /// <summary>A type of an earlier era stays placeable.</summary>
    [Fact]
    public void PlaceBuilding_TypeOfAnEarlierEra_IsPlaced() => RulesAssert.Ok(Place(City(era: 2, inventory: [("gold", 10)]), "hut", 0, 0));

    /// <summary>With one of two builders busy, a placement takes the second.</summary>
    [Fact]
    public void PlaceBuilding_OneBuilderBusy_UsesTheSecond()
    {
        CityState city = City(inventory: [("gold", 10)], buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003)]);

        RulesAssert.Ok(Place(city, "hut", 1, 0));

        Assert.Equal(2, CityQueries.BuildersBusy(city));
    }

    /// <summary>A type the content doesn't know is rejected as <see cref="MutationErrorCode.UnknownBuildingType"/>.</summary>
    [Fact]
    public void PlaceBuilding_UnknownType_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.UnknownBuildingType, City(inventory: [("gold", 100)]), city => Place(city, "castle", 0, 0));

    /// <summary>A type whose unlocking research is only running is still locked.</summary>
    [Fact]
    public void PlaceBuilding_LockedType_IsRejected()
    {
        CityState city = City(inventory: [("gold", 100), ("wood", 20)], activeResearch: new ActiveResearch(new ResearchId("carpentry"), 990, 1090));

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
        RulesAssert.Rejected(MutationErrorCode.OutOfBounds, City(inventory: [("gold", 100)]), city => Place(city, "hut", x, y));

    /// <summary>The last column and row of the grid are inside it.</summary>
    [Theory]
    [InlineData(5, 4)]
    [InlineData(0, 4)]
    public void PlaceBuilding_LastTileOfTheGrid_IsInside(int x, int y) => RulesAssert.Ok(Place(City(inventory: [("gold", 10)]), "hut", x, y));

    /// <summary>A tile with a building on it, in any phase, is rejected as <see cref="MutationErrorCode.TileOccupied"/>.</summary>
    [Fact]
    public void PlaceBuilding_TileTaken_IsRejected()
    {
        CityState city = City(inventory: [("gold", 100)], buildings: [Constructing(1, "hut", 3, 2, startedAt: 998, endsAt: 1003)]);

        RulesAssert.Rejected(MutationErrorCode.TileOccupied, city, city => Place(city, "lumber_camp", 3, 2));
    }

    /// <summary>With every builder busy (a construction and an upgrade) a placement is rejected as <see cref="MutationErrorCode.NoFreeBuilder"/>.</summary>
    [Fact]
    public void PlaceBuilding_EveryBuilderBusy_IsRejected()
    {
        CityState city = City(inventory: [("gold", 100)], buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003),
            Upgrading(2, "hut", 1, 0, level: 1, workers: 0, startedAt: 995, endsAt: 1003),
        ]);

        RulesAssert.Rejected(MutationErrorCode.NoFreeBuilder, city, city => Place(city, "hut", 2, 0));
    }

    /// <summary>A cost the stock can't cover, even by one item, is rejected as <see cref="MutationErrorCode.InsufficientResource"/>.</summary>
    [Fact]
    public void PlaceBuilding_CostNotAffordable_IsRejected()
    {
        CityState city = City(inventory: [("gold", 40), ("wood", 9)], completedResearch: ["carpentry"]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, city, city => Place(city, "workshop", 0, 0));
    }

    /// <summary>When several checks fail, the first in the order of <c>04</c> section 8 is reported: the lock before bounds, the tile before builders and cost.</summary>
    [Fact]
    public void PlaceBuilding_SeveralChecksFail_ReportsTheFirstInSpecOrder()
    {
        // Locked, out of bounds and unaffordable at once: the lock is checked first.
        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, City(), city => Place(city, "workshop", -1, 0));

        // Tile taken, no free builder and unaffordable: the tile comes first, and the cost is checked last.
        CityState busy = City(buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003),
            Constructing(2, "hut", 1, 0, startedAt: 998, endsAt: 1003),
        ]);
        RulesAssert.Rejected(MutationErrorCode.TileOccupied, busy, city => Place(city, "hut", 1, 0));
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
