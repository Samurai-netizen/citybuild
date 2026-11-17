using CityBuilder.Domain.Metrics;

namespace CityBuilder.Application.Metrics;

/// <summary>
/// The port to the stored player events (CR-0009, <c>core-instructions/06-data-model.md</c>, <c>player_events</c>). It only stages new
/// rows; the unit of work of the open transaction writes them, so an event exists exactly when the city's transaction commits. It has no
/// read member on purpose: game code never reads events (<c>02</c> rule 23); only the report tool reads the table (CR-0009-T2).
/// </summary>
public interface IPlayerEventRepository
{
    /// <summary>Stages the events for insertion; they are written with the next save of the unit of work.</summary>
    /// <param name="events">The events, in the order they happened; may be empty.</param>
    void Add(IReadOnlyList<PlayerEvent> events);
}
