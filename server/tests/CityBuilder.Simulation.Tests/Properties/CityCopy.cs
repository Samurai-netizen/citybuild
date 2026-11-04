using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// An independent copy of a city, made through the public restore constructors of <see cref="CityState"/> and <see cref="Building"/> (as
/// the server will restore a city from the database): every stored value, new building objects, nothing shared that the engine changes.
/// </summary>
internal static class CityCopy
{
    /// <summary>
    /// A copy of the city: every stored value (the purse, each building's store and needs, the research's needs, the trips and the next trip
    /// id, the couriers and routes), with new building, research and courier objects. Trips, roads and routes never change, so the copy
    /// shares them.
    /// </summary>
    public static CityState Of(CityState city) => new(
        city.Clock,
        city.Era,
        city.NextBuildingId,
        city.Purse,
        city.Buildings.Select(Of),
        city.CompletedResearch,
        city.ActiveResearch is null
            ? null
            : new ActiveResearch(city.ActiveResearch.Id, city.ActiveResearch.StartedAt, city.ActiveResearch.EndsAt, city.ActiveResearch.Needs.ToArray()),
        city.Roads,
        city.Trips,
        city.NextTripId,
        city.Couriers.Select(Of),
        city.Routes);

    /// <summary>A copy of the courier: every stored value (its load parts are values and are shared).</summary>
    public static Courier Of(Courier courier) => new(
        courier.StationId,
        courier.Slot,
        courier.State,
        courier.ToStop,
        courier.DepartedAt,
        courier.ArrivesAt,
        courier.Stamina,
        courier.Loads);

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
        building.SegmentStartedAt,
        building.Store.ToArray(),
        building.Needs.ToArray());
}
