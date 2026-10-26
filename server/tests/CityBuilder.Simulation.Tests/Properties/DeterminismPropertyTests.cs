using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Property 1, determinism (<c>04</c> section 10, invariant 1): the same seed gives the same operations, the same report for every operation
/// and the same city, whether the history is generated a second time or its operation list is replayed.
/// </summary>
public sealed class DeterminismPropertyTests
{
    /// <summary>Generating a seed's history a second time gives the same operations, rejected commands, reports and city.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Generate_SameSeedTwice_GivesIdenticalOperationsReportsAndCity(string contentName)
    {
        GameContent content = PropertyContent.Named(contentName);
        PropertyCheck.ForEachSeed("determinism", contentName, (first, _) =>
        {
            CityHistory second = CityGenerator.Generate(content, first.Seed);
            return FirstOperationDifference(first, second)
                ?? StructuralEquality.FirstDifference(first.Reports, second.Reports)
                ?? StructuralEquality.FirstDifference(first.City, second.City);
        });
    }

    /// <summary>Replaying a seed's operation list on a new city gives the same reports and city.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Replay_OperationsOfASeed_GivesIdenticalReportsAndCity(string contentName)
    {
        GameContent content = PropertyContent.Named(contentName);
        PropertyCheck.ForEachSeed("replay", contentName, (history, _) =>
        {
            CityHistory replayed = CityHistory.Replay(content, history.Seed, history.Operations);
            return StructuralEquality.FirstDifference(history.Reports, replayed.Reports)
                ?? StructuralEquality.FirstDifference(history.City, replayed.City);
        });
    }

    /// <summary>
    /// The first operation in which two histories differ (a missing one shows as <c>none</c>), then their counts of rejected commands; null
    /// when both match.
    /// </summary>
    private static string? FirstOperationDifference(CityHistory expected, CityHistory actual)
    {
        int count = Math.Max(expected.Operations.Count, actual.Operations.Count);
        for (int index = 0; index < count; index++)
        {
            CityOperation? expectedOperation = index < expected.Operations.Count ? expected.Operations[index] : null;
            CityOperation? actualOperation = index < actual.Operations.Count ? actual.Operations[index] : null;
            if (expectedOperation != actualOperation)
            {
                return $"operation {index + 1}: expected {expectedOperation?.ToString() ?? "none"}, actual {actualOperation?.ToString() ?? "none"}";
            }
        }

        return expected.Rejected == actual.Rejected ? null : $"rejected commands: expected {expected.Rejected}, actual {actual.Rejected}";
    }
}
