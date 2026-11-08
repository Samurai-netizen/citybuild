using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>Restoring a city: sorted lists, stores without zeros, projects, lookups, read-only views, and the checks that need no content.</summary>
public sealed class CityStateTests
{
    /// <summary>The restoring constructor keeps the clock, era and next building id.</summary>
    [Fact]
    public void Constructor_ValidValues_KeepsClockEraAndNextId()
    {
        CityState city = City(clock: 1234, era: 2, nextBuildingId: 9);

        Assert.Equal((1234L, 2, 9), (city.Clock, city.Era, city.NextBuildingId));
        Assert.Null(city.ActiveResearch);
    }

    /// <summary>Buildings come sorted by id whatever order they are given in.</summary>
    [Fact]
    public void Constructor_BuildingsInAnyOrder_SortsThemById()
    {
        CityState city = City(buildings: [Idle(5, "hut", 0, 0), Idle(2, "hut", 1, 0), Idle(3, "hut", 2, 0)]);

        Assert.Equal([2, 3, 5], city.Buildings.Select(building => building.Id));
    }

    /// <summary>A building's store keeps only quantities above 0, in item order; the purse keeps its coins (CR-0008: goods live in buildings).</summary>
    [Fact]
    public void Constructor_StoreWithZeros_KeepsOnlyPositiveQuantitiesInItemOrder()
    {
        CityState city = City(purse: 30, buildings: [Idle(1, "workshop", 0, 0, store: [("wood", 7), ("boards", 0)])]);
        Building workshop = city.Buildings[0];

        Assert.Equal(30, city.Purse);
        Assert.Equal([("wood", 7L)], workshop.Store.Select(stock => (stock.Key.Value, stock.Value)));
        Assert.Equal((7L, 0L), (workshop.GetStock(new ItemId("wood")), workshop.GetStock(new ItemId("boards"))));
    }

    /// <summary>A store reads as a dictionary in item order, with 0 for goods it doesn't hold.</summary>
    [Fact]
    public void Store_ReadAsADictionary_GivesTheStockInItemOrder()
    {
        IReadOnlyDictionary<ItemId, long> stock = Idle(1, "depot", 0, 0, store: [("wood", 7), ("boards", 30)]).Store;

        Assert.Equal((2, 30L, true, false), (stock.Count, stock[new ItemId("boards")], stock.ContainsKey(new ItemId("wood")), stock.ContainsKey(new ItemId("gold"))));
        Assert.Equal(["boards", "wood"], stock.Keys.Select(item => item.Value));
        Assert.Equal([30L, 7L], stock.Values);
        Assert.False(stock.TryGetValue(new ItemId("gold"), out long none));
        Assert.Equal(0, none);
        Assert.Throws<KeyNotFoundException>(() => stock[new ItemId("gold")]);
    }

    /// <summary>
    /// A construction site is constructing without a start or an end and with needs; an idle or producing building with needs has an upgrade
    /// waiting; neither is the other (<c>04</c> sections 4 and 16).
    /// </summary>
    [Fact]
    public void Projects_SiteAndWaitingUpgrade_AreToldApart()
    {
        Building site = Site(1, "workshop", 0, 0, needs: [("wood", 10)]);
        Building waiting = Idle(2, "hut", 1, 0, needs: [("wood", 5)]);
        Building constructing = Constructing(3, "hut", 2, 0, 1000, 1005);

        Assert.Equal((true, false, 10L), (site.IsConstructionSite, site.HasUpgradeWaiting, site.GetNeed(new ItemId("wood"))));
        Assert.Equal((false, true, 5L), (waiting.IsConstructionSite, waiting.HasUpgradeWaiting, waiting.GetNeed(new ItemId("wood"))));
        Assert.Equal((false, false), (constructing.IsConstructionSite, constructing.HasUpgradeWaiting));
    }

    /// <summary>A research waiting for goods has needs and no start or end; one whose timer runs has both and no needs (<c>04</c> section 5).</summary>
    [Fact]
    public void ActiveResearch_WaitingOrRunning_HoldsItsValues()
    {
        var waiting = new ActiveResearch(new ResearchId("carpentry"), null, null, [new(new ItemId("wood"), 4)]);
        var running = new ActiveResearch(new ResearchId("carpentry"), 10, 110);

        Assert.Equal((true, (long?)null, (long?)null, 4L), (waiting.IsWaiting, waiting.StartedAt, waiting.EndsAt, waiting.Needs[new ItemId("wood")]));
        Assert.Equal((false, (long?)10, (long?)110, 0), (running.IsWaiting, running.StartedAt, running.EndsAt, running.Needs.Count));
    }

    /// <summary>Completed research comes sorted in ordinal order.</summary>
    [Fact]
    public void Constructor_CompletedResearchInAnyOrder_SortsItInOrdinalOrder()
    {
        CityState city = City(completedResearch: ["carpentry", "better_saws"]);

        Assert.Equal(["better_saws", "carpentry"], city.CompletedResearch.Select(id => id.Value));
        Assert.True(city.HasCompletedResearch(new ResearchId("carpentry")));
        Assert.False(city.HasCompletedResearch(new ResearchId("warehouses")));
    }

    /// <summary>A building is found by its id.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public void TryGetBuilding_KnownId_FindsIt(int id)
    {
        Assert.True(ThreeBuildings().TryGetBuilding(id, out Building? found));
        Assert.Equal(id, found.Id);
    }

    /// <summary>An id with no building finds nothing.</summary>
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

    /// <summary>The city's lists can't be changed through a cast, and a store and needs have no writable interface at all.</summary>
    [Fact]
    public void Lists_CastBack_CanNotBeChanged()
    {
        CityState city = City(purse: 5, buildings: [Idle(1, "depot", 0, 0, store: [("wood", 1)], needs: [("boards", 2)])], completedResearch: ["carpentry"]);
        Building depot = city.Buildings[0];

        // A store and needs have no writable interface at all, so there is nothing to cast them back to (M0-12).
        Assert.False(depot.Store is ICollection<KeyValuePair<ItemId, long>> || depot.Store is System.Collections.IDictionary);
        Assert.False(depot.Needs is ICollection<KeyValuePair<ItemId, long>> || depot.Needs is System.Collections.IDictionary);
        Assert.Throws<NotSupportedException>(() => ((IList<Building>)city.Buildings).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ResearchId>)city.CompletedResearch).Clear());
    }

    /// <summary>Values that can't form a city, by name: a negative clock, era 0, duplicates, a taken tile, research completed twice or also running.</summary>
    private static readonly Dictionary<string, Func<CityState>> _invalidCities = new()
    {
        ["negative clock"] = () => City(clock: -1),
        ["era 0"] = () => City(era: 0),
        ["next building id 0"] = () => City(nextBuildingId: 0),
        ["negative purse"] = () => City(purse: -1),
        ["negative store"] = () => City(buildings: [Idle(1, "depot", 0, 0, store: [("wood", -1)])]),
        ["good listed twice in a store"] = () => City(buildings: [Idle(1, "depot", 0, 0, store: [("wood", 1), ("wood", 2)])]),
        ["negative need"] = () => City(buildings: [Site(1, "workshop", 0, 0, needs: [("wood", -1)])]),
        ["needs on an upgrading building"] = () =>
            City(buildings: [new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Upgrading, 1, 9, 2, 0, 0, 0, null, CityFixture.Pairs([("wood", 1)]))]),
        ["a construction site with a timer"] = () =>
            City(buildings: [new Building(1, new BuildingTypeId("workshop"), 0, 0, 1, 0, BuildingPhase.Constructing, 1, 9, 0, 0, 0, 0, null, CityFixture.Pairs([("wood", 1)]))]),
        ["constructing without needs or an end"] = () =>
            City(buildings: [new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Constructing, 0, null, 0, 0, 0, 0)]),
        ["building id listed twice"] = () => City(buildings: [Idle(2, "hut", 0, 0), Idle(2, "hut", 1, 0)]),
        ["building id not below the next id"] = () => City(buildings: [Idle(3, "hut", 0, 0)], nextBuildingId: 3),
        ["two buildings on one tile"] = () => City(buildings: [Idle(1, "hut", 2, 2), Idle(2, "hut", 2, 2)]),
        ["research completed twice"] = () => City(completedResearch: ["carpentry", "carpentry"]),
        ["running research already completed"] = () =>
            City(completedResearch: ["carpentry"], activeResearch: new ActiveResearch(new ResearchId("carpentry"), 0, 100)),
    };

    /// <summary>The names of <see cref="_invalidCities"/>.</summary>
    public static TheoryData<string> InvalidCities => new(_invalidCities.Keys);

    /// <summary>Each invalid set of values throws <see cref="ArgumentException"/>.</summary>
    [Theory]
    [MemberData(nameof(InvalidCities))]
    public void Constructor_InvalidValues_Throws(string problem) => Assert.Throws<ArgumentException>(_invalidCities[problem]);

    /// <summary>A null list, or a null building in the list, is a programmer error and throws.</summary>
    [Fact]
    public void Constructor_NullListOrBuilding_Throws()
    {
        // The nulls are deliberate: a missing list is a programmer error.
        Assert.Throws<ArgumentNullException>(() => new CityState(0, 1, 1, 0, null!, [], null));
        Assert.Throws<ArgumentNullException>(() => new CityState(0, 1, 1, 0, [], null!, null));
        Assert.Throws<ArgumentNullException>(() => new CityState(0, 1, 1, 0, [null!], [], null));
    }

    /// <summary>A running research that ends before it starts, or starts before 0, throws.</summary>
    [Fact]
    public void ActiveResearch_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ActiveResearch(new ResearchId("carpentry"), 100, 99));
        Assert.Throws<ArgumentException>(() => new ActiveResearch(new ResearchId("carpentry"), -1, 10));
        Assert.Throws<ArgumentException>(() => new ActiveResearch(new ResearchId("carpentry"), null, null, null));
        Assert.Throws<ArgumentException>(() => new ActiveResearch(new ResearchId("carpentry"), 1, 10, [new(new ItemId("wood"), 1)]));
        Assert.Throws<ArgumentException>(() => new ActiveResearch(new ResearchId("carpentry"), null, 10, [new(new ItemId("wood"), 1)]));
    }

    /// <summary>A city with three huts with the ids 1, 4 and 6.</summary>
    private static CityState ThreeBuildings() => City(buildings: [Idle(1, "hut", 0, 0), Idle(4, "hut", 1, 0), Idle(6, "hut", 2, 0)]);
}
