namespace CityBuilder.Application.Cities;

/// <summary>
/// When the player counts as away (<c>core-instructions/04-simulation-model.md</c>, section 2): from their last request that settled the city
/// plus the presence grace, in server seconds. The settle hands that second, on the city clock, to the engine as <c>absentFrom</c>, so
/// couriers spend stamina only after it. The server makes the policy from <c>Simulation:PresenceGraceSeconds</c> times
/// <c>Simulation:DevTimeScale</c> (the grace is real time; a Development clock runs that many server seconds per real one). Immutable.
/// </summary>
public sealed class PresencePolicy
{
    /// <summary>Creates the policy.</summary>
    /// <param name="graceSeconds">The grace in server seconds, 0 or more: how long after a request the player still counts as present.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="graceSeconds"/> is below 0.</exception>
    public PresencePolicy(long graceSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(graceSeconds);
        GraceSeconds = graceSeconds;
    }

    /// <summary>The grace in server seconds: the player counts as present for this long after each request.</summary>
    public long GraceSeconds { get; }

    /// <summary>
    /// The city-clock second from which the player counts as away in the settle that starts now: the city clock stands at the last settled
    /// server second, so the absence starts <c>lastSeen + grace − lastSettled</c> seconds after it (at the clock itself, or later).
    /// </summary>
    /// <param name="city">The city, not yet settled by this request.</param>
    /// <returns>The <c>absentFrom</c> for the engine's advance of the settle's window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
    public long AbsentFrom(CityAggregate city)
    {
        ArgumentNullException.ThrowIfNull(city);
        return checked(city.State.Clock + Math.Max(0, city.LastSeenUnixSec + GraceSeconds - city.LastSettledUnixSec));
    }
}
