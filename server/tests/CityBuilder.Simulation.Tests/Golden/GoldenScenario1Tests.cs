using System.Globalization;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// Golden scenario #1 (<see cref="GoldenScenario1"/>) against the table in <c>core-instructions/23-mvp-content.md</c> for content <c>mvp-2</c>
/// (the frozen <see cref="GoldenContent.V2"/>, CR-0008), checked by hand in CR-0008-T1 part B3. Each table row is one case: the purse, the free
/// workers, the Woodcutter's and the Sawmill's stores, what waits for goods, every building's phase and stall reason, and the research. A
/// later content change updates the table in <c>23</c>, not these cases. If a case fails, work out by hand whether the expectation or the
/// engine is wrong before changing either. The same script also runs one second at a time, and must give the same city at every row.
/// </summary>
public sealed class GoldenScenario1Tests
{
    /// <summary>The research line while trade_charter runs (from 60 until 660).</summary>
    private const string _researching = "active trade_charter until 660";

    /// <summary>At each row of the table the city has exactly the row's purse, free workers, stores, needs, phases, stall reasons and research.</summary>
    [Theory]
    [InlineData(0, 670, 0, "0", "—", "", "1 Constructing | 2 Constructing", "none")]
    [InlineData(30, 570, 4, "0", "—", "", "1 Idle NoRecipe | 2 Constructing | 3 Constructing", "none")]
    [InlineData(60, 220, 2, "0", "0 / 0", "4: 10 logs", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Constructing | 5 Constructing", _researching)]
    [InlineData(90, 220, 6, "1", "0 / 0", "4: 10 logs", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Constructing | 5 Idle NoRecipe", _researching)]
    [InlineData(360, 220, 5, "0", "0 / 0", "4: 10 logs", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Constructing | 5 Idle NoRecipe", _researching)]
    [InlineData(370, 220, 6, "0", "0 / 0", "", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Constructing | 5 Idle NoRecipe", _researching)]
    [InlineData(490, 220, 2, "0", "0 / 0", "", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Idle MissingInput:logs | 5 Idle NoRecipe", _researching)]
    [InlineData(500, 220, 3, "0", "2 / 0", "", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Producing | 5 Idle NoRecipe", _researching)]
    [InlineData(560, 220, 3, "2", "0 / 1", "", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Producing | 5 Idle NoRecipe", _researching)]
    [InlineData(620, 220, 3, "4", "0 / 2", "", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Idle MissingInput:logs | 5 Idle NoRecipe", _researching)]
    [InlineData(660, 20, 1, "3", "0 / 0", "6: 10 planks", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Idle MissingInput:logs | 5 Idle NoRecipe | 6 Constructing", "completed trade_charter")]
    [InlineData(670, 20, 3, "3", "1 / 0", "6: 8 planks", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Producing | 5 Idle NoRecipe | 6 Constructing", "completed trade_charter")]
    [InlineData(730, 20, 3, "5", "1 / 1", "6: 8 planks", "1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | 4 Idle MissingInput:logs | 5 Idle NoRecipe | 6 Constructing", "completed trade_charter")]
    public void Advance_GoldenScenario1AtATableRow_MatchesTheRow(
        long time, long purse, long freeWorkers, string woodcutterLogs, string sawmillStore, string waiting, string buildings, string research)
    {
        CityState city = GoldenScenario1.After(time, GoldenScenario1.OneAdvancePerGap);

        Assert.Equal(time, city.Clock);
        Assert.Equal(purse, city.Purse);
        Assert.Equal(freeWorkers, CityQueries.FreeWorkers(city, GoldenContent.V2));
        Assert.Equal(woodcutterLogs, Stored(city, 2, "logs"));
        Assert.Equal(sawmillStore, city.TryGetBuilding(4, out _) ? Stored(city, 4, "logs") + " / " + Stored(city, 4, "planks") : "—");
        Assert.Equal(waiting, DescribeWaiting(city));
        Assert.Equal(buildings, DescribeBuildings(city));
        Assert.Equal(research, DescribeResearch(city));
    }

    /// <summary>The Sawmill's site gets its 10 logs at 370 and builds for its 120 s, until 490, as the row at 370 says.</summary>
    [Fact]
    public void Advance_GoldenScenario1At370_TheSawmillBuildsUntil490()
    {
        CityState city = GoldenScenario1.After(370, GoldenScenario1.OneAdvancePerGap);

        Assert.True(city.TryGetBuilding(4, out Building? sawmill));
        Assert.Equal((BuildingPhase.Constructing, 370L, (long?)490), (sawmill!.Phase, sawmill.PhaseStartedAt, sawmill.PhaseEndsAt));
    }

    /// <summary>
    /// The trips of the table: at 660, before the actions, the Sawmill holds 2 planks and the Woodcutter 6 logs; after them trip 3 carries the
    /// 2 planks and trip 4 half of the 6 logs, 3, and both arrive at 670 (two ground steps, 10 s).
    /// </summary>
    [Fact]
    public void Advance_GoldenScenario1At660_TheTripsCarryWhatTheRowSays()
    {
        CityState before = GoldenScenario1.Before(660, GoldenScenario1.OneAdvancePerGap);
        CityState after = GoldenScenario1.After(660, GoldenScenario1.OneAdvancePerGap);

        Assert.Equal(("6", "2"), (Stored(before, 2, "logs"), Stored(before, 4, "planks")));
        Assert.Equal(
            ["4 -> 6 2 planks at 670", "2 -> 4 3 logs at 670"],
            after.Trips.Select(trip => string.Create(
                CultureInfo.InvariantCulture, $"{trip.SourceId} -> {trip.TargetId} {trip.Quantity} {trip.Item} at {trip.ArrivesAt}")));
    }

    /// <summary>
    /// The table's check: by 730 the Woodcutter made 22 logs (10 delivered to the site, 6 used by the Sawmill, 6 in stores), the Sawmill
    /// 3 planks (2 delivered, 1 stored), and 780 of the 800 coins were spent. The city's totals say the same.
    /// </summary>
    [Fact]
    public void Advance_GoldenScenario1At730_TotalsMatchTheCheck()
    {
        CityState city = GoldenScenario1.After(730, GoldenScenario1.OneAdvancePerGap);

        Assert.Equal(
            ["coins 20", "logs 6", "planks 1"],
            CityQueries.Totals(city, GoldenContent.V2).Select(line => line.Item + " " + line.Quantity.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// One advance per gap and one-second steps give identical cities at every row. The scenario stays far below the production cap, so split
    /// invariance (<c>04</c> section 10, invariant 2) promises identical cities.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(360)]
    [InlineData(370)]
    [InlineData(490)]
    [InlineData(500)]
    [InlineData(560)]
    [InlineData(620)]
    [InlineData(660)]
    [InlineData(670)]
    [InlineData(730)]
    public void Advance_GoldenScenario1InOneAdvancePerGapAndInOneSecondSteps_GivesIdenticalCities(long time)
    {
        CityState oneAdvancePerGap = GoldenScenario1.After(time, GoldenScenario1.OneAdvancePerGap);
        CityState oneSecondSteps = GoldenScenario1.After(time, 1);

        Assert.Equal(CityDump.Describe(oneAdvancePerGap), CityDump.Describe(oneSecondSteps));
    }

    /// <summary>How much of the item building <paramref name="id"/> holds in its store, as text.</summary>
    private static string Stored(CityState city, int id, string item)
    {
        Assert.True(city.TryGetBuilding(id, out Building? building));
        return building!.Store.TryGetValue(new ItemId(item), out long quantity) ? quantity.ToString(CultureInfo.InvariantCulture) : "0";
    }

    /// <summary>Each building with needs as "id: quantity item", in ascending id, joined by " | "; empty when nothing waits for goods.</summary>
    private static string DescribeWaiting(CityState city) =>
        string.Join(" | ", city.Buildings.Where(building => building.Needs.Count > 0).Select(building => string.Create(
            CultureInfo.InvariantCulture,
            $"{building.Id}: {string.Join(", ", building.Needs.OrderBy(need => need.Key.Value, StringComparer.Ordinal).Select(need => $"{need.Value} {need.Key}"))}")));

    /// <summary>Each building as its id and phase, followed by its stall reason when it has one, in ascending id, joined by " | ".</summary>
    private static string DescribeBuildings(CityState city) =>
        string.Join(" | ", city.Buildings.Select(building =>
            {
                StallReason? stall = StallReason.Of(city, GoldenContent.V2, building);
                string text = string.Create(CultureInfo.InvariantCulture, $"{building.Id} {building.Phase}");
                return stall is null ? text : text + " " + stall;
            }));

    /// <summary>The research as "active id until end", "completed" followed by the completed nodes, or "none".</summary>
    private static string DescribeResearch(CityState city)
    {
        ActiveResearch? active = city.ActiveResearch;
        if (active is not null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"active {active.Id} until {active.EndsAt}");
        }

        return city.CompletedResearch.Count == 0 ? "none" : "completed " + string.Join(", ", city.CompletedResearch);
    }
}
