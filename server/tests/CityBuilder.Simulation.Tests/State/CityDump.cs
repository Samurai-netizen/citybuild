using System.Globalization;
using System.Text;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// Writes every stored value of a city as text, one line per part, so a test can check that a city did not change at all, or show exactly
/// what did. It reads only public members, the way the server will.
/// </summary>
internal static class CityDump
{
    /// <summary>
    /// The city as text: a line with clock, era, next id and the purse, one per building (every stored field, its store and its needs), the
    /// completed research, the active research (with its needs) and one per road. Two cities with the same text have the same stored values.
    /// </summary>
    public static string Describe(CityState city)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"clock={city.Clock} era={city.Era} next={city.NextBuildingId} purse={city.Purse}\n");
        foreach (Building building in city.Buildings)
        {
            text.Append(CultureInfo.InvariantCulture, $"building {building.Id} {building.TypeId} ({building.X},{building.Y}) L{building.Level} ");
            text.Append(CultureInfo.InvariantCulture, $"w={building.AssignedWorkers} {building.Phase} {building.PhaseStartedAt}..{Time(building.PhaseEndsAt)} ");
            text.Append(CultureInfo.InvariantCulture, $"target={building.TargetLevel} labor={building.CycleLabor}/{building.LaborRemaining}@{building.SegmentStartedAt}");
            text.Append(" store:").Append(Amounts(building.Store)).Append(" needs:").Append(Amounts(building.Needs)).Append('\n');
        }

        text.Append("completed:").Append(string.Concat(city.CompletedResearch.Select(id => " " + id))).Append('\n');
        ActiveResearch? active = city.ActiveResearch;
        text.Append(active is null
            ? "active: none"
            : string.Create(CultureInfo.InvariantCulture, $"active: {active.Id} {Time(active.StartedAt)}..{Time(active.EndsAt)} needs:{Amounts(active.Needs)}"));
        foreach (RoadTile road in city.Roads)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nroad ({road.X},{road.Y}) L{road.Level}");
        }

        return text.ToString();
    }

    /// <summary>Quantities as <c> logs=3 planks=1</c> in their (ordinal item) order; empty for none.</summary>
    private static string Amounts(IReadOnlyDictionary<ItemId, long> amounts) =>
        string.Concat(amounts.Select(amount => string.Create(CultureInfo.InvariantCulture, $" {amount.Key}={amount.Value}")));

    /// <summary>The line of <see cref="Describe"/> for one building.</summary>
    public static string BuildingLine(CityState city, int buildingId) =>
        Describe(city).Split('\n').Single(line => line.StartsWith($"building {buildingId} ", StringComparison.Ordinal));

    /// <summary>A nullable time as text, <c>null</c> for none.</summary>
    private static string Time(long? time) => time.HasValue ? time.Value.ToString(CultureInfo.InvariantCulture) : "null";
}
