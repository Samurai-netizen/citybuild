using System.Globalization;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Properties;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// Golden scenario #2 (<see cref="GoldenScenario2"/>) against the table in <c>core-instructions/23-mvp-content.md</c> for content <c>mvp-2</c>
/// (the frozen <see cref="GoldenContent.V2"/>, CR-0008), checked by hand in CR-0008-T1 part B3. Each table row is one case: the purse, the
/// Woodcutter's logs, the Warehouse (what it needs, building, or its logs) and courier 1 (state, stamina, load). A later content change
/// updates the table in <c>23</c>, not these cases. If a case fails, work out by hand whether the expectation or the engine is wrong before
/// changing either. The same script also runs one second at a time, and must give the same city at every row.
/// </summary>
public sealed class GoldenScenario2Tests
{
    /// <summary>The table's rows: the time, the purse, the Woodcutter's logs, the Warehouse and courier 1, as the cases' text gives them.</summary>
    public static TheoryData<long, long, long, string, string> Rows => new()
    {
        { 60, 440, 0, "needs 20 logs", "none" },
        { 90, 440, 1, "needs 20 logs", "Walking 14400 0" },
        { 95, 440, 0, "needs 20 logs", "Walking 14400 1" },
        { 105, 440, 0, "needs 19 logs", "Walking 14400 0" },
        { 685, 440, 0, "builds", "Walking 14105 0" },
        { 745, 440, 0, "2 logs", "Walking 14045 0" },
        { 3685, 440, 0, "100 logs", "Walking 11405 0" },
        { 3975, 440, 0, "100 logs", "Walking 11115 10" },
        { 5160, 440, 40, "100 logs", "Walking 9935 10" },
        { 15095, 440, 40, "100 logs", "Returning 0 10" },
        { 15100, 440, 40, "100 logs", "Resting 0 10" },
        { 25000, 440, 40, "100 logs", "Walking 14400 10" },
        { 25005, 440, 40, "100 logs", "Walking 14400 10" },
    };

    /// <summary>
    /// At each row of the table the city has exactly the row's purse, Woodcutter logs, Warehouse and courier 1. Courier 1's stamina between
    /// rows is the table's own arithmetic: it spends only after 390 (in the first absence) and after 1 045 (in the second), at each arrival,
    /// so at an arrival at <c>t</c> it has 14 400 − (t − 390) left in the first absence (14 105 at 685, where the table shows no value) and
    /// 14 045 − (t − 1 045) in the second.
    /// </summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void Advance_GoldenScenario2AtATableRow_MatchesTheRow(long time, long purse, long woodcutterLogs, string warehouse, string courier)
    {
        CityState city = GoldenScenario2.After(time, GoldenScenario2.OneAdvancePerGap);

        Assert.Equal(time, city.Clock);
        Assert.Equal(purse, city.Purse);
        Assert.Equal(woodcutterLogs, Stock(city, 2));
        Assert.Equal(warehouse, DescribeWarehouse(city));
        Assert.Equal(courier, DescribeCourier(city, 1));
    }

    /// <summary>
    /// The walks of the table: courier 1 leaves at 90 for the Woodcutter (5 s, arriving at 95), takes the log and walks along the road to the
    /// Warehouse (10 s, 105); after the re-energizing at 25000 it leaves at once for the Woodcutter (25005) and walks on to the Warehouse
    /// (25015). Courier 2 has no route and stays at the station with its full stamina.
    /// </summary>
    [Fact]
    public void Advance_GoldenScenario2_WalksAsTheTableSays()
    {
        Assert.Equal("\ncourier 3/1 Walking to=1 90..95 stamina=14400 load:", CityDump.CourierLine(Courier(GoldenScenario2.After(90, GoldenScenario2.OneAdvancePerGap), 1)));
        Assert.Equal("\ncourier 3/1 Walking to=2 95..105 stamina=14400 load: logs@2=1", CityDump.CourierLine(Courier(GoldenScenario2.After(95, GoldenScenario2.OneAdvancePerGap), 1)));
        Assert.Equal("\ncourier 3/1 Walking to=1 25000..25005 stamina=14400 load: logs@2=10", CityDump.CourierLine(Courier(GoldenScenario2.After(25000, GoldenScenario2.OneAdvancePerGap), 1)));
        Assert.Equal("\ncourier 3/1 Walking to=2 25005..25015 stamina=14400 load: logs@2=10", CityDump.CourierLine(Courier(GoldenScenario2.After(25005, GoldenScenario2.OneAdvancePerGap), 1)));
        Assert.Equal("\ncourier 3/2 AtStation stamina=14400 load:", CityDump.CourierLine(Courier(GoldenScenario2.After(25005, GoldenScenario2.OneAdvancePerGap), 2)));
    }

    /// <summary>At 685 the 20th log reaches the site and the Warehouse builds for its 60 s, until 745; at 5160 the Woodcutter's store is full.</summary>
    [Fact]
    public void Advance_GoldenScenario2_TheWarehouseBuildsFrom685AndTheWoodcutterFillsAt5160()
    {
        Assert.True(GoldenScenario2.After(685, GoldenScenario2.OneAdvancePerGap).TryGetBuilding(4, out Building? warehouse));
        Assert.Equal((BuildingPhase.Constructing, 685L, (long?)745), (warehouse!.Phase, warehouse.PhaseStartedAt, warehouse.PhaseEndsAt));

        CityState full = GoldenScenario2.After(5160, GoldenScenario2.OneAdvancePerGap);
        Assert.True(full.TryGetBuilding(2, out Building? woodcutter));
        Assert.Equal("StorageFull:logs", StallReason.Of(full, GoldenContent.V2, woodcutter!)?.ToString());
    }

    /// <summary>
    /// The settle at 25000 (745 → 25000) reports what the table says: 148 logs produced, 98 moved into the Warehouse's store, courier 1's
    /// 14 045 s of stamina as the courier seconds, and courier 1 tired at 15095. The settle at 745 spent 355 s and tired no one.
    /// </summary>
    [Fact]
    public void Advance_GoldenScenario2_TheLooksReportWhatTheTableSays()
    {
        List<(long Time, List<SettlementReport> Settle)> requests = GoldenScenario2.Run(25000, GoldenScenario2.OneAdvancePerGap).Requests;
        SettlementReport look = Assert.Single(requests.Single(request => request.Time == 25000).Settle);
        SettlementReport first = Assert.Single(requests.Single(request => request.Time == 745).Settle);

        Assert.Equal((148L, 98L), (look.Produced[new ItemId("logs")], look.Moved[new ItemId("logs")]));
        Assert.Equal(14_045, look.CourierSeconds);
        TiredCourier tired = Assert.Single(look.TiredCouriers);
        Assert.Equal((3, 1, 15_095L), (tired.StationId, tired.Slot, tired.At));
        Assert.Equal((355L, 0), (first.CourierSeconds, first.TiredCouriers.Count));
    }

    /// <summary>
    /// The table's check: by 25005 the Woodcutter made 170 logs (20 delivered to the site, 100 in the Warehouse, 10 carried, 40 in its store)
    /// and 360 of the 800 coins were spent. The city's totals say the same.
    /// </summary>
    [Fact]
    public void Advance_GoldenScenario2At25005_TotalsMatchTheCheck()
    {
        CityState city = GoldenScenario2.After(25005, GoldenScenario2.OneAdvancePerGap);

        Assert.Equal(
            ["coins 440", "logs 150", "planks 0"],
            CityQueries.Totals(city, GoldenContent.V2).Select(line => line.Item + " " + line.Quantity.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// One advance per gap and one-second steps, each step with its settle's absence start, give identical cities and settle reports at every
    /// row (<c>04</c> section 10, invariant 2: for one <c>absentFrom</c>, any split).
    /// </summary>
    [Theory]
    [InlineData(90)]
    [InlineData(745)]
    [InlineData(5160)]
    [InlineData(15100)]
    [InlineData(25005)]
    public void Advance_GoldenScenario2InOneAdvancePerGapAndInOneSecondSteps_GivesIdenticalCitiesAndReports(long time)
    {
        (CityState oneAdvancePerGap, List<(long Time, List<SettlementReport> Settle)> gapRequests) = GoldenScenario2.Run(time, GoldenScenario2.OneAdvancePerGap);
        (CityState oneSecondSteps, List<(long Time, List<SettlementReport> Settle)> stepRequests) = GoldenScenario2.Run(time, 1);

        Assert.Equal(CityDump.Describe(oneAdvancePerGap), CityDump.Describe(oneSecondSteps));
        Assert.Equal(
            gapRequests.Select(request => ReportTotals.Sum(request.Settle).Fields()),
            stepRequests.Select(request => ReportTotals.Sum(request.Settle).Fields()));
    }

    /// <summary>The logs in building <paramref name="id"/>'s store.</summary>
    private static long Stock(CityState city, int id)
    {
        Assert.True(city.TryGetBuilding(id, out Building? building));
        return building!.GetStock(new ItemId("logs"));
    }

    /// <summary>The Warehouse as <c>needs N logs</c> while its site waits, <c>builds</c> while its timer runs, else <c>N logs</c> in its store.</summary>
    private static string DescribeWarehouse(CityState city)
    {
        Assert.True(city.TryGetBuilding(4, out Building? warehouse));
        if (warehouse!.Needs.TryGetValue(new ItemId("logs"), out long needed))
        {
            return string.Create(CultureInfo.InvariantCulture, $"needs {needed} logs");
        }

        return warehouse.Phase == BuildingPhase.Constructing
            ? "builds"
            : string.Create(CultureInfo.InvariantCulture, $"{warehouse.GetStock(new ItemId("logs"))} logs");
    }

    /// <summary>Courier <paramref name="slot"/> of the station as <c>state stamina carried</c>, or <c>none</c> before the station has couriers.</summary>
    private static string DescribeCourier(CityState city, int slot)
    {
        Courier? courier = city.Couriers.FirstOrDefault(candidate => candidate.StationId == 3 && candidate.Slot == slot);
        return courier is null
            ? "none"
            : string.Create(CultureInfo.InvariantCulture, $"{courier.State} {courier.Stamina} {courier.Carried}");
    }

    /// <summary>Courier <paramref name="slot"/> of the station; the test fails when there is none.</summary>
    private static Courier Courier(CityState city, int slot) =>
        city.Couriers.Single(candidate => candidate.StationId == 3 && candidate.Slot == slot);
}
