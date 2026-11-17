namespace CityBuilder.Domain.Metrics;

/// <summary>
/// One thing a player did or reached, for the player metrics (CR-0009, <c>core-instructions/06-data-model.md</c>, <c>player_events</c>).
/// Immutable.
/// </summary>
public sealed class PlayerEvent
{
    /// <summary>The longest event type, detail or outcome, in characters (the column's limit).</summary>
    public const int MaxLength = 64;

    /// <summary>Creates the event.</summary>
    /// <param name="playerId">The player.</param>
    /// <param name="eventType">The event type.</param>
    /// <param name="detail">The content id it is about, or null.</param>
    /// <param name="outcome">The outcome, or null.</param>
    /// <param name="value">A number, or null.</param>
    /// <param name="cityClock">The city clock.</param>
    /// <param name="occurredAt">The server time.</param>
    public PlayerEvent(Guid playerId, string eventType, string? detail, string? outcome, long? value, long cityClock, DateTimeOffset occurredAt)
    {
        throw new NotImplementedException();
    }

    /// <summary>The player.</summary>
    public Guid PlayerId { get; }

    /// <summary>The event type.</summary>
    public string EventType { get; } = "";

    /// <summary>The detail.</summary>
    public string? Detail { get; }

    /// <summary>The outcome.</summary>
    public string? Outcome { get; }

    /// <summary>The value.</summary>
    public long? Value { get; }

    /// <summary>The city clock.</summary>
    public long CityClock { get; }

    /// <summary>The server time.</summary>
    public DateTimeOffset OccurredAt { get; }
}
