using CityBuilder.Domain.Metrics;

namespace CityBuilder.MetricsReport;

/// <summary>
/// The return section (docs/plan/CR-0009-T2-PLAN.md §3.4): per creation day (UTC), the share of its players with a <c>session.start</c>
/// on day 1, 3 and 7 after it; none while that day has not ended at "now".
/// </summary>
public static class Returns
{
    /// <summary>The days after the creation day the section looks at.</summary>
    public static IReadOnlyList<int> Days { get; } = [1, 3, 7];

    /// <summary>Builds one row per creation day, the oldest first.</summary>
    /// <param name="context">The population, "now", the least sample.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<ReturnRow> Build(ReportContext context) =>
        [.. context.Timelines
            .GroupBy(t => DateOnly.FromDateTime(t.Player.CreatedAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => Row(context, g.Key, [.. g]))];

    /// <summary>Computes one creation day's row.</summary>
    /// <param name="context">The report's context.</param>
    /// <param name="day">The creation day.</param>
    /// <param name="cohort">The players created that day.</param>
    /// <returns>The row.</returns>
    private static ReturnRow Row(ReportContext context, DateOnly day, PlayerTimeline[] cohort)
    {
        DateTimeOffset start = new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        Rate[] returned = [.. Days.Select(after =>
        {
            DateTimeOffset from = start.AddDays(after);
            DateTimeOffset to = from.AddDays(1);
            int players = cohort.Count(t => t.Events.Any(e =>
                e.EventType == PlayerEventTypes.SessionStart && e.CreatedAt >= from && e.CreatedAt < to));
            return to > context.Now ? new Rate(null, cohort.Length < context.MinSample) : context.Percent(players, cohort.Length);
        })];

        return new ReturnRow(day, cohort.Length, returned);
    }
}

/// <summary>One creation day of the return section.</summary>
/// <param name="Day">The creation day (UTC).</param>
/// <param name="Players">The players created that day.</param>
/// <param name="Returned">Per day of <see cref="Returns.Days"/>, the share with a session start on it; none while the day has not ended.</param>
public sealed record ReturnRow(DateOnly Day, int Players, IReadOnlyList<Rate> Returned);
