using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// The trips of a city (<c>core-instructions/04-simulation-model.md</c>, sections 3, 16 and 18): <see cref="Trip"/> values, the restoring
/// constructor (sorted by id, ids below the next trip id, between buildings of the city), and the queries trips change: free workers, city
/// totals, what a building accepts and the amount modes. Fixture numbers: a hut provides 3 workers; tallying unlocks percentages.
/// </summary>
public sealed class CityTripsTests
{
    /// <summary>The good the trips here carry.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>A trip keeps every value it was made with.</summary>
    [Fact]
    public void Trip_Values_AreKept()
    {
        Trip trip = Trip(4, 2, 3, "wood", 6, 990, 1010);

        Assert.Equal((4, 2, 3, _wood, 6L, 990L, 1010L), (trip.Id, trip.SourceId, trip.TargetId, trip.Item, trip.Quantity, trip.DepartedAt, trip.ArrivesAt));
    }

    /// <summary>
    /// A trip is refused when its ids are below 1, its source is its target, it carries less than 1, it left before 0 or it doesn't arrive after
    /// it left.
    /// </summary>
    /// <param name="id">The trip id.</param>
    /// <param name="source">The source id.</param>
    /// <param name="target">The target id.</param>
    /// <param name="quantity">The load.</param>
    /// <param name="departedAt">The departure.</param>
    /// <param name="arrivesAt">The arrival.</param>
    [Theory]
    [InlineData(0, 2, 3, 6, 990, 1010)]
    [InlineData(1, 0, 3, 6, 990, 1010)]
    [InlineData(1, 2, 0, 6, 990, 1010)]
    [InlineData(1, 2, 2, 6, 990, 1010)]
    [InlineData(1, 2, 3, 0, 990, 1010)]
    [InlineData(1, 2, 3, 6, -1, 1010)]
    [InlineData(1, 2, 3, 6, 990, 990)]
    public void Trip_InvalidValues_AreRefused(int id, int source, int target, long quantity, long departedAt, long arrivesAt) =>
        Assert.Throws<ArgumentException>(() => Trip(id, source, target, "wood", quantity, departedAt, arrivesAt));

    /// <summary>The restoring constructor sorts the trips by id and keeps the next trip id.</summary>
    [Fact]
    public void Constructor_TripsInAnyOrder_SortsThemById()
    {
        CityState city = TripCity(trips: [Trip(5, 2, 3, "wood", 1, 990, 1010), Trip(2, 3, 2, "wood", 1, 990, 1020)], nextTripId: 9);

        Assert.Equal([2, 5], city.Trips.Select(trip => trip.Id));
        Assert.Equal(9, city.NextTripId);
    }

    /// <summary>
    /// The restoring constructor refuses a trip id twice, one not below the next trip id, a next trip id below 1, and a trip between buildings
    /// the city doesn't have.
    /// </summary>
    [Fact]
    public void Constructor_InvalidTrips_AreRefused()
    {
        Assert.Throws<ArgumentException>(() => TripCity(trips: [Trip(2, 2, 3, "wood", 1, 990, 1010), Trip(2, 3, 2, "wood", 1, 990, 1010)], nextTripId: 5));
        Assert.Throws<ArgumentException>(() => TripCity(trips: [Trip(5, 2, 3, "wood", 1, 990, 1010)], nextTripId: 5));
        Assert.Throws<ArgumentException>(() => TripCity(trips: [], nextTripId: 0));
        Assert.Throws<ArgumentException>(() => TripCity(trips: [Trip(1, 2, 7, "wood", 1, 990, 1010)], nextTripId: 2));
    }

    /// <summary>Each trip under way keeps one worker busy: 3 provided, 1 assigned, 1 on a trip, so 1 is free.</summary>
    [Fact]
    public void FreeWorkers_TripsUnderWay_CountAsBusy()
    {
        CityState city = City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0, workers: 1), Idle(3, "depot", 5, 0)],
            trips: [Trip(1, 2, 3, "wood", 6, 990, 1010)]);

        Assert.Equal(1, CityQueries.FreeWorkers(city, TestContent));
    }

    /// <summary>A city total counts the loads on trips with the stores: 4 in the camp, 6 on the way, 3 in the depot.</summary>
    [Fact]
    public void Total_LoadsOnTrips_Count()
    {
        CityState city = TripCity(trips: [Trip(1, 2, 3, "wood", 6, 990, 1010)], nextTripId: 2, campWood: 4, depotWood: 3);

        Assert.Equal(13, CityQueries.Total(city, TestContent, _wood));
        Assert.Equal(13, CityQueries.Totals(city, TestContent).Single(total => total.Item == _wood).Quantity);
    }

    /// <summary>A percentage is unlocked by tallying, an exact number by nothing in the fixture.</summary>
    [Fact]
    public void IsAmountModeUnlocked_ByResearch_OnlyAfterIt()
    {
        CityState before = City();
        CityState after = City(completedResearch: ["carpentry", "tallying"]);

        Assert.False(CityQueries.IsAmountModeUnlocked(before, TestContent, AmountMode.Percent));
        Assert.True(CityQueries.IsAmountModeUnlocked(after, TestContent, AmountMode.Percent));
        Assert.False(CityQueries.IsAmountModeUnlocked(after, TestContent, AmountMode.Exact));
    }

    /// <summary>
    /// The research's need counts only at the completed science building while the research waits: not at another building, not at a
    /// school still under construction, not once its timer runs.
    /// </summary>
    [Fact]
    public void ResearchNeedAt_OnlyAtTheCompletedScienceBuildingWhileWaiting()
    {
        var waiting = new ActiveResearch(new ResearchId("better_saws"), null, null, [new(new ItemId("boards"), 5)]);
        CityState city = City(completedResearch: ["carpentry"], buildings: [Idle(2, "depot", 0, 0), Idle(9, "school", 2, 2)], activeResearch: waiting);
        CityState building = City(completedResearch: ["carpentry"], buildings: [Constructing(9, "school", 2, 2, 990, 1010)], activeResearch: waiting);
        CityState running = City(completedResearch: ["carpentry"], buildings: [Idle(9, "school", 2, 2)], activeResearch: new ActiveResearch(new ResearchId("better_saws"), 990, 1190));
        var boards = new ItemId("boards");

        Assert.Equal(5, CityQueries.ResearchNeedAt(city, TestContent, city.Buildings[1], boards));
        Assert.Equal(0, CityQueries.ResearchNeedAt(city, TestContent, city.Buildings[0], boards));
        Assert.Equal(0, CityQueries.ResearchNeedAt(building, TestContent, building.Buildings[0], boards));
        Assert.Equal(0, CityQueries.ResearchNeedAt(running, TestContent, running.Buildings[0], boards));
    }

    /// <summary>A city with a hut 1, a lumber camp 2 and a depot 3, the given trips and next trip id, and the wood in the camp and the depot.</summary>
    private static CityState TripCity(Trip[] trips, int nextTripId, long campWood = 0, long depotWood = 0) =>
        City(
            buildings: [Idle(1, "hut", 0, 4), Idle(2, "lumber_camp", 0, 0, store: [("wood", campWood)]), Idle(3, "depot", 5, 0, store: [("wood", depotWood)])],
            trips: trips,
            nextTripId: nextTripId);
}
