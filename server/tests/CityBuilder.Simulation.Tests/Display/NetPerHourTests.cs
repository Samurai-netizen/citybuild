using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Display;

/// <summary>
/// <c>ProductionDisplay.NetPerHour</c>: the net rate of an item from the buildings that are able to run. Fixture numbers as in
/// <see cref="PerHourTests"/>: with one worker a lumber camp makes 90 wood an hour, a workshop uses 80 wood and makes 40 boards an hour;
/// the wood cap is 100. Rates are thousandths of an item per hour.
/// </summary>
public sealed class NetPerHourTests
{
    private static readonly ItemId _wood = new("wood");
    private static readonly ItemId _boards = new("boards");

    [Fact]
    public void NetPerHour_CityWithoutBuildings_IsZero()
    {
        Assert.Equal(0L, ProductionDisplay.NetPerHour(City(), TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_IdleProducerThatCanStart_CountsItsOutput()
    {
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1)]);

        Assert.Equal(90_000L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_RunningCycle_CountsItAtItsWorkers()
    {
        CityState city = City(buildings:
            [Producing(1, "lumber_camp", 0, 0, 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 990, endsAt: 1010)]);

        Assert.Equal(180_000L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_FrozenCycle_ContributesZero()
    {
        CityState city = City(buildings:
            [Producing(1, "lumber_camp", 0, 0, 1, workers: 0, cycleStartedAt: 980, cycleLabor: 40, laborRemaining: 30, segmentStartedAt: 990, endsAt: null)]);

        Assert.Equal(0L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_IdleProducerWithoutWorkers_ContributesZero()
    {
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0)]);

        Assert.Equal(0L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_ConstructingAndUpgradingBuildings_ContributeZero()
    {
        CityState city = City(buildings:
        [
            Constructing(1, "lumber_camp", 0, 0, startedAt: 990, endsAt: 1005),
            Upgrading(2, "lumber_camp", 1, 0, level: 1, workers: 2, startedAt: 990, endsAt: 1005),
        ]);

        Assert.Equal(0L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_HouseWithWorkersAround_ContributesZero()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0)]);

        Assert.Equal(0L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_IdleProducerMissingAnInput_ContributesZero()
    {
        CityState city = City(inventory: [("wood", 1)], buildings: [Idle(1, "workshop", 0, 0, workers: 1)]);

        Assert.Equal(0L, ProductionDisplay.NetPerHour(city, TestContent, _boards));
        Assert.Equal(0L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_IdleProducerWhoseOutputIsAtTheCap_ContributesZero()
    {
        CityState city = City(inventory: [("wood", 100)], buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1)]);

        Assert.Equal(0L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_ProducerWithInputs_SubtractsWhatItUses()
    {
        CityState city = City(inventory: [("wood", 2)], buildings: [Idle(1, "workshop", 0, 0, workers: 1)]);

        Assert.Equal(-80_000L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
        Assert.Equal(40_000L, ProductionDisplay.NetPerHour(city, TestContent, _boards));
    }

    [Fact]
    public void NetPerHour_SeveralBuildings_AddUp()
    {
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1), Idle(2, "lumber_camp", 1, 0, workers: 2)]);

        Assert.Equal(270_000L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_ProducerAndConsumerOfOneItem_GivesTheDifference()
    {
        CityState city = City(inventory: [("wood", 2)], buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1), Idle(2, "workshop", 1, 0, workers: 1)]);

        Assert.Equal(10_000L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_CompletedLaborReduction_RaisesTheRate()
    {
        CityState city = City(completedResearch: ["better_saws"], buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1)]);

        Assert.Equal(120_000L, ProductionDisplay.NetPerHour(city, TestContent, _wood));
    }

    [Fact]
    public void NetPerHour_ItemNoBuildingTouches_IsZero()
    {
        CityState city = City(buildings: [Idle(1, "lumber_camp", 0, 0, workers: 1)]);

        Assert.Equal(0L, ProductionDisplay.NetPerHour(city, TestContent, new ItemId("gold")));
    }

    [Fact]
    public void NetPerHour_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.NetPerHour(null!, TestContent, _wood));
        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.NetPerHour(City(), null!, _wood));
    }
}
