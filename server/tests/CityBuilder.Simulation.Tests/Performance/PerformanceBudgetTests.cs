using System.Diagnostics;
using System.Globalization;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.Performance.WorstCaseCity;

namespace CityBuilder.Simulation.Tests.Performance;

/// <summary>
/// The performance budget of <c>04</c> section 12 for producers: the worst-case city (<see cref="WorstCaseCity"/>) advanced by 8 hours, then
/// by 7 days, both with production for every second (no offline cap since CR-0008-T1 part H): a long absence ends when the stores are full
/// (the mines' 12,000 ore, the smelters' ore used up), as in the game, so it costs about what 8 hours do. Part I adds the courier worst
/// case and sets the final number. The exact counts prove that every cycle ran, so a run can't be fast by stalling early. The timing asserts a generous limit for CI and prints what it measured; the target on a dev machine
/// is 50 ms. They run in the collection <see cref="RunsAlone"/>.
/// </summary>
/// <param name="output">Where the timing test prints the times it measured, in every run, pass or fail.</param>
[Collection(nameof(RunsAlone))]
public sealed class PerformanceBudgetTests(ITestOutputHelper output)
{
    /// <summary>The first advance, 8 hours in seconds.</summary>
    private const long _eightHours = 8 * 3600;

    /// <summary>A long absence, 7 days in seconds, all of them with production.</summary>
    private const long _sevenDays = 7 * 24 * 3600;

    /// <summary>How many timed runs the median is taken over, after one warm-up run.</summary>
    private const int _measuredRuns = 5;

    /// <summary>The limit on the median time per advance: ten times the dev-machine target of <c>04</c> section 12, for slow and busy CI machines.</summary>
    private const long _ciLimitMicroseconds = 500_000;

    /// <summary>The built city has the shape the budget needs: 64 producers, half mines, each producing with 2 workers, their ends spread over all ten seconds.</summary>
    [Fact]
    public void Build_WorstCase_StaffsSixtyFourProducersWhoseCyclesEndOnEverySecondOfTen()
    {
        CityState city = Build();

        Building[] producers = city.Buildings.Where(building => building.TypeId == Mine || building.TypeId == Smelter).ToArray();
        Assert.Equal(Producers, producers.Length);
        Assert.Equal(Producers / 2, producers.Count(building => building.TypeId == Mine));
        Assert.All(producers, building =>
        {
            Assert.Equal(BuildingPhase.Producing, building.Phase);
            Assert.Equal(2, building.AssignedWorkers);
            Assert.Equal(CycleSeconds * building.AssignedWorkers, building.CycleLabor);
            Assert.InRange(building.PhaseEndsAt!.Value, city.Clock + 1, city.Clock + CycleSeconds);
        });
        Assert.Equal(CycleSeconds, producers.Select(building => building.PhaseEndsAt!.Value).Distinct().Count());
    }

    /// <summary>An 8-hour advance completes and starts every cycle of the 8 hours (exact counts, so a fast run can't be a stalled one).</summary>
    [Fact]
    public void Advance_EightHours_CompletesAndStartsEveryCycle()
    {
        CityState city = Build();

        AssertEightHours(Simulator.Advance(city, WorstCaseContent, _eightHours));
    }

    /// <summary>A 7-day advance after it runs the mines until their stores are full and the smelters until their ore runs out, and the research completes.</summary>
    [Fact]
    public void Advance_SevenDaysAfterEightHours_RunsTheProducersUntilTheirStoresStopThem()
    {
        CityState city = Build();
        Simulator.Advance(city, WorstCaseContent, _eightHours);
        (long mineOre, long smelterOre) = OreOf(city);

        AssertSevenDays(city, Simulator.Advance(city, WorstCaseContent, _sevenDays), mineOre, smelterOre);
    }

    /// <summary>The median of five timed runs of both advances, after a warm-up, stays within the CI limit; the times are printed.</summary>
    [Fact]
    public void Advance_WorstCase_StaysWithinTheCiLimit()
    {
        Measure();
        long[] eightHours = new long[_measuredRuns];
        long[] sevenDays = new long[_measuredRuns];
        for (int run = 0; run < _measuredRuns; run++)
        {
            (eightHours[run], sevenDays[run]) = Measure();
        }

        output.WriteLine(Times("8 h", eightHours));
        output.WriteLine(Times("7 d", sevenDays));
        Assert.True(Median(eightHours) <= _ciLimitMicroseconds, Times("8 h", eightHours));
        Assert.True(Median(sevenDays) <= _ciLimitMicroseconds, Times("7 d", sevenDays));
    }

    /// <summary>One run on a new city: the times of the two advances in microseconds, each report checked.</summary>
    private static (long EightHours, long SevenDays) Measure()
    {
        CityState city = Build();
        var watch = Stopwatch.StartNew();
        SettlementReport eightHours = Simulator.Advance(city, WorstCaseContent, _eightHours);
        long eightHoursTicks = watch.ElapsedTicks;
        (long mineOre, long smelterOre) = OreOf(city);
        watch.Restart();
        SettlementReport sevenDays = Simulator.Advance(city, WorstCaseContent, _sevenDays);
        long sevenDaysTicks = watch.ElapsedTicks;

        AssertEightHours(eightHours);
        AssertSevenDays(city, sevenDays, mineOre, smelterOre);
        return (Microseconds(eightHoursTicks), Microseconds(sevenDaysTicks));
    }

    /// <summary>
    /// The exact report of the 8-hour advance. Every producer's next cycle ends within 10 s of the start, so 8 hours hold 2,880 completions
    /// each, and each starts the next cycle: 64 × 2,880 = 184,320 completions.
    /// </summary>
    private static void AssertEightHours(SettlementReport report)
    {
        Assert.Equal(_eightHours, report.ElapsedSeconds);
        Assert.Equal(2, report.Produced.Count);
        Assert.Equal(32 * 2880 * 2, report.Produced[Ore]);
        Assert.Equal(32 * 2880, report.Produced[Ingots]);
        Assert.Single(report.Consumed);
        Assert.Equal(32 * 2880, report.Consumed[Ore]);
        Assert.Empty(report.CompletedBuildings);
        Assert.Empty(report.CompletedResearch);
    }

    /// <summary>
    /// The exact report of the 7-day advance: each mine runs on until its store holds <see cref="MineOreLimit"/> (2 ore a cycle, the limit
    /// even, so it fills exactly), each smelter finishes its running cycle and then starts one per ore left in its store (about 3,100 each);
    /// then every producer stands idle, about 9 hours in, and the 2-day research completes. About 200,000 completions, like 8 hours.
    /// </summary>
    /// <param name="city">The city after the 7-day advance.</param>
    /// <param name="report">The 7-day advance's report.</param>
    /// <param name="mineOre">The ore in the mines' stores before the 7-day advance, over all of them (<see cref="OreOf"/>).</param>
    /// <param name="smelterOre">The ore in the smelters' stores before the 7-day advance, over all of them.</param>
    private static void AssertSevenDays(CityState city, SettlementReport report, long mineOre, long smelterOre)
    {
        Assert.InRange(smelterOre, 32 * 3000L, 32 * 3119L);
        Assert.Equal(_sevenDays, report.ElapsedSeconds);
        Assert.Equal(2, report.Produced.Count);
        Assert.Equal((32 * MineOreLimit) - mineOre, report.Produced[Ore]);
        Assert.Equal(smelterOre + 32, report.Produced[Ingots]);
        Assert.Single(report.Consumed);
        Assert.Equal(smelterOre, report.Consumed[Ore]);
        Assert.Empty(report.CompletedBuildings);
        Assert.Equal([DeepVaults], report.CompletedResearch);
        Assert.All(city.Buildings, building => Assert.Equal(BuildingPhase.Idle, building.Phase));
    }

    /// <summary>The ore in the mines' stores and in the smelters' stores, each over all of them: what decides how long each runs on.</summary>
    private static (long Mines, long Smelters) OreOf(CityState city) => (
        city.Buildings.Where(building => building.TypeId == Mine).Sum(building => building.GetStock(Ore)),
        city.Buildings.Where(building => building.TypeId == Smelter).Sum(building => building.GetStock(Ore)));

    /// <summary>Stopwatch ticks in microseconds.</summary>
    private static long Microseconds(long ticks) => ticks * 1_000_000 / Stopwatch.Frequency;

    /// <summary>The median of an odd number of values.</summary>
    private static long Median(long[] values) => values.Order().ElementAt(values.Length / 2);

    /// <summary>A line with the median, minimum and maximum of one advance's runs, the CI limit and the target, in invariant formatting.</summary>
    private static string Times(string advance, long[] microseconds) => string.Create(
        CultureInfo.InvariantCulture,
        $"{advance} of the worst case, {_measuredRuns} runs: median {Median(microseconds):N0} µs, " +
        $"min {microseconds.Min():N0} µs, max {microseconds.Max():N0} µs (CI limit {_ciLimitMicroseconds:N0} µs, target 50,000 µs)");
}
