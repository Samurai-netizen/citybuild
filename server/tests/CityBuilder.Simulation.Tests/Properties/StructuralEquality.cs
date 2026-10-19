using System.Globalization;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Compares two cities, or two report totals, field by field and names the first field that differs, so a failing property says what went
/// wrong instead of "not equal". A city's fields come in a fixed order: clock, era, next building id, the stock per item, every field of every
/// building by id, the completed research and the active research. It reads only public members, the way the server will.
/// </summary>
internal static class StructuralEquality
{
    private const string _absent = "absent";

    /// <summary>The first field in which <paramref name="actual"/> differs from <paramref name="expected"/>, or null when they are equal.</summary>
    public static string? FirstDifference(CityState expected, CityState actual) => FirstDifference(Fields(expected), Fields(actual));

    /// <summary>The first field in which <paramref name="actual"/> differs from <paramref name="expected"/>, or null when they are equal.</summary>
    public static string? FirstDifference(ReportTotals expected, ReportTotals actual) => FirstDifference(expected.Fields(), actual.Fields());

    /// <summary>The first report, by position, that differs, with its first differing field; or null when the lists are equal.</summary>
    public static string? FirstDifference(IReadOnlyList<SettlementReport> expected, IReadOnlyList<SettlementReport> actual)
    {
        if (expected.Count != actual.Count)
        {
            return $"report count: expected {Text(expected.Count)}, actual {Text(actual.Count)}";
        }

        for (int index = 0; index < expected.Count; index++)
        {
            string? difference = FirstDifference(ReportTotals.Sum(expected[index]), ReportTotals.Sum(actual[index]));
            if (difference is not null)
            {
                return $"report {Text(index + 1)} {difference}";
            }
        }

        return null;
    }

    /// <summary>Every stored value of <paramref name="city"/>, as a path and a value, in the order of the class summary.</summary>
    public static IReadOnlyList<(string Path, string Value)> Fields(CityState city)
    {
        var fields = new List<(string Path, string Value)>
        {
            ("clock", Text(city.Clock)),
            ("era", Text(city.Era)),
            ("nextBuildingId", Text(city.NextBuildingId)),
        };
        fields.AddRange(city.Inventory.OrderBy(stock => stock.Key).Select(stock => ($"inventory[{stock.Key}]", Text(stock.Value))));
        foreach (Building building in city.Buildings)
        {
            string prefix = $"building {Text(building.Id)} ";
            fields.Add((prefix + "type", building.TypeId.ToString()));
            fields.Add((prefix + "tile", $"({Text(building.X)},{Text(building.Y)})"));
            fields.Add((prefix + "level", Text(building.Level)));
            fields.Add((prefix + "assignedWorkers", Text(building.AssignedWorkers)));
            fields.Add((prefix + "phase", building.Phase.ToString()));
            fields.Add((prefix + "phaseStartedAt", Text(building.PhaseStartedAt)));
            fields.Add((prefix + "phaseEndsAt", building.PhaseEndsAt.HasValue ? Text(building.PhaseEndsAt.Value) : "null"));
            fields.Add((prefix + "targetLevel", Text(building.TargetLevel)));
            fields.Add((prefix + "cycleLabor", Text(building.CycleLabor)));
            fields.Add((prefix + "laborRemaining", Text(building.LaborRemaining)));
            fields.Add((prefix + "segmentStartedAt", Text(building.SegmentStartedAt)));
        }

        fields.Add(("completedResearch", string.Join(",", city.CompletedResearch)));
        ActiveResearch? active = city.ActiveResearch;
        fields.Add(("activeResearch", active is null ? "none" : $"{active.Id} {Text(active.StartedAt)}..{Text(active.EndsAt)}"));
        return fields;
    }

    /// <summary>A number as invariant text.</summary>
    public static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The first field in which <paramref name="actual"/> differs from <paramref name="expected"/>, or null: the expected fields in order, then
    /// the fields only the actual side has, so the first difference is the earliest one in that order.
    /// </summary>
    public static string? FirstDifference(IReadOnlyList<(string Path, string Value)> expected, IReadOnlyList<(string Path, string Value)> actual)
    {
        Dictionary<string, string> actualByPath = actual.ToDictionary(field => field.Path, field => field.Value, StringComparer.Ordinal);
        foreach ((string path, string value) in expected)
        {
            string actualValue = actualByPath.Remove(path, out string? found) ? found : _absent;
            if (!string.Equals(value, actualValue, StringComparison.Ordinal))
            {
                return $"{path}: expected {value}, actual {actualValue}";
            }
        }

        foreach ((string path, string value) in actual)
        {
            if (actualByPath.ContainsKey(path))
            {
                return $"{path}: expected {_absent}, actual {value}";
            }
        }

        return null;
    }
}
