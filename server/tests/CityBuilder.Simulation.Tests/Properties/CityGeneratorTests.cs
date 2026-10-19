using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The generator is only as good as the cities it reaches: over the property seeds it must reach every command, upgrades, research,
/// locked buildings, chains, frozen cycles and gaps beyond the production cap. The floors are well below what the seeds reach today (the
/// message prints the counts), so a change to the generator that stops reaching a situation fails here instead of silently weakening the
/// properties.
/// </summary>
public sealed class CityGeneratorTests
{
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Generate_PropertySeeds_ReachEveryCommandAndSituation(string contentName)
    {
        GameContent content = PropertyContent.Named(contentName);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int seed = 0; seed < PropertyContent.Seeds; seed++)
        {
            CityHistory history = CityGenerator.Generate(content, seed);
            foreach (CityOperation operation in history.Operations)
            {
                Count(counts, operation.GetType().Name, true);
            }

            Count(counts, "seeds with a gap beyond the cap", history.Reports.Any(report => report.ProductionSeconds < report.ElapsedSeconds));
            Count(counts, "seeds with an upgrade done", history.City.Buildings.Any(building => building.Level > 1));
            Count(counts, "seeds with research done", history.City.CompletedResearch.Count > 0);
            Count(counts, "seeds with a locked building placed", history.City.Buildings.Any(building => IsLockedAtStart(content, building)));
            Count(counts, "seeds with a cycle that used inputs", history.Reports.Any(report => report.Consumed.Count > 0));
            Count(counts, "seeds ending with a frozen cycle", history.City.Buildings.Any(IsFrozen));
        }

        string observed = string.Join(", ", counts.Select(count => $"{count.Key} {count.Value}"));
        foreach (string kind in (string[])["PlaceBuilding", "UpgradeBuilding", "AssignWorkers", "StartResearch", "Advance"])
        {
            Assert.True(counts.GetValueOrDefault(kind) >= 20, $"Too few accepted {kind} operations. Observed: {observed}");
        }

        AssertAtLeast(counts, "seeds with a gap beyond the cap", 100, observed);
        AssertAtLeast(counts, "seeds with an upgrade done", 20, observed);
        AssertAtLeast(counts, "seeds with research done", 100, observed);
        AssertAtLeast(counts, "seeds with a locked building placed", 5, observed);
        AssertAtLeast(counts, "seeds with a cycle that used inputs", 5, observed);
        AssertAtLeast(counts, "seeds ending with a frozen cycle", 5, observed);
    }

    [Fact]
    public void Generate_RejectedCommands_AreLeftOutAndCounted()
    {
        CityHistory history = CityGenerator.Generate(PropertyContent.Named("golden-v1"), seed: 7);

        // Every listed operation was accepted (a replay accepts each one again), and the seed tried more commands than it kept.
        Assert.Equal(history.Operations.Count, CityHistory.Replay(PropertyContent.Named("golden-v1"), 7, history.Operations).Reports.Count);
        Assert.True(history.Rejected > 0, history.Describe());
    }

    [Fact]
    public void Describe_History_PrintsTheSeedAndNumberedOperations()
    {
        var history = new CityHistory(
            seed: 3,
            city: CityFactory.CreateStarterCity(PropertyContent.Named("fixture")),
            operations: [new CityOperation.PlaceBuilding("hut", 1, 2), new CityOperation.Advance(30), new CityOperation.AssignWorkers(1, 0)],
            reports: [],
            rejected: 0);

        Assert.Equal(
            "seed 3, 3 operations from the starting city:\n  1. PlaceBuilding hut (1,2)\n  2. Advance 30\n  3. AssignWorkers 1 0",
            history.Describe());
    }

    [Fact]
    public void Gap_ManyDraws_StayWithinZeroAndFourCapsAndReachBothSidesOfTheCap()
    {
        GameContent content = PropertyContent.Named("golden-v1");
        long cap = content.Rules.MaxOfflineProductionSeconds;
        var random = new SeededRandom(1);

        long[] gaps = [.. Enumerable.Range(0, 1000).Select(_ => CityGenerator.Gap(random, content))];

        Assert.All(gaps, gap => Assert.InRange(gap, 0, 4 * cap));
        Assert.Contains(gaps, gap => gap <= 60);
        Assert.Contains(gaps, gap => gap > cap);
    }

    private static void Count(SortedDictionary<string, int> counts, string key, bool happened)
    {
        if (happened)
        {
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
    }

    private static void AssertAtLeast(SortedDictionary<string, int> counts, string key, int floor, string observed) =>
        Assert.True(counts.GetValueOrDefault(key) >= floor, $"Fewer than {floor} {key}. Observed: {observed}");

    private static bool IsLockedAtStart(GameContent content, Building building) =>
        content.TryGetBuilding(building.TypeId, out BuildingDefinition? type) && !type.IsAvailableAtStart;

    private static bool IsFrozen(Building building) => building.Phase == BuildingPhase.Producing && building.PhaseEndsAt is null;
}
