using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>Restoring a city: sorted lists, the stock without zeros, lookups, read-only views, and the checks that need no content.</summary>
public sealed class CityStateTests
{
    [Fact]
    public void Constructor_ValidValues_KeepsClockEraAndNextId()
    {
        CityState city = City(clock: 1234, era: 2, nextBuildingId: 9);

        Assert.Equal((1234L, 2, 9), (city.Clock, city.Era, city.NextBuildingId));
        Assert.Null(city.ActiveResearch);
    }

    [Fact]
    public void Constructor_BuildingsInAnyOrder_SortsThemById()
    {
        CityState city = City(buildings: [Idle(5, "hut", 0, 0), Idle(2, "hut", 1, 0), Idle(3, "hut", 2, 0)]);

        Assert.Equal([2, 3, 5], city.Buildings.Select(building => building.Id));
    }

    [Fact]
    public void Constructor_StockWithZeros_KeepsOnlyPositiveQuantitiesInItemOrder()
    {
        CityState city = City(inventory: [("wood", 7), ("boards", 0), ("gold", 30)]);

        Assert.Equal([("gold", 30L), ("wood", 7L)], city.Inventory.Select(stock => (stock.Key.Value, stock.Value)));
        Assert.Equal(0, city.GetQuantity(new ItemId("boards")));
    }

    [Fact]
    public void Constructor_CompletedResearchInAnyOrder_SortsItInOrdinalOrder()
    {
        CityState city = City(completedResearch: ["carpentry", "better_saws"]);

        Assert.Equal(["better_saws", "carpentry"], city.CompletedResearch.Select(id => id.Value));
        Assert.True(city.HasCompletedResearch(new ResearchId("carpentry")));
        Assert.False(city.HasCompletedResearch(new ResearchId("warehouses")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public void TryGetBuilding_KnownId_FindsIt(int id)
    {
        Assert.True(ThreeBuildings().TryGetBuilding(id, out Building? found));
        Assert.Equal(id, found.Id);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(7)]
    public void TryGetBuilding_UnknownId_FindsNothing(int id)
    {
        Assert.False(ThreeBuildings().TryGetBuilding(id, out Building? found));
        Assert.Null(found);
    }

    [Fact]
    public void Lists_CastBack_CanNotBeChanged()
    {
        CityState city = City(inventory: [("gold", 5)], buildings: [Idle(1, "hut", 0, 0)], completedResearch: ["carpentry"]);

        Assert.Throws<NotSupportedException>(() => ((IDictionary<ItemId, long>)city.Inventory).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Building>)city.Buildings).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ResearchId>)city.CompletedResearch).Clear());
    }

    private static readonly Dictionary<string, Func<CityState>> _invalidCities = new()
    {
        ["negative clock"] = () => City(clock: -1),
        ["era 0"] = () => City(era: 0),
        ["next building id 0"] = () => City(nextBuildingId: 0),
        ["negative stock"] = () => City(inventory: [("gold", -1)]),
        ["item listed twice"] = () => City(inventory: [("gold", 1), ("gold", 2)]),
        ["building id listed twice"] = () => City(buildings: [Idle(2, "hut", 0, 0), Idle(2, "hut", 1, 0)]),
        ["building id not below the next id"] = () => City(buildings: [Idle(3, "hut", 0, 0)], nextBuildingId: 3),
        ["two buildings on one tile"] = () => City(buildings: [Idle(1, "hut", 2, 2), Idle(2, "hut", 2, 2)]),
        ["research completed twice"] = () => City(completedResearch: ["carpentry", "carpentry"]),
        ["running research already completed"] = () =>
            City(completedResearch: ["carpentry"], activeResearch: new ActiveResearch(new ResearchId("carpentry"), 0, 100)),
    };

    public static TheoryData<string> InvalidCities => new(_invalidCities.Keys);

    [Theory]
    [MemberData(nameof(InvalidCities))]
    public void Constructor_InvalidValues_Throws(string problem) => Assert.Throws<ArgumentException>(_invalidCities[problem]);

    [Fact]
    public void Constructor_NullListOrBuilding_Throws()
    {
        // The nulls are deliberate: a missing list is a programmer error.
        Assert.Throws<ArgumentNullException>(() => new CityState(0, 1, 1, null!, [], [], null));
        Assert.Throws<ArgumentNullException>(() => new CityState(0, 1, 1, [], null!, [], null));
        Assert.Throws<ArgumentNullException>(() => new CityState(0, 1, 1, [], [], null!, null));
        Assert.Throws<ArgumentNullException>(() => new CityState(0, 1, 1, [], [null!], [], null));
    }

    [Fact]
    public void ActiveResearch_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ActiveResearch(new ResearchId("carpentry"), 100, 99));
        Assert.Throws<ArgumentException>(() => new ActiveResearch(new ResearchId("carpentry"), -1, 10));
    }

    private static CityState ThreeBuildings() => City(buildings: [Idle(1, "hut", 0, 0), Idle(4, "hut", 1, 0), Idle(6, "hut", 2, 0)]);
}
