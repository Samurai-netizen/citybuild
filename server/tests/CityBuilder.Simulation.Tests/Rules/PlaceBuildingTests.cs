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

    [Fact]
    public void PlaceBuilding_ValidPlacement_NamesTheCostAsSpentAndNothingRefunded()
    {
        CityState city = City(inventory: [("gold", 50), ("wood", 12)], completedResearch: ["carpentry"]);

        RulesAssert.Moved(Place(city, "workshop", 0, 0), spent: "gold=40 wood=10", refunded: "");
    }

    [Fact]
    public void PlaceBuilding_StockExactlyTheCost_LeavesNoneOfTheItem()
    {
        CityState city = City(inventory: [("gold", 40), ("wood", 10)], completedResearch: ["carpentry"]);

        RulesAssert.Ok(Place(city, "workshop", 0, 0));

        Assert.Empty(city.Inventory);
    }

    [Fact]
    public void PlaceBuilding_TypeUnlockedByCompletedResearch_IsPlaced()
    {
        CityState city = City(inventory: [("gold", 40), ("wood", 10)], completedResearch: ["carpentry"]);

        RulesAssert.Ok(Place(city, "workshop", 5, 4));

        Building workshop = Assert.Single(city.Buildings);
        Assert.Equal(("workshop", BuildingPhase.Constructing, (long?)1020), (workshop.TypeId.Value, workshop.Phase, workshop.PhaseEndsAt));
    }

    [Fact]
    public void PlaceBuilding_TypeOfALaterEra_IsRejectedAsLocked() =>
        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, City(era: 1, inventory: [("gold", 100)]), city => Place(city, "manor", 0, 0));

    [Fact]
    public void PlaceBuilding_TypeOfTheCitysEra_IsPlaced()
    {
        CityState city = City(era: 2, inventory: [("gold", 10)]);

        RulesAssert.Ok(Place(city, "manor", 0, 0));

        Assert.Equal("manor", Assert.Single(city.Buildings).TypeId.Value);
    }

    [Fact]
    public void PlaceBuilding_TypeOfAnEarlierEra_IsPlaced() => RulesAssert.Ok(Place(City(era: 2, inventory: [("gold", 10)]), "hut", 0, 0));

    [Fact]
    public void PlaceBuilding_OneBuilderBusy_UsesTheSecond()
    {
        CityState city = City(inventory: [("gold", 10)], buildings: [Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003)]);

        RulesAssert.Ok(Place(city, "hut", 1, 0));

        Assert.Equal(2, CityQueries.BuildersBusy(city));
    }

    [Fact]
    public void PlaceBuilding_UnknownType_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.UnknownBuildingType, City(inventory: [("gold", 100)]), city => Place(city, "castle", 0, 0));

    [Fact]
    public void PlaceBuilding_LockedType_IsRejected()
    {
        CityState city = City(inventory: [("gold", 100), ("wood", 20)], activeResearch: new ActiveResearch(new ResearchId("carpentry"), 990, 1090));

        RulesAssert.Rejected(MutationErrorCode.BuildingLocked, city, city => Place(city, "workshop", 0, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(6, 0)]
    [InlineData(0, 5)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void PlaceBuilding_TileOutsideTheGrid_IsRejected(int x, int y) =>
        RulesAssert.Rejected(MutationErrorCode.OutOfBounds, City(inventory: [("gold", 100)]), city => Place(city, "hut", x, y));

    [Theory]
    [InlineData(5, 4)]
    [InlineData(0, 4)]
    public void PlaceBuilding_LastTileOfTheGrid_IsInside(int x, int y) => RulesAssert.Ok(Place(City(inventory: [("gold", 10)]), "hut", x, y));

    [Fact]
    public void PlaceBuilding_TileTaken_IsRejected()
    {
        CityState city = City(inventory: [("gold", 100)], buildings: [Constructing(1, "hut", 3, 2, startedAt: 998, endsAt: 1003)]);

        RulesAssert.Rejected(MutationErrorCode.TileOccupied, city, city => Place(city, "lumber_camp", 3, 2));
    }

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

    [Fact]
    public void PlaceBuilding_CostNotAffordable_IsRejected()
    {
        CityState city = City(inventory: [("gold", 40), ("wood", 9)], completedResearch: ["carpentry"]);

        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, city, city => Place(city, "workshop", 0, 0));
    }

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

    [Fact]
    public void PlaceBuilding_NullCityOrContent_Throws()
    {
        // The nulls are deliberate: they are programmer errors, not business rules.
        Assert.Throws<ArgumentNullException>(() => CityRules.PlaceBuilding(null!, TestContent, new BuildingTypeId("hut"), 0, 0));
        Assert.Throws<ArgumentNullException>(() => CityRules.PlaceBuilding(City(), null!, new BuildingTypeId("hut"), 0, 0));
    }

    private static MutationResult Place(CityState city, string type, int x, int y) =>
        CityRules.PlaceBuilding(city, TestContent, new BuildingTypeId(type), x, y);
}
