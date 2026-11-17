namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>player_events</c> (core-instructions/06-data-model.md, CR-0009): one thing a player did or reached, kept for the player
/// metrics. It holds the player's id and content ids only (<c>07</c> → Logging policy); the columns are pinned by a test.
/// </summary>
public sealed class PlayerEventRow
{
    /// <summary><c>id</c>, the primary key, an identity column the database numbers; it orders the events of one request.</summary>
    public long Id { get; set; }

    /// <summary><c>player_id</c>: the player (<see cref="PlayerRow"/>).</summary>
    public Guid PlayerId { get; set; }

    /// <summary><c>event_type</c>: what happened, for example <c>command.place-building</c> or <c>research.completed</c>.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary><c>detail</c>: the content id it is about (a building type, a research), or <see langword="null"/>.</summary>
    public string? Detail { get; set; }

    /// <summary><c>outcome</c>: how a command ended (<c>accepted</c> or an error code), or <see langword="null"/>.</summary>
    public string? Outcome { get; set; }

    /// <summary><c>value</c>: a number the event carries (the level of a completed building), or <see langword="null"/>.</summary>
    public long? Value { get; set; }

    /// <summary><c>city_clock</c>: the city clock when it was recorded, in seconds, 0 or more.</summary>
    public long CityClock { get; set; }

    /// <summary><c>created_at</c>: the server time when it was recorded (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
