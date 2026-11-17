namespace CityBuilder.MetricsReport;

/// <summary>
/// One player's events in <c>(created_at, id)</c> order, and when the player reached a checkpoint (docs/plan/CR-0009-T2-PLAN.md §3.3:
/// the reach is the <c>atLeast</c>-th matching event).
/// </summary>
public sealed class PlayerTimeline
{
    /// <summary>Makes the timeline of one player.</summary>
    /// <param name="player">The player.</param>
    /// <param name="events">The player's events, already in timeline order.</param>
    private PlayerTimeline(MetricsPlayer player, IReadOnlyList<MetricsEvent> events)
    {
        Player = player;
        Events = events;
    }

    /// <summary>The player.</summary>
    public MetricsPlayer Player { get; }

    /// <summary>The player's events in <c>(created_at, id)</c> order.</summary>
    public IReadOnlyList<MetricsEvent> Events { get; }

    /// <summary>The timeline of every player of the data, the players in <c>(created_at, id)</c> order.</summary>
    /// <param name="data">The players and events.</param>
    /// <returns>One timeline per player; events of a player not in the data are left out.</returns>
    public static IReadOnlyList<PlayerTimeline> Build(MetricsData data)
    {
        ILookup<Guid, MetricsEvent> byPlayer = data.Events.ToLookup(e => e.PlayerId);
        return [.. data.Players
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(p => new PlayerTimeline(p, [.. byPlayer[p.Id].OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)]))];
    }

    /// <summary>The event with which the player reached a checkpoint: its <c>atLeast</c>-th match.</summary>
    /// <param name="checkpoint">The checkpoint.</param>
    /// <returns>The reaching event, or <see langword="null"/> when the player has fewer matches.</returns>
    public MetricsEvent? Reach(ReportCheckpoint checkpoint)
    {
        int seen = 0;
        foreach (MetricsEvent playerEvent in Events)
        {
            if (CheckpointMatcher.Matches(checkpoint, playerEvent) && ++seen == checkpoint.AtLeast)
            {
                return playerEvent;
            }
        }

        return null;
    }

    /// <summary>How many of the player's events match a checkpoint.</summary>
    /// <param name="checkpoint">The checkpoint.</param>
    /// <returns>The number of matches, also past the reach.</returns>
    public int Count(ReportCheckpoint checkpoint) => Events.Count(e => CheckpointMatcher.Matches(checkpoint, e));
}
