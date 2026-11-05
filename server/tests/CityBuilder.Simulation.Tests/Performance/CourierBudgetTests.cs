using System.Diagnostics;
using System.Globalization;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.Performance.CourierWorstCase;

namespace CityBuilder.Simulation.Tests.Performance;

/// <summary>
/// The courier half of the performance budget of <c>04</c> section 12 (CR-0008-T1 part I): the worst case of <see cref="CourierWorstCase"/>
/// settled once after a long absence (8 h 20 min, the player away from 300 s), so every one of the 152 couriers walks until its stamina is
/// gone and rests: 7 276 arrivals each, 1 105 952 in all. The exact end state proves every walk ran, so a run can't be fast by stopping early.
/// The timing asserts the CI limit and prints what it measured; <c>04</c> section 12 has the target. They run in the collection
/// <see cref="RunsAlone"/>.
/// </summary>
/// <param name="output">Where the timing test prints the times it measured, in every run, pass or fail.</param>
[Collection(nameof(RunsAlone))]
public sealed class CourierBudgetTests(ITestOutputHelper output)
{
    /// <summary>The absence the settlement covers: 30 000 s, past the grace (300 s), the stamina (28 800 s) and the last walk home.</summary>
    private const long _absence = 30_000;

    /// <summary>The second every courier tires: the arrival at which the 28 800 s after the grace are spent (a multiple of 4 s after 1000).</summary>
    private const long _tiredAt = Clock + Grace + Stamina;

    /// <summary>How many timed runs the median is taken over, after one warm-up run.</summary>
    private const int _measuredRuns = 5;

    /// <summary>
    /// The limit on the median time per settlement: a generous one for slow and busy CI machines, which run Release (<c>04</c> section 12's
    /// target is 500 ms; measured 308 ms, CR-0008-T1 part I); a Debug build, as developers run the suite, gets three times as much (it
    /// measured 1.8 s).
    /// </summary>
#if DEBUG
    private const long _ciLimitMicroseconds = 6_000_000;
#else
    private const long _ciLimitMicroseconds = 2_000_000;
#endif

    /// <summary>The built city has the shape of the case: 26 houses and 38 stations at level 2 on all 64 tiles, 152 couriers, each with a route.</summary>
    [Fact]
    public void Build_WorstCase_Has152CouriersOnRoutesOfOneStep()
    {
        CityState city = Build();

        Assert.Equal(64, city.Buildings.Count);
        Assert.Equal(Stations, city.Buildings.Count(building => building.TypeId == new BuildingTypeId("courier_station")));
        Assert.All(city.Buildings, building => Assert.Equal(2, building.Level));
        Assert.Equal(Stations * CouriersPerStation, city.Couriers.Count);
        Assert.Equal(city.Couriers.Count, city.Routes.Count);
        Assert.True(CityQueries.FreeWorkers(city, CaseContent) >= 0);
    }

    /// <summary>
    /// One settlement after the absence: every courier spent all its stamina, tired at the same arrival (30 100, at the neighbour), walked
    /// home and rests; the report names all 152 and the 28 800 s; no goods moved (there are none).
    /// </summary>
    [Fact]
    public void Advance_LongAbsence_RunsEveryCourierToTheEndOfItsStamina()
    {
        CityState city = Build();

        AssertSettled(city, Simulator.Advance(city, CaseContent, _absence, Clock + Grace));
    }

    /// <summary>The median of five timed settlements, after a warm-up, stays within the CI limit; the times are printed.</summary>
    [Fact]
    public void Advance_WorstCase_StaysWithinTheCiLimit()
    {
        Measure();
        long[] times = new long[_measuredRuns];
        for (int run = 0; run < _measuredRuns; run++)
        {
            times[run] = Measure();
        }

        output.WriteLine(Times(times));
        Assert.True(Median(times) <= _ciLimitMicroseconds, Times(times));
    }

    /// <summary>One run on a new city: the settlement's time in microseconds, its result checked.</summary>
    private static long Measure()
    {
        CityState city = Build();
        var watch = Stopwatch.StartNew();
        SettlementReport report = Simulator.Advance(city, CaseContent, _absence, Clock + Grace);
        long ticks = watch.ElapsedTicks;

        AssertSettled(city, report);
        return ticks * 1_000_000 / Stopwatch.Frequency;
    }

    /// <summary>The exact end of the settlement of <see cref="Advance_LongAbsence_RunsEveryCourierToTheEndOfItsStamina"/>.</summary>
    private static void AssertSettled(CityState city, SettlementReport report)
    {
        Assert.Equal(Clock + _absence, city.Clock);
        Assert.All(city.Couriers, courier => Assert.Equal((CourierState.Resting, 0L), (courier.State, courier.Stamina)));
        Assert.Equal(Stamina, report.CourierSeconds);
        Assert.Equal(Stations * CouriersPerStation, report.TiredCouriers.Count);
        Assert.All(report.TiredCouriers, tired => Assert.Equal(_tiredAt, tired.At));
        Assert.Empty(report.Moved);
        Assert.Empty(report.Produced);
    }

    /// <summary>The median of an odd number of values.</summary>
    private static long Median(long[] values) => values.Order().ElementAt(values.Length / 2);

    /// <summary>A line with the median, minimum and maximum of the runs, the CI limit and the target, in invariant formatting.</summary>
    private static string Times(long[] microseconds) => string.Create(
        CultureInfo.InvariantCulture,
        $"Courier worst case (1,105,952 arrivals), {_measuredRuns} runs: median {Median(microseconds):N0} µs, " +
        $"min {microseconds.Min():N0} µs, max {microseconds.Max():N0} µs (CI limit {_ciLimitMicroseconds:N0} µs, target 500,000 µs)");
}
