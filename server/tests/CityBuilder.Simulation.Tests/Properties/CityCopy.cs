using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// An independent copy of a city, made through the public restore constructors of <see cref="CityState"/> and <see cref="Building"/> (as
/// the server will restore a city from the database): every stored value, new building objects, nothing shared that the engine changes.
/// </summary>
internal static class CityCopy
{
    /// <summary>A copy of the city: every stored value, with new building and research objects.</summary>
    public static CityState Of(CityState city) => new(
        city.Clock,
        city.Era,
        city.NextBuildingId,
        city.Inventory,
        city.Buildings.Select(Of),
        city.CompletedResearch,
        city.ActiveResearch is null ? null : new ActiveResearch(city.ActiveResearch.Id, city.ActiveResearch.StartedAt, city.ActiveResearch.EndsAt));

    /// <summary>A copy of the building: every stored value.</summary>
    public static Building Of(Building building) => new(
        building.Id,
        building.TypeId,
        building.X,
        building.Y,
        building.Level,
        building.AssignedWorkers,
        building.Phase,
        building.PhaseStartedAt,
        building.PhaseEndsAt,
        building.TargetLevel,
        building.CycleLabor,
        building.LaborRemaining,
        building.SegmentStartedAt);
}
