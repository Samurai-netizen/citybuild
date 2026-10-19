using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Cities;

/// <summary>
/// A player's city as the server keeps it: the engine's <see cref="CityState"/> plus the server second it was last settled at
/// (<c>core-instructions/06-data-model.md</c>, <c>cities.last_settled_unix</c>). The city clock and the server clock are separate: the city
/// clock counts simulated seconds from the city's start, the last settled second is wall time. A skeleton until M0-20 adds what persistence
/// needs.
/// </summary>
public sealed class CityAggregate
{
    /// <summary>Creates the city, for example when a repository loads it.</summary>
    /// <param name="playerId">The player who owns the city.</param>
    /// <param name="state">The engine's state of the city.</param>
    /// <param name="lastSettledUnixSec">The server second, since the Unix epoch, the city was last settled at.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    public CityAggregate(Guid playerId, CityState state, long lastSettledUnixSec)
    {
        PlayerId = playerId;
        State = state ?? throw new ArgumentNullException(nameof(state));
        LastSettledUnixSec = lastSettledUnixSec;
    }

    /// <summary>The player who owns the city.</summary>
    public Guid PlayerId { get; }

    /// <summary>The engine's state of the city; settling and commands change it in place.</summary>
    public CityState State { get; }

    /// <summary>The server second, since the Unix epoch, the city was last settled at. It never decreases.</summary>
    public long LastSettledUnixSec { get; private set; }

    /// <summary>Records the settlement that ended at <paramref name="lastSettledUnixSec"/>.</summary>
    /// <param name="lastSettledUnixSec">The new last settled second, from the settlement window.</param>
    /// <exception cref="ArgumentOutOfRangeException">The second is before the current last settled second.</exception>
    public void MarkSettled(long lastSettledUnixSec)
    {
        if (lastSettledUnixSec < LastSettledUnixSec)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastSettledUnixSec), lastSettledUnixSec, $"The last settled second never decreases; it is {LastSettledUnixSec}.");
        }

        LastSettledUnixSec = lastSettledUnixSec;
    }
}
