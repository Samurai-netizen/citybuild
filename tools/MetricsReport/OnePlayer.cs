namespace CityBuilder.MetricsReport;

/// <summary>The one-player section (docs/plan/CR-0009-T2-PLAN.md §3.4, <c>--player</c>): that player's timeline, the only place a player id appears.</summary>
public static class OnePlayer
{
    /// <summary>The most rows the section shows.</summary>
    public const int MaxRows = 5000;

    /// <summary>The timeline of one player of the population.</summary>
    /// <param name="context">The population.</param>
    /// <param name="playerId">The player's id.</param>
    /// <returns>The first <see cref="MaxRows"/> events in timeline order, or <see langword="null"/> when the id is not in the population.</returns>
    public static OnePlayerTimeline? Build(ReportContext context, Guid playerId)
    {
        PlayerTimeline? timeline = context.Timelines.FirstOrDefault(t => t.Player.Id == playerId);
        return timeline is null ? null : new OnePlayerTimeline(playerId, [.. timeline.Events.Take(MaxRows)], timeline.Events.Count);
    }
}

/// <summary>One player's timeline as the page shows it.</summary>
/// <param name="PlayerId">The player's id.</param>
/// <param name="Rows">The events in <c>(created_at, id)</c> order, at most <see cref="OnePlayer.MaxRows"/>.</param>
/// <param name="TotalEvents">All the player's events, so the page can say when rows were left out.</param>
public sealed record OnePlayerTimeline(Guid PlayerId, IReadOnlyList<MetricsEvent> Rows, int TotalEvents);
