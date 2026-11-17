namespace CityBuilder.Domain.Metrics;

/// <summary>
/// One thing a player did or reached, kept for the player metrics (CR-0009, <c>core-instructions/06-data-model.md</c>,
/// <c>player_events</c>): a command applied or refused by a game rule, a completed research or building, a session start, the player's
/// creation. Written in the city's transaction and never read by game code, so it can't change the game (<c>02</c> rule 23).
/// It holds the player's id and content ids only, never a name, token, address or request text (<c>07</c> → Logging policy). Immutable.
/// </summary>
public sealed class PlayerEvent
{
    /// <summary>The longest event type, detail or outcome, in characters (the columns' limit).</summary>
    public const int MaxLength = 64;

    /// <summary>Creates the event.</summary>
    /// <param name="playerId">The player it belongs to; not <see cref="Guid.Empty"/>.</param>
    /// <param name="eventType">What happened (<see cref="PlayerEventTypes"/>): 1 to <see cref="MaxLength"/> lower-case letters, digits,
    /// <c>.</c>, <c>-</c> and <c>_</c>, so a name from code, never text a client typed.</param>
    /// <param name="detail">The content id it is about (a building type, a research), or <see langword="null"/>; never blank, at most
    /// <see cref="MaxLength"/> characters.</param>
    /// <param name="outcome">How a command ended (<see cref="PlayerEventTypes.OutcomeAccepted"/> or an error code), or
    /// <see langword="null"/>; never blank, at most <see cref="MaxLength"/> characters.</param>
    /// <param name="value">A number the event carries (the level of a completed building), or <see langword="null"/>.</param>
    /// <param name="cityClock">The city clock when it was recorded, in seconds; 0 or more.</param>
    /// <param name="occurredAt">The server time when it was recorded.</param>
    /// <exception cref="ArgumentException">A value breaks one of the rules above.</exception>
    public PlayerEvent(Guid playerId, string eventType, string? detail, string? outcome, long? value, long cityClock, DateTimeOffset occurredAt)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("A player event needs a player.", nameof(playerId));
        }

        if (!IsEventTypeName(eventType))
        {
            throw new ArgumentException(
                $"An event type is 1 to {MaxLength} lower-case letters, digits, '.', '-' or '_'.", nameof(eventType));
        }

        CheckOptionalText(detail, nameof(detail));
        CheckOptionalText(outcome, nameof(outcome));
        if (cityClock < 0)
        {
            throw new ArgumentException("The city clock is 0 or more.", nameof(cityClock));
        }

        PlayerId = playerId;
        EventType = eventType;
        Detail = detail;
        Outcome = outcome;
        Value = value;
        CityClock = cityClock;
        OccurredAt = occurredAt;
    }

    /// <summary>The player it belongs to.</summary>
    public Guid PlayerId { get; }

    /// <summary>What happened (<see cref="PlayerEventTypes"/>).</summary>
    public string EventType { get; }

    /// <summary>The content id it is about, or <see langword="null"/>.</summary>
    public string? Detail { get; }

    /// <summary>How a command ended (<see cref="PlayerEventTypes.OutcomeAccepted"/> or an error code), or <see langword="null"/>.</summary>
    public string? Outcome { get; }

    /// <summary>A number the event carries (the level of a completed building), or <see langword="null"/>.</summary>
    public long? Value { get; }

    /// <summary>The city clock when it was recorded, in seconds, 0 or more.</summary>
    public long CityClock { get; }

    /// <summary>The server time when it was recorded.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Whether <paramref name="text"/> is a valid event type: 1 to <see cref="MaxLength"/> characters out of
    /// <c>a-z 0-9 . - _</c>.</summary>
    /// <param name="text">The candidate, possibly <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is valid.</returns>
    public static bool IsEventTypeName(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > MaxLength)
        {
            return false;
        }

        foreach (char c in text)
        {
            bool allowed = c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Refuses an optional text that is set but blank or longer than <see cref="MaxLength"/>: a missing value is
    /// <see langword="null"/>, never an empty string, so the report never sees two spellings of "none".</summary>
    /// <param name="text">The detail or the outcome.</param>
    /// <param name="name">The parameter's name, for the exception.</param>
    /// <exception cref="ArgumentException">The text is blank or too long.</exception>
    private static void CheckOptionalText(string? text, string name)
    {
        if (text is not null && (string.IsNullOrWhiteSpace(text) || text.Length > MaxLength))
        {
            throw new ArgumentException($"The value is null, or 1 to {MaxLength} characters and not blank.", name);
        }
    }
}
