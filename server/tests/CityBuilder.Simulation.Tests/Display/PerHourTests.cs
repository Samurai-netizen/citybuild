using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Display;

/// <summary>
/// <c>ProductionDisplay.PerHour</c>, the preview of a worker count. Fixture numbers: a lumber camp makes 1 wood from 40 labor (up to 2 workers,
/// 3 on level 2); a workshop makes 1 boards from 2 wood with 90 labor (up to 2 workers, 6 on level 2 with 180 labor); a mint makes 5 gold
/// from 20 labor (up to 2 workers). The research <c>better_saws</c> takes 25 % off the lumber camp's labor and 10 % off the workshop's.
/// Rates are thousandths of an item per hour.
/// </summary>
public sealed class PerHourTests
{
    /// <summary>A raw producer has no input rates and an output rate of 3600 s over its cycle: 90 wood an hour with 1 worker, 180 with 2.</summary>
    [Theory]
    [InlineData(1, "wood:90000")] // 40 s per cycle
    [InlineData(2, "wood:180000")] // 20 s per cycle
    public void PerHour_RawProducer_GivesItsOutputAndNoInputs(int workers, string expectedOutputs)
    {
        ProductionRates rates = RatesOf(Idle(1, "lumber_camp", 0, 0), workers);

        Assert.Empty(rates.Inputs);
        Assert.Equal(expectedOutputs, Text(rates.Outputs));
    }

    /// <summary>A producer with inputs shows both sides: the workshop uses 80 wood and makes 40 boards an hour with 1 worker.</summary>
    [Theory]
    [InlineData(1, "wood:80000", "boards:40000")] // 90 s per cycle
    [InlineData(2, "wood:160000", "boards:80000")] // 45 s per cycle
    public void PerHour_ProducerWithInputs_GivesInputsAndOutputs(int workers, string expectedInputs, string expectedOutputs)
    {
        ProductionRates rates = RatesOf(Idle(1, "workshop", 0, 0), workers);

        Assert.Equal(expectedInputs, Text(rates.Inputs));
        Assert.Equal(expectedOutputs, Text(rates.Outputs));
    }

    /// <summary>With no workers the cycle would freeze, so every line shows 0, still in recipe order.</summary>
    [Fact]
    public void PerHour_NoWorkers_IsZeroForEveryItem()
    {
        ProductionRates rates = RatesOf(Idle(1, "workshop", 0, 0), 0);

        Assert.Equal("wood:0", Text(rates.Inputs));
        Assert.Equal("boards:0", Text(rates.Outputs));
    }

    /// <summary>The cycle's seconds round up (40 labor, 3 workers: 14 s), and the rate then rounds down (257.142 an hour).</summary>
    [Fact]
    public void PerHour_CycleSecondsRoundUp_ThenTheRateRoundsDown()
    {
        // 40 labor with 3 workers: ceil(40 / 3) = 14 s; 3600 s / 14 s = 257.142... per hour.
        ProductionRates rates = RatesOf(Idle(1, "lumber_camp", 0, 0, level: 2), 3);

        Assert.Equal("wood:257142", Text(rates.Outputs));
    }

    /// <summary>Several items per cycle scale the rate: 5 gold per 20 s is 900 an hour.</summary>
    [Fact]
    public void PerHour_QuantityPerCycleAboveOne_ScalesTheRate()
    {
        // 5 gold per 20 s with one worker: 900 per hour.
        Assert.Equal("gold:900000", Text(RatesOf(Idle(1, "mint", 0, 0), 1).Outputs));
    }

    /// <summary>Completed research counts, because the next cycle gets it: 25 % off 40 labor gives 120 wood an hour instead of 90.</summary>
    [Fact]
    public void PerHour_CompletedLaborReduction_ShortensTheCycle()
    {
        CityState city = City(completedResearch: ["better_saws"]);

        // 40 labor - 25 % = 30 s with one worker: 120 per hour instead of 90.
        Assert.Equal("wood:120000", Text(RatesOf(city, Idle(1, "lumber_camp", 0, 0), 1).Outputs));
    }

    /// <summary>More workers never show a lower rate, from 0 to the level's maximum of 6.</summary>
    [Fact]
    public void PerHour_AtEveryWorkerCountOfTheLevel_NeverDecreasesWithMoreWorkers()
    {
        Building workshop = Idle(1, "workshop", 0, 0, level: 2);
        long previous = -1;

        for (int workers = 0; workers <= 6; workers++)
        {
            long current = RatesOf(workshop, workers).Outputs[0].MilliPerHour;

            Assert.True(current >= previous, $"{workers} workers: {current} after {previous}");
            previous = current;
        }
    }

    /// <summary>An upgrading building shows the recipe and worker limit of the level it has now, not the target level.</summary>
    [Fact]
    public void PerHour_UsesTheCurrentLevelOfAnUpgradingBuilding()
    {
        // The recipe and the worker limit of level 2 apply: 180 labor, 4 workers, 45 s per cycle.
        Building upgrading = Upgrading(1, "workshop", 0, 0, level: 2, workers: 4, startedAt: 990, endsAt: 1005);

        Assert.Equal("boards:80000", Text(RatesOf(upgrading, 4).Outputs));
    }

    /// <summary>A worker count below 0 or above the level's maximum is a programmer error and throws for <c>workers</c>.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void PerHour_WorkersOutsideTheLevel_Throws(int workers)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => RatesOf(Idle(1, "workshop", 0, 0), workers));

        Assert.Equal("workers", exception.ParamName);
    }

    /// <summary>A house has no recipe, so asking for its rates is a programmer error.</summary>
    [Fact]
    public void PerHour_House_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => RatesOf(Idle(1, "hut", 0, 0), 0));

        Assert.Equal("building", exception.ParamName);
    }

    /// <summary>A null city, content or building throws.</summary>
    [Fact]
    public void PerHour_NullArguments_Throw()
    {
        CityState city = City();
        Building building = Idle(1, "workshop", 0, 0);

        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.PerHour(null!, TestContent, building, 1));
        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.PerHour(city, null!, building, 1));
        Assert.Throws<ArgumentNullException>(() => ProductionDisplay.PerHour(city, TestContent, null!, 1));
    }

    /// <summary>The rates of a building in an empty fixture city.</summary>
    private static ProductionRates RatesOf(Building building, int workers) => RatesOf(City(), building, workers);

    /// <summary>The rates of a building in a given city (for completed research).</summary>
    private static ProductionRates RatesOf(CityState city, Building building, int workers) =>
        ProductionDisplay.PerHour(city, TestContent, building, workers);

    /// <summary>Rates as <c>item:milli</c> joined by commas, for compact assertions.</summary>
    private static string Text(IReadOnlyList<ItemRate> rates) => string.Join(",", rates.Select(rate => rate.ToString()));
}
