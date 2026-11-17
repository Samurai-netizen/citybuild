namespace CityBuilder.Application.Metrics;

/// <summary>
/// When a request starts a new play session (CR-0009, <c>session.start</c>): when more than the session gap has passed since the player's
/// last request that settled the city. The server makes the policy from <c>Metrics:SessionGapSeconds</c> times
/// <c>Simulation:DevTimeScale</c> (the gap is real time; a Development clock runs that many server seconds per real one). Only the metrics
/// read it: no game rule depends on a session (<c>02</c> rule 23). Immutable.
/// </summary>
public sealed class SessionPolicy
{
    /// <summary>Creates the policy.</summary>
    /// <param name="gapSeconds">The gap in server seconds, 0 or more.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="gapSeconds"/> is below 0.</exception>
    public SessionPolicy(long gapSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gapSeconds);
        GapSeconds = gapSeconds;
    }

    /// <summary>The gap in server seconds: a request more than this long after the last one starts a new session.</summary>
    public long GapSeconds { get; }

    /// <summary>Whether a request at <paramref name="nowUnixSec"/> starts a new session: more than the gap after the last-seen second.</summary>
    /// <param name="lastSeenUnixSec">The player's last-seen second, read before this request marks the city seen.</param>
    /// <param name="nowUnixSec">The request's server second.</param>
    /// <returns><see langword="true"/> when the request starts a session; exactly the gap does not.</returns>
    public bool StartsSession(long lastSeenUnixSec, long nowUnixSec) => nowUnixSec - lastSeenUnixSec > GapSeconds;
}
