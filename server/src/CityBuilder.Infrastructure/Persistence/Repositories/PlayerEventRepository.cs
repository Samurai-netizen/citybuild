using CityBuilder.Application.Metrics;
using CityBuilder.Domain.Metrics;
using CityBuilder.Infrastructure.Persistence.Rows;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="IPlayerEventRepository"/> over <see cref="CityBuilderDbContext"/>: stages one <c>player_events</c> row per event; the
/// request's unit of work writes them with the city's save, in its transaction. Scoped, one per request, like the context.
/// </summary>
/// <param name="db">The request's context.</param>
/// <exception cref="ArgumentNullException"><paramref name="db"/> is null.</exception>
public sealed class PlayerEventRepository(CityBuilderDbContext db) : IPlayerEventRepository
{
    /// <summary>The request's context.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is null.</exception>
    public void Add(IReadOnlyList<PlayerEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        // Staged in the given order: the identity numbers them in it, and it orders one request's events (they share created_at).
        _db.PlayerEvents.AddRange(events.Select(ToRow));
    }

    /// <summary>The row of an event; <c>created_at</c> is the time the event was recorded, the database numbers <c>id</c>.</summary>
    /// <param name="playerEvent">The event.</param>
    /// <returns>A new row to insert.</returns>
    private static PlayerEventRow ToRow(PlayerEvent playerEvent) => new()
    {
        PlayerId = playerEvent.PlayerId,
        EventType = playerEvent.EventType,
        Detail = playerEvent.Detail,
        Outcome = playerEvent.Outcome,
        Value = playerEvent.Value,
        CityClock = playerEvent.CityClock,
        CreatedAt = playerEvent.OccurredAt,
    };
}
