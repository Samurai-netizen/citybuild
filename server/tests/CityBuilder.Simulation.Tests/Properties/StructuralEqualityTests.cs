using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The structural equality helper of the property tests: equal cities and totals have no difference, and a difference is named by its path
/// and both values, the earliest one in the field order.
/// </summary>
public sealed class StructuralEqualityTests
{
    [Fact]
    public void FirstDifference_CitiesWithEqualValues_IsNull()
    {
        Assert.Null(StructuralEquality.FirstDifference(SampleCity(), SampleCity()));
    }

    [Fact]
    public void FirstDifference_ClockAndStockDiffer_NamesTheClockFirst()
    {
        CityState actual = SampleCity(clock: 1001, wood: 6);

        Assert.Equal("clock: expected 1000, actual 1001", StructuralEquality.FirstDifference(SampleCity(), actual));
    }

    [Fact]
    public void FirstDifference_StockDiffers_NamesTheItem()
    {
        Assert.Equal("inventory[wood]: expected 5, actual 6", StructuralEquality.FirstDifference(SampleCity(), SampleCity(wood: 6)));
    }

    [Fact]
    public void FirstDifference_ItemOnlyOnOneSide_NamesItAsAbsent()
    {
        Assert.Equal("inventory[wood]: expected 5, actual absent", StructuralEquality.FirstDifference(SampleCity(), SampleCity(wood: 0)));
        Assert.Equal("inventory[wood]: expected absent, actual 5", StructuralEquality.FirstDifference(SampleCity(wood: 0), SampleCity()));
    }

    [Fact]
    public void FirstDifference_BuildingFieldDiffers_NamesTheBuildingAndTheField()
    {
        // 30 labor left with 2 workers ends at 1015: the end comes before the labor in the field order.
        CityState actual = SampleCity(laborRemaining: 30, endsAt: 1015);

        Assert.Equal("building 2 phaseEndsAt: expected 1010, actual 1015", StructuralEquality.FirstDifference(SampleCity(), actual));
    }

    [Fact]
    public void FirstDifference_FrozenAgainstRunningCycle_NamesTheWorkersFirstAndShowsNoEndAsNull()
    {
        CityState actual = SampleCity(workers: 0, endsAt: null);

        Assert.Equal("building 2 assignedWorkers: expected 2, actual 0", StructuralEquality.FirstDifference(SampleCity(), actual));
        Assert.Contains(("building 2 phaseEndsAt", "null"), StructuralEquality.Fields(actual));
    }

    [Fact]
    public void FirstDifference_ExtraBuilding_NamesItsFirstField()
    {
        CityState actual = City(inventory: [("gold", 100), ("wood", 5)], buildings:
        [
            Idle(1, "hut", 0, 0),
            Producing(2, "lumber_camp", 1, 0, level: 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 20, segmentStartedAt: 1000, endsAt: 1010),
            Idle(3, "hut", 2, 0),
        ], activeResearch: new ActiveResearch(new ResearchId("carpentry"), 950, 1050));

        Assert.Equal("building 3 type: expected absent, actual hut", StructuralEquality.FirstDifference(SampleCity(nextBuildingId: 4), actual));
    }

    [Fact]
    public void FirstDifference_ActiveResearchDiffers_NamesBothTimers()
    {
        CityState actual = City(inventory: [("gold", 100), ("wood", 5)], buildings:
        [
            Idle(1, "hut", 0, 0),
            Producing(2, "lumber_camp", 1, 0, level: 1, workers: 2, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: 20, segmentStartedAt: 1000, endsAt: 1010),
        ]);

        Assert.Equal("activeResearch: expected carpentry 950..1050, actual none", StructuralEquality.FirstDifference(SampleCity(), actual));
    }

    [Fact]
    public void FirstDifference_TotalsOfEqualAdvances_IsNull()
    {
        ReportTotals expected = ReportTotals.Sum(Simulator.Advance(SampleCity(), TestContent, 30));
        ReportTotals actual = ReportTotals.Sum(Simulator.Advance(SampleCity(), TestContent, 30));

        Assert.Null(StructuralEquality.FirstDifference(expected, actual));
    }

    [Fact]
    public void FirstDifference_TotalsOfTwoPiecesAndOfTheWhole_AreEqual()
    {
        CityState split = SampleCity();
        SettlementReport first = Simulator.Advance(split, TestContent, 10);
        SettlementReport second = Simulator.Advance(split, TestContent, 50);

        // Over the 60 s the lumber camp's cycles end at 1010, 1030, 1050 and the research at 1050, in both runs.
        ReportTotals whole = ReportTotals.Sum(Simulator.Advance(SampleCity(), TestContent, 60));
        Assert.Null(StructuralEquality.FirstDifference(whole, ReportTotals.Sum(first, second)));
        Assert.Equal(("produced[wood]", "3"), whole.Fields().Single(field => field.Path == "produced[wood]"));
        Assert.Equal(("completedResearch", "carpentry"), whole.Fields().Single(field => field.Path == "completedResearch"));
    }

    [Fact]
    public void FirstDifference_TotalsDiffer_NamesTheFirstField()
    {
        ReportTotals expected = ReportTotals.Sum(Simulator.Advance(SampleCity(), TestContent, 0));
        ReportTotals actual = ReportTotals.Sum(Simulator.Advance(SampleCity(), TestContent, 60));

        Assert.Equal("elapsedSeconds: expected 0, actual 60", StructuralEquality.FirstDifference(expected, actual));
    }

    [Fact]
    public void Sum_NoReports_IsZeroAndEmpty()
    {
        Assert.Equal(
            [("elapsedSeconds", "0"), ("productionSeconds", "0"), ("completedBuildings", ""), ("completedResearch", ""), ("eraChanged", "false")],
            ReportTotals.Sum().Fields());
    }

    // A hut, a lumber camp halfway through a 40-labor cycle with 2 workers (ends at 1010) and carpentry researching until 1050.
    private static CityState SampleCity(
        long clock = 1000, long wood = 5, int workers = 2, long laborRemaining = 20, long? endsAt = 1010, int? nextBuildingId = null) =>
        City(clock: clock, inventory: wood == 0 ? [("gold", 100)] : [("gold", 100), ("wood", wood)], buildings:
        [
            Idle(1, "hut", 0, 0),
            Producing(2, "lumber_camp", 1, 0, level: 1, workers: workers, cycleStartedAt: 990, cycleLabor: 40, laborRemaining: laborRemaining, segmentStartedAt: 1000, endsAt: endsAt),
        ], activeResearch: new ActiveResearch(new ResearchId("carpentry"), 950, 1050), nextBuildingId: nextBuildingId);
}
