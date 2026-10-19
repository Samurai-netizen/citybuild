using System.Diagnostics;
using System.Globalization;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.Performance.WorstCaseCity;

namespace CityBuilder.Simulation.Tests.Performance;

/// <summary>
/// The performance budget of <c>04</c> section 12: the worst-case city (<see cref="WorstCaseCity"/>) advanced by the full 8-hour production
/// cap, then by 7 days (8 hours of production and a dormant tail with only timers). The exact counts prove that every cycle ran, so a run
/// can't be fast by stalling early. The timing asserts a generous limit for CI and prints what it measured; the target on a dev machine
/// is 50 ms. They run in the collection <see cref="RunsAlone"/>.
/// </summary>
[Collection(nameof(RunsAlone))]
public sealed class PerformanceBudgetTests(ITestOutputHelper output)
{
    private const long _eightHours = 8 * 3600;

    private const long _sevenDays = 7 * 24 * 3600;

    private const int _measuredRuns = 5;

    // Ten times the dev-machine target of 04 section 12, for slow and busy CI machines.
    private const long _ciLimitMicroseconds = 500_000;

    [Fact]
    public void Build_WorstCase_StaffsSixtyFourProducersWhoseCyclesEndOnEverySecondOfTen()
    {
        CityState city = Build();

        Building[] producers = city.Buildings.Where(building => building.TypeId != House).ToArray();
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
        Assert.Equal(_eightHours, WorstCaseContent.Rules.MaxOfflineProductionSeconds);
    }

    [Fact]
    public void Advance_EightHours_CompletesAndStartsEveryCycleOfTheCap()
    {
        CityState city = Build();

        AssertEightHours(Simulator.Advance(city, WorstCaseContent, _eightHours));
    }

    [Fact]
    public void Advance_SevenDaysAfterEightHours_RunsTheCapThenFinishesTheCyclesAndTheResearchInTheTail()
    {
        CityState city = Build();
        Simulator.Advance(city, WorstCaseContent, _eightHours);

        AssertSevenDays(city, Simulator.Advance(city, WorstCaseContent, _sevenDays));
    }

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

    // One run on a new city: the times of the two advances in microseconds, each report checked.
    private static (long EightHours, long SevenDays) Measure()
    {
        CityState city = Build();
        var watch = Stopwatch.StartNew();
        SettlementReport eightHours = Simulator.Advance(city, WorstCaseContent, _eightHours);
        long eightHoursTicks = watch.ElapsedTicks;
        watch.Restart();
        SettlementReport sevenDays = Simulator.Advance(city, WorstCaseContent, _sevenDays);
        long sevenDaysTicks = watch.ElapsedTicks;

        AssertEightHours(eightHours);
        AssertSevenDays(city, sevenDays);
        return (Microseconds(eightHoursTicks), Microseconds(sevenDaysTicks));
    }

    // Every producer's next cycle ends within 10 s of the start, so 8 hours hold 2,880 completions each, and each starts the next cycle
    // (the last one at the cap's last second at the latest): 64 × 2,880 = 184,320 completions.
    private static void AssertEightHours(SettlementReport report)
    {
        Assert.Equal(_eightHours, report.ProductionSeconds);
        Assert.Equal(2, report.Produced.Count);
        Assert.Equal(32 * 2880 * 2, report.Produced[Ore]);
        Assert.Equal(32 * 2880, report.Produced[Ingots]);
        Assert.Single(report.Consumed);
        Assert.Equal(32 * 2880, report.Consumed[Ore]);
        Assert.Empty(report.CompletedBuildings);
        Assert.Empty(report.CompletedResearch);
    }

    // The same 2,880 completions and starts in the production window, then each producer's last cycle completes in the dormant tail
    // (2,881 completions), nothing starts there, and the 2-day research completes: the tail adds only timer events.
    private static void AssertSevenDays(CityState city, SettlementReport report)
    {
        Assert.Equal(_eightHours, report.ProductionSeconds);
        Assert.Equal(2, report.Produced.Count);
        Assert.Equal(32 * 2881 * 2, report.Produced[Ore]);
        Assert.Equal(32 * 2881, report.Produced[Ingots]);
        Assert.Single(report.Consumed);
        Assert.Equal(32 * 2880, report.Consumed[Ore]);
        Assert.Empty(report.CompletedBuildings);
        Assert.Equal([DeepVaults], report.CompletedResearch);
        Assert.All(city.Buildings, building => Assert.Equal(BuildingPhase.Idle, building.Phase));
    }

    private static long Microseconds(long ticks) => ticks * 1_000_000 / Stopwatch.Frequency;

    private static long Median(long[] values) => values.Order().ElementAt(values.Length / 2);

    private static string Times(string advance, long[] microseconds) => string.Create(
        CultureInfo.InvariantCulture,
        $"{advance} of the worst case, {_measuredRuns} runs: median {Median(microseconds):N0} µs, " +
        $"min {microseconds.Min():N0} µs, max {microseconds.Max():N0} µs (CI limit {_ciLimitMicroseconds:N0} µs, target 50,000 µs)");
}
