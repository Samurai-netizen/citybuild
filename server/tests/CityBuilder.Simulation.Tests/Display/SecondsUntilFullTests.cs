using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Display;

/// <summary>
/// <c>ProductionDisplay.SecondsUntilFull</c>. Fixture numbers: wood is capped at 100 (150 with the research <c>warehouses</c>), gold is
/// uncapped; one worker on a lumber camp makes 90 wood an hour (a camp on level 2 with 3 workers 257.142), a workshop with one worker
/// uses 80 wood an hour.
/// </summary>
public sealed class SecondsUntilFullTests
{
    private static readonly ItemId _wood = new("wood");

    [Theory]
    [InlineData(0, 4_000L)] // 100 wood at 90 an hour
    [InlineData(10, 3_600L)] // 90 wood: one hour
    [InlineData(99, 40L)] // 1 wood
    public void SecondsUntilFull_CampFillingTheStock_CountsTheMissingWood(long stock, long expected)
    {
        CityState city = City(inventory: [("wood", stock)], buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1)]);

        Assert.Equal(expected, ProductionDisplay.SecondsUntilFull(city, TestContent, _wood));
    }

    [Fact]
    public void SecondsUntilFull_RoundsUp()
    {
        // ceil(100 × 3600 / 257.142) = 1401 s: 1400 s would be 99.99 wood.
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0, level: 2, workers: 3)]);

        Assert.Equal(1_401L, ProductionDisplay.SecondsUntilFull(city, TestContent, _wood));
    }

    [Fact]
    public void SecondsUntilFull_UsesTheCapWithBonuses()
    {
        CityState city = City(completedResearch: ["warehouses"], buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1)]);

        // 150 wood at 90 an hour.
        Assert.Equal(6_000L, ProductionDisplay.SecondsUntilFull(city, TestContent, _wood));
    }

    [Fact]
    public void SecondsUntilFull_UncappedItem_IsNull()
    {
        CityState city = City(buildings: [Idle(1, "mint", 0, 0, workers: 1)]);

        Assert.Null(ProductionDisplay.SecondsUntilFull(city, TestContent, new ItemId("gold")));
    }

    [Fact]
    public void SecondsUntilFull_NothingMakesTheItem_IsNull()
    {
        Assert.Null(ProductionDisplay.SecondsUntilFull(City(), TestContent, _wood));
    }

    [Fact]
    public void SecondsUntilFull_NetRateBelowZero_IsNull()
    {
        CityState city = City(inventory: [("wood", 10)], buildings: [Idle(1, "workshop", 0, 0, workers: 1)]);

        Assert.Null(ProductionDisplay.SecondsUntilFull(city, TestContent, _wood));
    }

    [Fact]
    public void SecondsUntilFull_CampWithoutWorkers_IsNull()
    {
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0)]);

        Assert.Null(ProductionDisplay.SecondsUntilFull(city, TestContent, _wood));
    }

    [Fact]
    public void SecondsUntilFull_StockAboveTheCapWhileACycleRuns_IsZero()
    {
        // Refunds may put the stock above the cap; the running cycle still makes wood.
        CityState city = City(inventory: [("wood", 120)], buildings:
            [Producing(1, "lumber_camp", 0, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1030)]);

        Assert.Equal(0L, ProductionDisplay.SecondsUntilFull(city, TestContent, _wood));
    }

    [Fact]
    public void SecondsUntilFull_FullStockWithOnlyAnIdleCamp_IsNull()
    {
        // The camp can't start (storage full), so nothing is made: net rate 0.
        CityState city = City(inventory: [("wood", 100)], buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1)]);

        Assert.Null(ProductionDisplay.SecondsUntilFull(city, TestContent, _wood));
    }

    [Fact]
    public void SecondsUntilFull_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.SecondsUntilFull(null!, TestContent, _wood));
        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.SecondsUntilFull(City(), null!, _wood));
    }
}
