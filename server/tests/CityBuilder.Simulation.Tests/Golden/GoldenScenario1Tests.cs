using System.Globalization;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// Golden scenario #1 (<see cref="GoldenScenario1"/>) against the table in <c>core-instructions/23-mvp-content.md</c>, which was checked by
/// hand. Each table row is one case: the stock, the free workers, every building's phase and stall reason, and the research. If a case
/// fails, work out by hand whether the table or the engine is wrong before changing either. The same script also runs one second at a time,
/// and must give the same city at every row.
/// </summary>
public sealed class GoldenScenario1Tests
{
    private const string _houses = "1 Idle NoRecipe | 2 Idle NoRecipe";
    private const string _researching = "active trade_charter until 600";
    private const string _researched = "completed trade_charter";

    [Theory]
    [InlineData(0, 450, 40, 10, 0, "1 Constructing | 2 Constructing", _researching)]
    [InlineData(30, 220, 10, 10, 8, _houses + " | 3 Constructing | 4 Constructing", _researching)]
    [InlineData(90, 220, 10, 10, 6, _houses + " | 3 Producing | 4 Constructing", _researching)]
    [InlineData(150, 220, 10, 10, 3, _houses + " | 3 Producing | 4 Producing", _researching)]
    [InlineData(600, 220, 11, 17, 3, _houses + " | 3 Producing | 4 Producing", _researched)]
    [InlineData(750, 20, 10, 0, 3, _houses + " | 3 Producing | 4 Producing | 5 Constructing", _researched)]
    [InlineData(930, 20, 10, 1, 1, _houses + " | 3 Producing | 4 Producing | 5 Producing", _researched)]
    [InlineData(990, 30, 10, 0, 1, _houses + " | 3 Producing | 4 Producing | 5 Producing", _researched)]
    [InlineData(1050, 40, 10, 1, 1, _houses + " | 3 Producing | 4 Producing | 5 Idle MissingInput:planks", _researched)]
    [InlineData(1170, 50, 10, 1, 1, _houses + " | 3 Producing | 4 Producing | 5 Idle MissingInput:planks", _researched)]
    public void Advance_GoldenScenario1AtATableRow_MatchesTheRow(
        long time, long coins, long logs, long planks, long freeWorkers, string buildings, string research)
    {
        CityState city = GoldenScenario1.After(time, GoldenScenario1.OneAdvancePerGap);

        Assert.Equal(time, city.Clock);
        Assert.Equal(coins, city.GetQuantity(new ItemId("coins")));
        Assert.Equal(logs, city.GetQuantity(new ItemId("logs")));
        Assert.Equal(planks, city.GetQuantity(new ItemId("planks")));
        Assert.Equal(freeWorkers, CityQueries.FreeWorkers(city, GoldenContent.V1));
        Assert.Equal(buildings, DescribeBuildings(city));
        Assert.Equal(research, DescribeResearch(city));
    }

    [Fact]
    public void Advance_GoldenScenario1At750BeforePlacingTheTradingPost_HasTheTwentyPlanksItCosts()
    {
        CityState city = GoldenScenario1.Before(750, GoldenScenario1.OneAdvancePerGap);

        Assert.Equal(220, city.GetQuantity(new ItemId("coins")));
        Assert.Equal(20, city.GetQuantity(new ItemId("planks")));
    }

    // At 1170 the advances are exactly one per gap between actions (30, 720, 420 s); at the other rows the last gap ends at the row instead.
    // The scenario stays far below the production cap, so split invariance (04 section 10, invariant 2) promises identical cities.
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(150)]
    [InlineData(600)]
    [InlineData(750)]
    [InlineData(930)]
    [InlineData(990)]
    [InlineData(1050)]
    [InlineData(1170)]
    public void Advance_GoldenScenario1InOneAdvancePerGapAndInOneSecondSteps_GivesIdenticalCities(long time)
    {
        CityState oneAdvancePerGap = GoldenScenario1.After(time, GoldenScenario1.OneAdvancePerGap);
        CityState oneSecondSteps = GoldenScenario1.After(time, 1);

        Assert.Equal(CityDump.Describe(oneAdvancePerGap), CityDump.Describe(oneSecondSteps));
    }

    // "<id> <phase>", followed by the stall reason when there is one, in ascending id.
    private static string DescribeBuildings(CityState city) =>
        string.Join(" | ", city.Buildings.Select(building =>
        {
            StallReason? stall = StallReason.Of(city, GoldenContent.V1, building);
            string text = string.Create(CultureInfo.InvariantCulture, $"{building.Id} {building.Phase}");
            return stall is null ? text : text + " " + stall;
        }));

    private static string DescribeResearch(CityState city)
    {
        ActiveResearch? active = city.ActiveResearch;
        return active is null
            ? "completed " + string.Join(", ", city.CompletedResearch)
            : string.Create(CultureInfo.InvariantCulture, $"active {active.Id} until {active.EndsAt}");
    }
}
