using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Display;

/// <summary>
/// <c>ProductionDisplay.SecondsUntilFull</c> (<c>04</c> section 15): when a good fills one building's store at that building's own rate
/// (CR-0008: per store). Fixture numbers: a lumber camp's store holds 100 wood (150 at level 2, and 50 more with the research
/// <c>warehouses</c>); one worker on a camp makes 90 wood an hour (a camp on level 2 with 3 workers 257.142), a workshop with one worker uses 80
/// wood an hour; gold is the currency, which no store holds.
/// </summary>
public sealed class SecondsUntilFullTests
{
    /// <summary>The good whose fill time is measured.</summary>
    private static readonly ItemId _wood = new("wood");

    /// <summary>The time is the room left in the camp's store at its rate: 100 wood at 90 an hour is 4000 s, 1 wood is 40 s.</summary>
    /// <param name="stock">The wood in the camp's store.</param>
    /// <param name="expected">The expected seconds.</param>
    [Theory]
    [InlineData(0, 4_000L)] // 100 wood at 90 an hour
    [InlineData(10, 3_600L)] // 90 wood: one hour
    [InlineData(99, 40L)] // 1 wood
    public void SecondsUntilFull_CampFillingItsStore_CountsTheRoomLeft(long stock, long expected)
    {
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1, store: [("wood", stock)])]);

        Assert.Equal(expected, SecondsUntilFull(city, 1, _wood));
    }

    /// <summary>The time rounds up (2101 s, since 2100 s would give 149.99 wood), so it never shows the limit sooner than it comes.</summary>
    [Fact]
    public void SecondsUntilFull_RoundsUp()
    {
        // ceil(150 × 3600 / 257.142) = 2101 s: 2100 s would be 149.99 wood.
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0, level: 2, workers: 3)]);

        Assert.Equal(2_101L, SecondsUntilFull(city, 1, _wood));
    }

    /// <summary>The limit includes completed research bonuses: 150 wood at 90 an hour.</summary>
    [Fact]
    public void SecondsUntilFull_UsesTheLimitWithBonuses()
    {
        CityState city = City(completedResearch: ["warehouses"], buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1)]);

        // 100 + 50 wood at 90 an hour.
        Assert.Equal(6_000L, SecondsUntilFull(city, 1, _wood));
    }

    /// <summary>A good the store doesn't hold, and the currency (in no store), never fill it: no time.</summary>
    [Fact]
    public void SecondsUntilFull_GoodTheStoreDoesNotHold_IsNull()
    {
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1), Idle(2, "mint", 1, 0, workers: 1)]);

        Assert.Null(SecondsUntilFull(city, 1, new ItemId("boards")));
        Assert.Null(SecondsUntilFull(city, 2, new ItemId("gold")));
    }

    /// <summary>A store whose building makes nothing (a warehouse) never fills by itself: deliveries come in steps, not as a rate.</summary>
    [Fact]
    public void SecondsUntilFull_WarehouseStore_IsNull()
    {
        Assert.Null(SecondsUntilFull(City(buildings: [Idle(1, "depot", 0, 0, store: [("wood", 10)])]), 1, _wood));
    }

    /// <summary>With more used than made the store empties, so there is no time.</summary>
    [Fact]
    public void SecondsUntilFull_NetRateBelowZero_IsNull()
    {
        CityState city = City(buildings: [Idle(1, "workshop", 0, 0, workers: 1, store: [("wood", 10)])]);

        Assert.Null(SecondsUntilFull(city, 1, _wood));
    }

    /// <summary>A camp without workers makes nothing, so there is no time.</summary>
    [Fact]
    public void SecondsUntilFull_CampWithoutWorkers_IsNull()
    {
        Assert.Null(SecondsUntilFull(City(buildings: [Idle(1, "lumber_camp", 0, 0)]), 1, _wood));
    }

    /// <summary>A store already above its limit (refunds may do that) while its cycle still runs is full now: 0 s.</summary>
    [Fact]
    public void SecondsUntilFull_StoreAboveTheLimitWhileACycleRuns_IsZero()
    {
        // Refunds may put the store above its limit; the running cycle still makes wood.
        CityState city = City(buildings:
        [
            Producing(1, "lumber_camp", 0, 0, 1, workers: 1, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1030,
                store: [("wood", 120)]),
        ]);

        Assert.Equal(0L, SecondsUntilFull(city, 1, _wood));
    }

    /// <summary>At its limit an idle camp can't start (storage full), so its rate is 0 and there is no time.</summary>
    [Fact]
    public void SecondsUntilFull_FullStoreWithAnIdleCamp_IsNull()
    {
        // The camp can't start (storage full), so nothing is made: rate 0.
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1, store: [("wood", 100)])]);

        Assert.Null(SecondsUntilFull(city, 1, _wood));
    }

    /// <summary>A null city, content or building throws.</summary>
    [Fact]
    public void SecondsUntilFull_NullArguments_Throw()
    {
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0)]);

        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.SecondsUntilFull(null!, TestContent, city.Buildings[0], _wood));
        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.SecondsUntilFull(city, null!, city.Buildings[0], _wood));
        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.SecondsUntilFull(city, TestContent, null!, _wood));
    }

    /// <summary>The seconds until the good fills the store of the building with the id, with the fixture content.</summary>
    private static long? SecondsUntilFull(CityState city, int buildingId, ItemId item)
    {
        Assert.True(city.TryGetBuilding(buildingId, out Building? building));
        return ProductionDisplay.SecondsUntilFull(city, TestContent, building, item);
    }
}
