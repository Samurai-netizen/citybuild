namespace CityBuilder.MetricsReport.Tests;

/// <summary>
/// Builds hand-made metrics data for the tests: players and events with readable times (seconds after a fixed start) and ids
/// given in the order the events are added.
/// </summary>
public sealed class TestData
{
    /// <summary>The time every test time counts from: 2026-11-01 00:00 UTC.</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The players added so far, in the order added.</summary>
    private readonly List<MetricsPlayer> _players = [];

    /// <summary>The events added so far, in the order added.</summary>
    private readonly List<MetricsEvent> _events = [];

    /// <summary>A player id from a small number, so a test can name its players 1, 2, 3.</summary>
    /// <param name="number">The player's number.</param>
    /// <returns>The id <c>00000000-0000-0000-0000-00000000000n</c>.</returns>
    public static Guid PlayerId(int number) => new($"00000000-0000-0000-0000-{number:D12}");

    /// <summary>Adds a player.</summary>
    /// <param name="number">The player's number (<see cref="PlayerId"/>).</param>
    /// <param name="createdAtSecond">When the player was created, in seconds after <see cref="Start"/>.</param>
    /// <param name="lastSeenSecond">When the city was last seen, in seconds after <see cref="Start"/>.</param>
    /// <returns>This builder.</returns>
    public TestData Player(int number, long createdAtSecond = 0, long lastSeenSecond = 0)
    {
        _players.Add(new MetricsPlayer(PlayerId(number), Start.AddSeconds(createdAtSecond), Start.ToUnixTimeSeconds() + lastSeenSecond));
        return this;
    }

    /// <summary>Adds an event; its id is one more than the last one's unless given.</summary>
    /// <param name="player">The player's number.</param>
    /// <param name="second">When it happened, in seconds after <see cref="Start"/>.</param>
    /// <param name="eventType">The event type.</param>
    /// <param name="detail">The detail, or <see langword="null"/>.</param>
    /// <param name="outcome">The outcome, or <see langword="null"/>.</param>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <param name="cityClock">The city clock at the event.</param>
    /// <param name="id">The event id, or <see langword="null"/> for the next one.</param>
    /// <returns>This builder.</returns>
    public TestData Event(
        int player, long second, string eventType, string? detail = null, string? outcome = null, long? value = null, long cityClock = 0,
        long? id = null)
    {
        long eventId = id ?? (_events.Count == 0 ? 1 : _events.Max(e => e.Id) + 1);
        _events.Add(new MetricsEvent(eventId, PlayerId(player), eventType, detail, outcome, value, cityClock, Start.AddSeconds(second)));
        return this;
    }

    /// <summary>The data set built so far.</summary>
    /// <returns>The players and events.</returns>
    public MetricsData Build() => new([.. _players], [.. _events]);
}
