using CityBuilder.BalanceSim.Strategies;
using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim.Tests;

/// <summary>The run loop: check-ins, timeline rows, and every strategy on the real content.</summary>
public sealed class SimulationRunTests
{
    /// <summary>Every strategy with every default pattern, for <see cref="EveryStrategy_EarnsCoins_WithNoRejectedAction"/>.</summary>
    public static readonly TheoryData<StrategyKind, string> StrategiesAndPatterns = Combinations();

    /// <summary>The first check-in is longer, check-ins are cut at the run's end, and overlapping ones merge (the 1:00 one into the first, ending at its own end).</summary>
    [Fact]
    public void Sessions_UseTheFirstLength_CutAtTheEnd_AndMerge()
    {
        RunOptions options = Options(StrategyKind.Balanced, "every 1h", hours: 3) with { FirstSessionSeconds = 4_000, SessionSeconds = 600 };

        IReadOnlyList<(long Start, long End)> sessions = SimulationRun.Sessions(options);

        Assert.Equal([(0L, 4_200L), (7_200L, 7_800L)], sessions);
        Assert.Equal((0L, 3_600L), SimulationRun.Sessions(options with { RunSeconds = 3_600 })[0]);
    }

    /// <summary>
    /// Each strategy, with each default check-in pattern, starts research and earns coins in the first check-in, and the engine rejects none of
    /// its actions: the strategies check what the rules check (docs/modules/BalanceSim.md → Strategies). 24 h keeps the test fast.
    /// </summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="pattern">The check-in pattern.</param>
    [Theory]
    [MemberData(nameof(StrategiesAndPatterns))]
    public void EveryStrategy_EarnsCoins_WithNoRejectedAction(StrategyKind strategy, string pattern)
    {
        RunResult result = SimulationRun.Play(TestContent.Mvp, Options(strategy, pattern, hours: 24));

        Assert.NotEmpty(result.Recorder.ResearchStarts);
        Assert.True(result.Recorder.FirstCoinsAt <= result.Options.FirstSessionSeconds, $"First coins at {result.Recorder.FirstCoinsAt}.");
        Assert.Empty(result.Recorder.Rejections);
        Assert.Equal(24 * 3_600, result.City.Clock);
    }

    /// <summary>
    /// With a pattern that keeps the couriers busy, ResearchRush finishes every research node within three days; GreedyCoins researches only
    /// the nodes that help coins.
    /// </summary>
    [Fact]
    public void ResearchRush_FinishesTheResearch_GreedyCoinsOnlyWhatHelpsCoins()
    {
        GameContent content = TestContent.Mvp;
        RunResult rush = SimulationRun.Play(content, Options(StrategyKind.ResearchRush, "every 4h", hours: 72));
        RunResult greedy = SimulationRun.Play(content, Options(StrategyKind.GreedyCoins, "every 4h", hours: 72));
        ContentChain chain = ContentChain.Of(content);

        Assert.Equal(content.Research.Count, rush.City.CompletedResearch.Count);
        Assert.All(greedy.Recorder.ResearchStarts, start =>
            Assert.True(content.TryGetResearch(start.Research, out ResearchDefinition? node) && chain.HelpsCoins(node)));
    }

    /// <summary>The timeline has a row at the start, at every sample time and at every check-in start, in time order, each once.</summary>
    [Fact]
    public void Timeline_HasRowsAtSamplesAndCheckIns()
    {
        RunOptions options = Options(StrategyKind.Balanced, "every 90min", hours: 6) with { SampleSeconds = 3_600 };

        RunResult result = SimulationRun.Play(TestContent.Mvp, options);

        long[] times = [.. result.Recorder.TimelineRows.Select(row => long.Parse(row[0], System.Globalization.CultureInfo.InvariantCulture))];
        long[] expected = [.. new long[] { 0, 3_600, 5_400, 7_200, 10_800, 14_400, 16_200, 18_000, 21_600 }];
        Assert.Equal(expected, times);
        Assert.All(result.Recorder.TimelineRows, row => Assert.Equal(result.Recorder.TimelineHeader.Count, row.Length));
    }

    /// <summary>The measured time is the whole run, and the producer time never exceeds it per producer.</summary>
    [Fact]
    public void Measurements_CoverTheWholeRun()
    {
        RunResult result = SimulationRun.Play(TestContent.Mvp, Options(StrategyKind.GreedyCoins, "08:00,13:00,20:00 daily", hours: 24));

        Assert.Equal(24 * 3_600, result.Recorder.MeasuredSeconds);
        Assert.True(result.Recorder.IdleWorkerSeconds <= result.Recorder.ProvidedWorkerSeconds);
        Assert.NotEmpty(result.Recorder.ProducerSeconds);
    }

    /// <summary>Run settings with the command line's defaults.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="pattern">The check-in pattern.</param>
    /// <param name="hours">The run length.</param>
    /// <returns>The settings.</returns>
    internal static RunOptions Options(StrategyKind strategy, string pattern, long hours) =>
        new(strategy, SessionPattern.Parse(pattern), hours * 3_600, 1_800, 300, 900, 300);

    /// <summary>Every strategy × every default pattern.</summary>
    /// <returns>The theory data.</returns>
    private static TheoryData<StrategyKind, string> Combinations()
    {
        var data = new TheoryData<StrategyKind, string>();
        foreach (StrategyKind strategy in Enum.GetValues<StrategyKind>())
        {
            foreach (string pattern in CommandLine.DefaultPatterns)
            {
                data.Add(strategy, pattern);
            }
        }

        return data;
    }
}
