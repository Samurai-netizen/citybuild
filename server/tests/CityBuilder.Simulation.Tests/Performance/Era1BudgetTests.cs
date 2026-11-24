using System.Diagnostics;
using System.Globalization;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.Performance.Era1WorstCase;

namespace CityBuilder.Simulation.Tests.Performance;

/// <summary>
/// The performance budget of <c>04</c> section 12 over the Era I content (M1-02 acceptance: "13 building types on the largest map"): the
/// city of <see cref="Era1WorstCase"/> settled with the player away from 300 s, first for 8 hours, then for 7 days. The 8 hours run every
/// chain (every good is made and every carried good is moved, no courier tires yet); the 7 days run every courier to the end of its stamina.
/// The timing asserts the CI limit of <see cref="CourierBudgetTests"/> and prints what it measured; <c>04</c> section 12 has the target and
/// the measured numbers. They run in the collection <see cref="RunsAlone"/>.
/// </summary>
/// <param name="output">Where the timing test prints the times it measured, in every run, pass or fail.</param>
[Collection(nameof(RunsAlone))]
public sealed class Era1BudgetTests(ITestOutputHelper output)
{
    /// <summary>The first advance, 8 hours in seconds.</summary>
    private const long _eightHours = 8 * 3600;

    /// <summary>A long absence after it, 7 days in seconds.</summary>
    private const long _sevenDays = 7 * 24 * 3600;

    /// <summary>How many timed runs the median is taken over, after one warm-up run.</summary>
    private const int _measuredRuns = 5;

    /// <summary>The limit on the median time per advance: the one of <see cref="CourierBudgetTests"/> (Release 2 s, Debug three times as much).</summary>
#if DEBUG
    private const long _ciLimitMicroseconds = 6_000_000;
#else
    private const long _ciLimitMicroseconds = 2_000_000;
#endif

    /// <summary>The built city has the shape of the case: all 64 tiles, all 13 types at their highest level, 108 couriers on routes, workers to spare.</summary>
    [Fact]
    public void Build_Era1WorstCase_FillsTheMapWithEveryTypeAtItsHighestLevel()
    {
        CityState city = Build();

        Assert.Equal(CaseContent.Rules.GridWidth * CaseContent.Rules.GridHeight, city.Buildings.Count);
        Assert.Equal(CaseContent.Buildings.Count, city.Buildings.Select(building => building.TypeId).Distinct().Count());
        Assert.Equal(13, CaseContent.Buildings.Count);
        Assert.All(city.Buildings, building =>
            Assert.Equal(CityQueries.MaxLevel(city, CaseContent, building.TypeId), building.Level));
        Assert.Equal(Stations * CouriersPerStation, city.Couriers.Count);
        Assert.Equal(city.Couriers.Count, city.Routes.Count);
        Assert.Equal(10, CityQueries.FreeWorkers(city, CaseContent));
    }

    /// <summary>8 hours away: every good of the three chains is made and every carried good is moved; no courier tires yet.</summary>
    [Fact]
    public void Advance_EightHoursAway_RunsEveryChain()
    {
        CityState city = Build();

        AssertEightHours(Simulator.Advance(city, CaseContent, _eightHours, Clock + Grace));
    }

    /// <summary>7 days away after it: every courier walks until its stamina is gone and rests.</summary>
    [Fact]
    public void Advance_SevenDaysAwayAfterEightHours_RunsEveryCourierToTheEndOfItsStamina()
    {
        CityState city = Build();
        Simulator.Advance(city, CaseContent, _eightHours, Clock + Grace);

        AssertSevenDays(city, Simulator.Advance(city, CaseContent, _sevenDays, city.Clock));
    }

    /// <summary>The median of five timed runs of both advances, after a warm-up, stays within the CI limit; the times are printed.</summary>
    [Fact]
    public void Advance_Era1WorstCase_StaysWithinTheCiLimit()
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
        SettlementReport eightHours = Simulator.Advance(city, CaseContent, _eightHours, Clock + Grace);
        long eightHoursTicks = watch.ElapsedTicks;
        watch.Restart();
        SettlementReport sevenDays = Simulator.Advance(city, CaseContent, _sevenDays, city.Clock);
        long sevenDaysTicks = watch.ElapsedTicks;

        AssertEightHours(eightHours);
        AssertSevenDays(city, sevenDays);
        return (Microseconds(eightHoursTicks), Microseconds(sevenDaysTicks));
    }

    /// <summary>The 8-hour report: the whole window, every good made, every carried good moved, no courier tired.</summary>
    private static void AssertEightHours(SettlementReport report)
    {
        Assert.Equal(_eightHours, report.ElapsedSeconds);
        Assert.All(Goods, good => Assert.True(report.Produced.GetValueOrDefault(new ItemId(good)) > 0, good + " is made"));
        Assert.All(Carried, good => Assert.True(report.Moved.GetValueOrDefault(new ItemId(good)) > 0, good + " is moved"));
        Assert.Empty(report.TiredCouriers);
    }

    /// <summary>The 7-day report: every courier tired once and now rests with no stamina.</summary>
    private static void AssertSevenDays(CityState city, SettlementReport report)
    {
        Assert.Equal(_sevenDays, report.ElapsedSeconds);
        Assert.Equal(Stations * CouriersPerStation, report.TiredCouriers.Count);
        Assert.All(city.Couriers, courier => Assert.Equal((CourierState.Resting, 0L), (courier.State, courier.Stamina)));
    }

    /// <summary>Stopwatch ticks in microseconds.</summary>
    private static long Microseconds(long ticks) => ticks * 1_000_000 / Stopwatch.Frequency;

    /// <summary>The median of an odd number of values.</summary>
    private static long Median(long[] values) => values.Order().ElementAt(values.Length / 2);

    /// <summary>A line with the median, minimum and maximum of one advance's runs, the CI limit and the target, in invariant formatting.</summary>
    private static string Times(string advance, long[] microseconds) => string.Create(
        CultureInfo.InvariantCulture,
        $"{advance} of the Era I worst case, {_measuredRuns} runs: median {Median(microseconds):N0} µs, " +
        $"min {microseconds.Min():N0} µs, max {microseconds.Max():N0} µs (CI limit {_ciLimitMicroseconds:N0} µs, target 500,000 µs)");
}
