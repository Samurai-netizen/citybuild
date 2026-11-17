using System.Text.RegularExpressions;
using CityBuilder.BalanceSim.Strategies;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim.Tests;

/// <summary>The shared building choices (<see cref="Construction"/>), played on the real content with one number pinned.</summary>
public sealed class ConstructionTests
{
    /// <summary>
    /// Regression (M0-A, M0-68): with spare coins, Balanced ordered the only Woodcutter's Hut's upgrade, whose planks come from the Sawmill,
    /// whose logs come from that hut; the waiting hut got no workers, so the chain stopped and the Trading Post came hours late. An upgrade
    /// must not wait for goods that only the building itself can feed (<see cref="Construction.CanUpgrade"/>).
    /// </summary>
    [Fact]
    public void Balanced_WithALargerPurse_StillEarnsCoinsInTheFirstCheckIn()
    {
        using var folder = new TemporaryFolder();
        string source = File.ReadAllText(Path.Combine(TestContent.Root, "server", "content", "mvp-content.json"));
        // The purse is pinned to the 1000 coins the bug showed with, whatever the real content's purse is (mvp-3 has 1000, M0-69).
        string changed = Regex.Replace(source, "\"purse\": [0-9]+", "\"purse\": 1000");
        string file = Path.Combine(folder.Path, "content.json");
        File.WriteAllText(file, changed);
        GameContent content = ContentFileLoader.Load(file);
        Assert.Equal(1000, content.StartingCity.Purse);

        RunResult result = SimulationRun.Play(content, SimulationRunTests.Options(StrategyKind.Balanced, "every 4h", hours: 8));

        Assert.True(result.Recorder.FirstCoinsAt <= result.Options.FirstSessionSeconds, $"First coins at {result.Recorder.FirstCoinsAt}.");
        Assert.Empty(result.Recorder.Rejections);
    }
}
