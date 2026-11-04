using System.Globalization;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Compares two cities, or two report totals, field by field and names the first field that differs, so a failing property says what went
/// wrong instead of "not equal". A city's fields come in a fixed order: clock, era, next building id, the purse, every field of every
/// building by id (its store and needs included), the completed research, the active research with its needs, the roads, the next trip id and every trip. It reads only public members, the way the server will.
/// </summary>
internal static class StructuralEquality
{
    /// <summary>The value shown for a field only one side has: an item the other side's stock doesn't hold, or a building it doesn't have.</summary>
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
            ("purse", Text(city.Purse)),
        };
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
            fields.AddRange(building.Store.Select(stock => ($"{prefix}store[{stock.Key}]", Text(stock.Value))));
            fields.AddRange(building.Needs.Select(need => ($"{prefix}needs[{need.Key}]", Text(need.Value))));
        }

        fields.Add(("completedResearch", string.Join(",", city.CompletedResearch)));
        ActiveResearch? active = city.ActiveResearch;
        fields.Add(("activeResearch", active is null ? "none" : $"{active.Id} {Optional(active.StartedAt)}..{Optional(active.EndsAt)}"));
        if (active is not null)
        {
            fields.AddRange(active.Needs.Select(need => ($"activeResearch needs[{need.Key}]", Text(need.Value))));
        }
        fields.AddRange(city.Roads.Select(road => ($"road ({Text(road.X)},{Text(road.Y)})", Text(road.Level))));
        fields.Add(("nextTripId", Text(city.NextTripId)));
        fields.AddRange(city.Trips.Select(trip => (
            $"trip {Text(trip.Id)}",
            $"{Text(trip.SourceId)}->{Text(trip.TargetId)} {trip.Item}={Text(trip.Quantity)} {Text(trip.DepartedAt)}..{Text(trip.ArrivesAt)}")));
        return fields;
    }

    /// <summary>A number as invariant text.</summary>
    public static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A number that may be missing as invariant text, <c>null</c> when it is.</summary>
    public static string Optional(long? value) => value.HasValue ? Text(value.Value) : "null";

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
