namespace CityBuilder.MetricsReport;

/// <summary>The actions section (docs/plan/CR-0009-T2-PLAN.md §3.4): what the players did, per event type.</summary>
public static class Actions
{
    /// <summary>Builds one row per event type of the data, the most players first, a tie by type (ordinal).</summary>
    /// <param name="context">The population and the least sample.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<ActionRow> Build(ReportContext context) =>
        [.. context.Timelines
            .SelectMany(t => t.Events.GroupBy(e => e.EventType, StringComparer.Ordinal).Select(g => (Type: g.Key, Times: (long)g.Count())))
            .GroupBy(p => p.Type, StringComparer.Ordinal)
            .Select(g => new ActionRow(
                g.Key, g.Count(), context.Percent(g.Count(), context.Population), g.Sum(p => p.Times), Stats.Median(g.Select(p => p.Times))))
            .OrderByDescending(r => r.Players)
            .ThenBy(r => r.EventType, StringComparer.Ordinal)];
}

/// <summary>One event type of the actions section.</summary>
/// <param name="EventType">The event type.</param>
/// <param name="Players">The players with at least one such event.</param>
/// <param name="Share">Their share of the population.</param>
/// <param name="Times">The events of this type in total.</param>
/// <param name="MedianPerPlayer">The median events per player who has any.</param>
public sealed record ActionRow(string EventType, int Players, Rate Share, long Times, long? MedianPerPlayer);
