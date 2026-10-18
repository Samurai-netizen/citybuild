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
    public static string Describe(CityState city)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"clock={city.Clock} era={city.Era} next={city.NextBuildingId}\n");
        text.Append("inventory:");
        foreach (KeyValuePair<ItemId, long> stock in city.Inventory)
        {
            text.Append(CultureInfo.InvariantCulture, $" {stock.Key}={stock.Value}");
        }

        text.Append('\n');
        foreach (Building building in city.Buildings)
        {
            text.Append(CultureInfo.InvariantCulture, $"building {building.Id} {building.TypeId} ({building.X},{building.Y}) L{building.Level} ");
            text.Append(CultureInfo.InvariantCulture, $"w={building.AssignedWorkers} {building.Phase} {building.PhaseStartedAt}..{Time(building.PhaseEndsAt)} ");
            text.Append(CultureInfo.InvariantCulture, $"target={building.TargetLevel} labor={building.CycleLabor}/{building.LaborRemaining}@{building.SegmentStartedAt}\n");
        }

        text.Append("completed:").Append(string.Concat(city.CompletedResearch.Select(id => " " + id))).Append('\n');
        ActiveResearch? active = city.ActiveResearch;
        text.Append(active is null ? "active: none" : string.Create(CultureInfo.InvariantCulture, $"active: {active.Id} {active.StartedAt}..{active.EndsAt}"));
        return text.ToString();
    }

    /// <summary>The line of <see cref="Describe"/> for one building.</summary>
    public static string BuildingLine(CityState city, int buildingId) =>
        Describe(city).Split('\n').Single(line => line.StartsWith($"building {buildingId} ", StringComparison.Ordinal));

    private static string Time(long? time) => time.HasValue ? time.Value.ToString(CultureInfo.InvariantCulture) : "null";
}
