using System.Globalization;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Properties;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// Golden scenario #3 (<see cref="GoldenScenario3"/>) against the table in <c>core-instructions/28-era1-content.md</c> for content <c>era1-1</c>
/// (the frozen <see cref="GoldenContent.V3"/>), checked by hand in M1-02. Each table row is one case: the Farm's grain, the Windmill and the
/// Bakery (phase, inputs, outputs) and the two couriers (state, stamina, load). A later content change updates the table in <c>28</c>, not
/// these cases. If a case fails, work out by hand whether the expectation or the engine is wrong before changing either. The same script
/// also runs one second at a time, and must give the same city at every row.
/// </summary>
public sealed class GoldenScenario3Tests
{
    /// <summary>The table's rows: the time, the Farm's grain, the Windmill, the Bakery and couriers 1 and 2, as the cases' text gives them.</summary>
    public static TheoryData<long, long, string, string, string, string> Rows => new()
    {
        { 0, 0, "Idle 0 0", "Idle 0 0", "Walking 14400 0", "Walking 14400 0" },
        { 30, 1, "Idle 0 0", "Idle 0 0", "Walking 14400 0", "Walking 14400 0" },
        { 35, 0, "Idle 0 0", "Idle 0 0", "Walking 14400 1", "Walking 14400 0" },
        { 45, 0, "Idle 1 0", "Idle 0 0", "Walking 14400 0", "Walking 14400 0" },
        { 85, 0, "Producing to 145 0 0", "Idle 0 0", "Walking 14400 0", "Walking 14400 0" },
        { 145, 0, "Producing to 205 0 0", "Idle 0 0", "Walking 14400 0", "Walking 14400 1" },
        { 155, 0, "Producing to 205 0 0", "Producing to 215 0 0", "Walking 14400 1", "Walking 14400 0" },
        { 215, 0, "Producing to 265 0 0", "Producing to 275 0 1", "Walking 14400 1", "Walking 14400 0" },
        { 300, 1, "Producing to 325 1 0", "Producing to 335 0 2", "Walking 14400 0", "Walking 14400 0" },
        { 600, 1, "Producing to 625 1 0", "Producing to 635 0 7", "Walking 14400 0", "Walking 14400 0" },
        { 1355, 0, "Producing to 1405 0 0", "Idle 1 20", "Walking 14400 1", "Walking 14400 0" },
        { 1500, 1, "Producing to 1525 1 0", "Idle 3 20", "Walking 14400 0", "Walking 14400 0" },
    };

    /// <summary>At each row of the table the city has exactly the row's Farm grain, Windmill, Bakery and couriers, and the purse never moves.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void Advance_GoldenScenario3AtATableRow_MatchesTheRow(long time, long farmGrain, string windmill, string bakery, string courier1, string courier2)
    {
        CityState city = GoldenScenario3.After(time, GoldenScenario3.OneAdvancePerGap);

        Assert.Equal(time, city.Clock);
        Assert.Equal(1000, city.Purse);
        Assert.Equal(farmGrain, Stock(city, GoldenScenario3.Farm, "grain"));
        Assert.Equal(windmill, DescribeProducer(city, GoldenScenario3.Windmill, "grain", "flour"));
        Assert.Equal(bakery, DescribeProducer(city, GoldenScenario3.Bakery, "flour", "bread"));
        Assert.Equal(courier1, DescribeCourier(city, 1));
        Assert.Equal(courier2, DescribeCourier(city, 2));
    }

    /// <summary>
    /// The walks of the table: courier 1 leaves at 0 for the Farm (3 steps, 15 s) and then walks Farm ↔ Windmill in 10 s; courier 2 leaves
    /// at 0 for the Windmill (1 step, 5 s) and then walks Windmill ↔ Bakery in 10 s.
    /// </summary>
    [Fact]
    public void Advance_GoldenScenario3_WalksAsTheTableSays()
    {
        CityState start = GoldenScenario3.After(0, GoldenScenario3.OneAdvancePerGap);
        CityState later = GoldenScenario3.After(35, GoldenScenario3.OneAdvancePerGap);

        Assert.Equal("\ncourier 7/1 Walking to=1 0..15 stamina=14400 load:", CityDump.CourierLine(Courier(start, 1)));
        Assert.Equal("\ncourier 7/2 Walking to=1 0..5 stamina=14400 load:", CityDump.CourierLine(Courier(start, 2)));
        Assert.Equal("\ncourier 7/1 Walking to=2 35..45 stamina=14400 load: grain@4=1", CityDump.CourierLine(Courier(later, 1)));
        Assert.Equal("\ncourier 7/2 Walking to=2 25..35 stamina=14400 load:", CityDump.CourierLine(Courier(GoldenScenario3.After(30, GoldenScenario3.OneAdvancePerGap), 2)));
    }

    /// <summary>At 1355 the Bakery makes its 20th bread, a full store, and stops: <c>StorageFull:bread</c>; the flour that keeps coming waits.</summary>
    [Fact]
    public void Advance_GoldenScenario3At1355_TheBakeryIsFull()
    {
        CityState city = GoldenScenario3.After(1355, GoldenScenario3.OneAdvancePerGap);

        Assert.True(city.TryGetBuilding(GoldenScenario3.Bakery, out Building? bakery));
        Assert.Equal("StorageFull:bread", StallReason.Of(city, GoldenContent.V3, bakery!)?.ToString());
    }

    /// <summary>
    /// The table's check at 1500: the Farm made 50 grain (1 in its store, 1 in the Windmill, 48 milled in 24 cycles), the Windmill 23 flour
    /// (20 baked, 3 waiting in the Bakery), the Bakery 20 bread; no coins moved. The city's totals say the same (the running Windmill
    /// cycle's flour is reserved, not in a store yet), item by item in id order.
    /// </summary>
    [Fact]
    public void Advance_GoldenScenario3At1500_TotalsMatchTheCheck()
    {
        CityState city = GoldenScenario3.After(1500, GoldenScenario3.OneAdvancePerGap);

        Assert.Equal(
            ["bread 20", "coins 1000", "cut_stone 0", "flour 3", "grain 2", "logs 0", "planks 0", "stone 0"],
            CityQueries.Totals(city, GoldenContent.V3).Select(line => line.Item + " " + line.Quantity.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// One advance per gap and one-second steps give identical cities and settle reports at every row named (<c>04</c> section 10,
    /// invariant 2: for one <c>absentFrom</c>, any split).
    /// </summary>
    [Theory]
    [InlineData(145)]
    [InlineData(600)]
    [InlineData(1355)]
    [InlineData(1500)]
    public void Advance_GoldenScenario3InOneAdvancePerGapAndInOneSecondSteps_GivesIdenticalCitiesAndReports(long time)
    {
        (CityState oneAdvancePerGap, List<(long Time, List<SettlementReport> Settle)> gapRequests) = GoldenScenario3.Run(time, GoldenScenario3.OneAdvancePerGap);
        (CityState oneSecondSteps, List<(long Time, List<SettlementReport> Settle)> stepRequests) = GoldenScenario3.Run(time, 1);

        Assert.Equal(CityDump.Describe(oneAdvancePerGap), CityDump.Describe(oneSecondSteps));
        Assert.Equal(
            gapRequests.Select(request => ReportTotals.Sum(request.Settle).Fields()),
            stepRequests.Select(request => ReportTotals.Sum(request.Settle).Fields()));
    }

    /// <summary>The <paramref name="item"/> in building <paramref name="id"/>'s store.</summary>
    private static long Stock(CityState city, int id, string item)
    {
        Assert.True(city.TryGetBuilding(id, out Building? building));
        return building!.GetStock(new ItemId(item));
    }

    /// <summary>A producer as <c>phase [to end] input output</c>: its phase, a running cycle's end, and its store's input and output goods.</summary>
    private static string DescribeProducer(CityState city, int id, string input, string output)
    {
        Assert.True(city.TryGetBuilding(id, out Building? building));
        string phase = building!.PhaseEndsAt is long end
            ? string.Create(CultureInfo.InvariantCulture, $"{building.Phase} to {end}")
            : building.Phase.ToString();
        return string.Create(CultureInfo.InvariantCulture, $"{phase} {Stock(city, id, input)} {Stock(city, id, output)}");
    }

    /// <summary>Courier <paramref name="slot"/> of the station as <c>state stamina carried</c>.</summary>
    private static string DescribeCourier(CityState city, int slot)
    {
        Courier courier = Courier(city, slot);
        return string.Create(CultureInfo.InvariantCulture, $"{courier.State} {courier.Stamina} {courier.Carried}");
    }

    /// <summary>Courier <paramref name="slot"/> of the station; the test fails when there is none.</summary>
    private static Courier Courier(CityState city, int slot) =>
        city.Couriers.Single(candidate => candidate.StationId == GoldenScenario3.Station && candidate.Slot == slot);
}
