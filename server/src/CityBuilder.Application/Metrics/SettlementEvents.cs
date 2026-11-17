using CityBuilder.Application.Cities;
using CityBuilder.Domain.Metrics;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Metrics;

/// <summary>
/// What a settlement completed, as player events (CR-0009): one <c>research.completed</c> per research node (detail the research id) and
/// one <c>building.completed</c> per construction or upgrade (detail the type id, value the level the building now has). Every event
/// carries the settled city clock, not the second the completion happened inside the window (<c>06</c>, <c>player_events</c>). The engine
/// is not asked for anything new: the settlement already lists its completions, each once.
/// </summary>
public static class SettlementEvents
{
    /// <summary>The settlement's completions as events: the research first, then the buildings, each in the settlement's order.</summary>
    /// <param name="city">The acting player's city, settled and closed.</param>
    /// <param name="settlement">The closed settlement of that city.</param>
    /// <param name="now">The server time of the request.</param>
    /// <returns>The events, in order; empty when nothing completed.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public static IReadOnlyList<PlayerEvent> Of(CityAggregate city, Settlement settlement, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(settlement);

        long clock = city.State.Clock;
        var events = new List<PlayerEvent>(settlement.CompletedResearch.Count + settlement.CompletedBuildings.Count);
        foreach (ResearchId research in settlement.CompletedResearch)
        {
            events.Add(new PlayerEvent(city.PlayerId, PlayerEventTypes.ResearchCompleted, research.Value, null, null, clock, now));
        }

        foreach (int buildingId in settlement.CompletedBuildings)
        {
            // A completed building is still in the city: nothing in a settlement removes one.
            Building building = city.State.Buildings.Single(candidate => candidate.Id == buildingId);
            events.Add(new PlayerEvent(
                city.PlayerId, PlayerEventTypes.BuildingCompleted, building.TypeId.Value, null, building.Level, clock, now));
        }

        return events;
    }
}
