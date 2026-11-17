using CityBuilder.Application.Cities;
using CityBuilder.Domain.Metrics;

namespace CityBuilder.Application.Metrics;

/// <summary>
/// The player's session events (CR-0009): <c>player.created</c> at the first login and <c>session.start</c> at the first login and at each
/// request that settles the city more than the session gap (<see cref="SessionPolicy"/>) after the one before. Neither has a detail,
/// an outcome or a value.
/// </summary>
public static class SessionEvents
{
    /// <summary>
    /// The request's <c>session.start</c> when it starts a session: the policy decides from the last-seen second the city had before this
    /// request settled it (the settle marks the city seen, so it must be read before <see cref="CitySettler.Begin"/>).
    /// </summary>
    /// <param name="city">The acting player's city, settled and closed.</param>
    /// <param name="lastSeenUnixSec">The city's last-seen second before this request.</param>
    /// <param name="sessions">The session policy.</param>
    /// <param name="now">The server time of the request.</param>
    /// <returns>The event at the settled clock, or <see langword="null"/> when the request is in the session before it.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public static PlayerEvent? StartOf(CityAggregate city, long lastSeenUnixSec, SessionPolicy sessions, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(sessions);
        return sessions.StartsSession(lastSeenUnixSec, now.ToUnixTimeSeconds()) ? Of(PlayerEventTypes.SessionStart, city.PlayerId, city.State.Clock, now) : null;
    }

    /// <summary>A new player's first events, in order: <c>player.created</c>, then <c>session.start</c>.</summary>
    /// <param name="city">The new player's new city.</param>
    /// <param name="now">The server time of the first login.</param>
    /// <returns>The two events, at the new city's clock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
    public static IReadOnlyList<PlayerEvent> FirstLogin(CityAggregate city, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(city);
        return
        [
            Of(PlayerEventTypes.PlayerCreated, city.PlayerId, city.State.Clock, now),
            Of(PlayerEventTypes.SessionStart, city.PlayerId, city.State.Clock, now),
        ];
    }

    /// <summary>An event of the type with no detail, outcome or value.</summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cityClock">The city clock.</param>
    /// <param name="now">The server time.</param>
    /// <returns>The event.</returns>
    private static PlayerEvent Of(string eventType, Guid playerId, long cityClock, DateTimeOffset now) =>
        new(playerId, eventType, detail: null, outcome: null, value: null, cityClock, now);
}
