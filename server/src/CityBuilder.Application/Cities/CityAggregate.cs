using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Cities;

/// <summary>
/// A player's city as the server keeps it (<c>core-instructions/06-data-model.md</c>, <c>cities</c> and its child tables): the engine's
/// <see cref="CityState"/> plus what only the server stores. The city clock and the server clock are separate: the city clock counts
/// simulated seconds from the city's start, the last settled second is wall time. The engine keeps only the ids of completed research, so
/// the aggregate keeps the city-clock second each one completed (<c>city_research.completed_at</c>); advance the city through
/// <see cref="Advance"/> so those seconds are recorded.
/// </summary>
public sealed class CityAggregate
{
    /// <summary>The completion clock of each completed research, by research id; the keys are exactly the state's completed research.</summary>
    private readonly SortedDictionary<ResearchId, long> _researchCompletedAt;

    /// <summary>Creates the city, for example when a repository loads it or the first login creates it.</summary>
    /// <param name="cityId">The city's id, set by the application; not <see cref="Guid.Empty"/>.</param>
    /// <param name="playerId">The player who owns the city; not <see cref="Guid.Empty"/>.</param>
    /// <param name="state">The engine's state of the city.</param>
    /// <param name="researchCompletedAt">The city-clock second each completed research of <paramref name="state"/> completed: one entry
    /// per completed research, none for others, each between 0 and the city clock.</param>
    /// <param name="lastSettledUnixSec">The server second, since the Unix epoch, the city was last settled at.</param>
    /// <param name="stateVersion">The stored version of the city, 0 or more; a save writes the next one (<c>06</c>, <c>state_version</c>).</param>
    /// <param name="contentVersion">The content version the city was last settled with; not blank.</param>
    /// <param name="lastSeenUnixSec">The server second, since the Unix epoch, of the player's last request that settled the city
    /// (<c>04</c> section 2); null means <paramref name="lastSettledUnixSec"/>, which every request so far also was.</param>
    /// <exception cref="ArgumentException">A value breaks one of the rules above.</exception>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    public CityAggregate(
        Guid cityId,
        Guid playerId,
        CityState state,
        IEnumerable<KeyValuePair<ResearchId, long>> researchCompletedAt,
        long lastSettledUnixSec,
        long stateVersion,
        string contentVersion,
        long? lastSeenUnixSec = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(researchCompletedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentVersion);
        if (cityId == Guid.Empty || playerId == Guid.Empty)
        {
            throw new ArgumentException("The city and player ids must not be empty.", nameof(cityId));
        }

        if (stateVersion < 0)
        {
            throw new ArgumentException("The state version must not be negative.", nameof(stateVersion));
        }

        _researchCompletedAt = new SortedDictionary<ResearchId, long>();
        foreach (KeyValuePair<ResearchId, long> completion in researchCompletedAt)
        {
            if (!state.HasCompletedResearch(completion.Key) || completion.Value < 0 || completion.Value > state.Clock
                || !_researchCompletedAt.TryAdd(completion.Key, completion.Value))
            {
                throw new ArgumentException(
                    $"Research {completion.Key}: not completed in the state, listed twice, or completed outside 0..{state.Clock}.",
                    nameof(researchCompletedAt));
            }
        }

        if (_researchCompletedAt.Count != state.CompletedResearch.Count)
        {
            throw new ArgumentException("Every completed research of the state needs its completion clock.", nameof(researchCompletedAt));
        }

        CityId = cityId;
        PlayerId = playerId;
        State = state;
        LastSettledUnixSec = lastSettledUnixSec;
        LastSeenUnixSec = lastSeenUnixSec ?? lastSettledUnixSec;
        StateVersion = stateVersion;
        ContentVersion = contentVersion;
        ResearchCompletedAt = _researchCompletedAt.AsReadOnly();
    }

    /// <summary>The city's id.</summary>
    public Guid CityId { get; }

    /// <summary>The player who owns the city.</summary>
    public Guid PlayerId { get; }

    /// <summary>
    /// The engine's state of the city; commands change it in place. Advance it only through <see cref="Advance"/>: a research completed
    /// around it has no completion clock, and saving the city then fails.
    /// </summary>
    public CityState State { get; }

    /// <summary>The city-clock second each completed research completed, in research id order.</summary>
    public IReadOnlyDictionary<ResearchId, long> ResearchCompletedAt { get; }

    /// <summary>The server second, since the Unix epoch, the city was last settled at. It never decreases.</summary>
    public long LastSettledUnixSec { get; private set; }

    /// <summary>
    /// The server second, since the Unix epoch, of the player's last request that settled the city: the start of their presence grace
    /// (<c>04</c> section 2). It never decreases; <see cref="MarkSeen"/> records the next one.
    /// </summary>
    public long LastSeenUnixSec { get; private set; }

    /// <summary>The stored version of the city, as loaded; a save writes this plus 1, and the stored row must still have this one.</summary>
    public long StateVersion { get; }

    /// <summary>The content version the city was last settled with; <see cref="MarkSettled"/> records the next one.</summary>
    public string ContentVersion { get; private set; }

    /// <summary>
    /// Advances the city by <paramref name="elapsedSeconds"/> with the engine (<see cref="Simulator.Advance"/>) and records the completion
    /// clock of the research that completed: the end of the research that was running, since the engine's research timer fires exactly then.
    /// </summary>
    /// <param name="content">The game content.</param>
    /// <param name="elapsedSeconds">The seconds to advance; 0 is the zero-length advance every command ends with.</param>
    /// <param name="absentFrom">The city-clock second from which the player counts as away (<see cref="PresencePolicy"/>); null: present the
    /// whole window, as in a zero-length advance.</param>
    /// <returns>The engine's report of the advance.</returns>
    /// <exception cref="InvalidOperationException">The engine completed a research that wasn't running (an engine bug).</exception>
    public SettlementReport Advance(GameContent content, long elapsedSeconds, long? absentFrom = null)
    {
        ActiveResearch? running = State.ActiveResearch;
        SettlementReport report = Simulator.Advance(State, content, elapsedSeconds, absentFrom);
        foreach (ResearchId completed in report.CompletedResearch)
        {
            // One research slot, and only commands start research, so at most the one running at the start completes; it completes at its
            // timer's end, which a delivery may have set during the advance (04 section 5).
            if (running is null || completed != running.Id || running.EndsAt is not long endsAt)
            {
                throw new InvalidOperationException($"Research {completed} completed, but it was not the one running.");
            }

            _researchCompletedAt.Add(completed, endsAt);
        }

        return report;
    }

    /// <summary>
    /// Records that the player made a request at <paramref name="unixSec"/> (<c>04</c> section 2): the last-seen second becomes it, unless
    /// it is earlier (a server clock set back), so it never decreases.
    /// </summary>
    /// <param name="unixSec">The server second of the request.</param>
    public void MarkSeen(long unixSec) => LastSeenUnixSec = Math.Max(LastSeenUnixSec, unixSec);

    /// <summary>Records the settlement that ended at <paramref name="lastSettledUnixSec"/>, run with <paramref name="contentVersion"/>.</summary>
    /// <param name="lastSettledUnixSec">The new last settled second, from the settlement window.</param>
    /// <param name="contentVersion">The version of the content the settlement ran with; not blank.</param>
    /// <exception cref="ArgumentOutOfRangeException">The second is before the current last settled second.</exception>
    /// <exception cref="ArgumentException">The content version is null or blank.</exception>
    public void MarkSettled(long lastSettledUnixSec, string contentVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentVersion);
        if (lastSettledUnixSec < LastSettledUnixSec)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastSettledUnixSec), lastSettledUnixSec, $"The last settled second never decreases; it is {LastSettledUnixSec}.");
        }

        LastSettledUnixSec = lastSettledUnixSec;
        ContentVersion = contentVersion;
    }
}
