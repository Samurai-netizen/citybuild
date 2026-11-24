using System.Diagnostics;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.IntegrationTests.EndToEnd;

/// <summary>
/// The M1-03 acceptance run: the charter player's script (<see cref="CharterPlayer"/>) replayed for its first 7 days on the Era I map, as the
/// server settles it (one advance per request, then a zero-length advance per act), with every expansion of the map (each research that
/// opens land or unlocks the bridge) completing inside those 7 days. Every advance stays inside the CI limit of the performance budget
/// (<c>04</c> section 12: Release 2 s, Debug three times as much), so opening land never makes a settle expensive.
/// </summary>
public sealed class CharterBudgetTests
{
    /// <summary>The length of the run: 7 days in seconds.</summary>
    private const long _sevenDays = 7 * 24 * 3600;

    /// <summary>The CI limit on one advance, in microseconds (the one of the engine's budget tests).</summary>
#if DEBUG
    private const long _ciLimitMicroseconds = 6_000_000;
#else
    private const long _ciLimitMicroseconds = 2_000_000;
#endif

    /// <summary>
    /// Replaying the script's first 7 days (and settling the rest of the 7 days with the player away) completes every map expansion, and the
    /// slowest single advance stays inside the CI limit.
    /// </summary>
    [Fact]
    public void Replay_SevenDaysWithEveryExpansion_EachAdvanceStaysInsideTheBudget()
    {
        GameContent content = CharterPlayer.Content;
        CityState city = CityFactory.CreateStarterCity(content);
        long slowest = 0;
        long? lastRequest = null;
        foreach ((long time, LoopAct[] acts) in CharterPlayer.Script.Where(step => step.Time <= _sevenDays))
        {
            slowest = Math.Max(slowest, Timed(() => Simulator.Advance(city, content, time - city.Clock, lastRequest + CharterPlayer.GraceSeconds)));
            foreach (LoopAct act in acts)
            {
                Assert.True(EraOneLoop.Mutate(city, content, act).IsOk, $"t={time} {act}");
                slowest = Math.Max(slowest, Timed(() => Simulator.Advance(city, content, 0)));
            }

            lastRequest = time;
        }

        slowest = Math.Max(slowest, Timed(() => Simulator.Advance(city, content, _sevenDays - city.Clock, lastRequest + CharterPlayer.GraceSeconds)));

        Assert.Equal(_sevenDays, city.Clock);
        string[] expansions = [.. content.Research
            .Where(node => node.Effects.Any(effect => effect is OpenLandEffect or UnlockBridgeEffect))
            .Select(node => node.Id.Value)];
        Assert.Equal(["bridge_building", "land_survey", "open_fields"], expansions.Order());
        Assert.All(expansions, id => Assert.Contains(new ResearchId(id), city.CompletedResearch));
        Assert.True(slowest <= _ciLimitMicroseconds, $"slowest advance {slowest:N0} µs, limit {_ciLimitMicroseconds:N0} µs");
    }

    /// <summary>Runs <paramref name="advance"/> and returns how long it took, in microseconds.</summary>
    /// <param name="advance">The advance to time.</param>
    /// <returns>The elapsed microseconds.</returns>
    private static long Timed(Action advance)
    {
        long start = Stopwatch.GetTimestamp();
        advance();
        return (Stopwatch.GetTimestamp() - start) * 1_000_000 / Stopwatch.Frequency;
    }
}
