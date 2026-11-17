using CityBuilder.Domain.Metrics;

namespace CityBuilder.MetricsReport;

/// <summary>
/// The checkpoint explorer (docs/plan/CR-0009-T2-PLAN.md §3.4): one row per named and per unnamed checkpoint, each standing alone (no
/// funnel changes a number), in the default order: last stop, descending.
/// </summary>
public static class Explorer
{
    /// <summary>How many unnamed rows, the most reached, get a detail block.</summary>
    public const int UnnamedDetails = 50;

    /// <summary>How many lines a "most common" list of a detail block holds.</summary>
    public const int TopCount = 5;

    /// <summary>Builds the rows.</summary>
    /// <param name="context">The population, "now", who left, the least sample.</param>
    /// <param name="file">The checkpoint file; its funnels are not read.</param>
    /// <returns>The rows, last stop descending; a tie keeps the named ones first, each in its list order.</returns>
    public static IReadOnlyList<ExplorerRow> Build(ReportContext context, CheckpointFile file)
    {
        IReadOnlyList<PlayerTimeline> timelines = context.Timelines;
        IReadOnlyList<ReportCheckpoint> checkpoints = CheckpointMatcher.All(file.Checkpoints, timelines.SelectMany(t => t.Events));
        int[][] reaches = [.. checkpoints.Select(c => timelines.Select(t => t.ReachIndex(c)).ToArray())];
        bool[] left = [.. timelines.Select(t => context.HasLeft(t.Player))];
        int[] lastStops = LastStops(checkpoints, reaches, timelines, left);
        HashSet<int> unnamedWithDetail = [.. Enumerable.Range(0, checkpoints.Count)
            .Where(c => !checkpoints[c].IsNamed)
            .OrderByDescending(c => reaches[c].Count(r => r >= 0))
            .Take(UnnamedDetails)];

        return [.. Enumerable.Range(0, checkpoints.Count)
            .Select(c => Row(context, checkpoints, reaches, left, c, lastStops[c], checkpoints[c].IsNamed || unnamedWithDetail.Contains(c)))
            .OrderByDescending(r => r.LastStop)];
    }

    /// <summary>
    /// For each checkpoint, the players who left with it as their latest reach: once among the named checkpoints, once among the
    /// unnamed ones. The latest reach is the greatest <c>(created_at, id)</c>; one event reaching two checkpoints counts for the first.
    /// </summary>
    /// <param name="checkpoints">The checkpoints.</param>
    /// <param name="reaches">Per checkpoint, per player, the index of the reach or -1.</param>
    /// <param name="timelines">The timelines.</param>
    /// <param name="left">Per player, whether they left.</param>
    /// <returns>Per checkpoint, its last-stop players.</returns>
    private static int[] LastStops(IReadOnlyList<ReportCheckpoint> checkpoints, int[][] reaches, IReadOnlyList<PlayerTimeline> timelines, bool[] left)
    {
        int[] lastStops = new int[checkpoints.Count];
        for (int player = 0; player < timelines.Count; player++)
        {
            if (!left[player])
            {
                continue;
            }

            foreach (bool named in new[] { true, false })
            {
                int best = -1;
                for (int c = 0; c < checkpoints.Count; c++)
                {
                    if (checkpoints[c].IsNamed == named && reaches[c][player] >= 0
                        && (best < 0 || IsLater(timelines[player].Events[reaches[c][player]], timelines[player].Events[reaches[best][player]])))
                    {
                        best = c;
                    }
                }

                if (best >= 0)
                {
                    lastStops[best]++;
                }
            }
        }

        return lastStops;
    }

    /// <summary>Whether an event comes strictly after another in <c>(created_at, id)</c> order.</summary>
    /// <param name="candidate">The event to test.</param>
    /// <param name="current">The event to beat.</param>
    /// <returns><see langword="true"/> when <paramref name="candidate"/> is later.</returns>
    private static bool IsLater(MetricsEvent candidate, MetricsEvent current) =>
        candidate.CreatedAt > current.CreatedAt || (candidate.CreatedAt == current.CreatedAt && candidate.Id > current.Id);

    /// <summary>Computes one row.</summary>
    /// <param name="context">The report's context.</param>
    /// <param name="checkpoints">Every checkpoint.</param>
    /// <param name="reaches">Per checkpoint, per player, the index of the reach or -1.</param>
    /// <param name="left">Per player, whether they left.</param>
    /// <param name="index">The row's checkpoint.</param>
    /// <param name="lastStop">Its last-stop players.</param>
    /// <param name="withDetail">Whether the row gets a detail block.</param>
    /// <returns>The row.</returns>
    private static ExplorerRow Row(
        ReportContext context, IReadOnlyList<ReportCheckpoint> checkpoints, int[][] reaches, bool[] left, int index, int lastStop, bool withDetail)
    {
        ReportCheckpoint checkpoint = checkpoints[index];
        IReadOnlyList<PlayerTimeline> timelines = context.Timelines;
        int[] reachers = [.. Enumerable.Range(0, timelines.Count).Where(p => reaches[index][p] >= 0)];
        int[] leftReachers = [.. reachers.Where(p => left[p])];
        int reached = reachers.Length;
        int notReached = timelines.Count - reached;
        int leftWithout = context.LeftCount - leftReachers.Length;
        // The event with which a player reached the row's checkpoint.
        MetricsEvent ReachOf(int player) => timelines[player].Events[reaches[index][player]];

        return new ExplorerRow(
            checkpoint,
            reached,
            context.Percent(reached, timelines.Count),
            timelines.Sum(t => (long)t.Count(checkpoint)),
            Stats.Median(reachers.Select(p => (long)timelines[p].Count(checkpoint))),
            Stats.Median(reachers.Select(p => (long)(ReachOf(p).CreatedAt - timelines[p].Player.CreatedAt).TotalSeconds)),
            Stats.Median(reachers.Select(p => ReachOf(p).CityClock)),
            context.Percent(leftReachers.Length, reached),
            context.Percent(leftWithout, notReached),
            Rate.Difference(leftReachers.Length, reached, leftWithout, notReached, context.MinSample),
            lastStop,
            context.Percent(lastStop, context.LeftCount),
            Stats.Median(reachers.Select(p => Math.Max(0, timelines[p].Player.LastSeenUnix - ReachOf(p).CreatedAt.ToUnixTimeSeconds()))),
            Stats.Median(reachers.Select(p => (long)timelines[p].Events
                .Skip(reaches[index][p] + 1)
                .Count(e => e.EventType == PlayerEventTypes.SessionStart))),
            withDetail ? Detail(context, checkpoints, reaches, index, reachers, leftReachers) : null);
    }

    /// <summary>The detail block of a row.</summary>
    /// <param name="context">The report's context.</param>
    /// <param name="checkpoints">Every checkpoint.</param>
    /// <param name="reaches">Per checkpoint, per player, the index of the reach or -1.</param>
    /// <param name="index">The row's checkpoint.</param>
    /// <param name="reachers">The players who reached it.</param>
    /// <param name="leftReachers">Those of them who left.</param>
    /// <returns>The detail.</returns>
    private static ExplorerDetail Detail(
        ReportContext context, IReadOnlyList<ReportCheckpoint> checkpoints, int[][] reaches, int index, int[] reachers, int[] leftReachers)
    {
        IReadOnlyList<PlayerTimeline> timelines = context.Timelines;
        IEnumerable<string> next = reachers
            .Where(p => reaches[index][p] + 1 < timelines[p].Events.Count)
            .Select(p => TitleOf(timelines[p].Events[reaches[index][p] + 1]));
        IEnumerable<string> last = leftReachers.Select(p => TitleOf(timelines[p].Events[^1]));
        DetailCount[] others = [.. Enumerable.Range(0, checkpoints.Count)
            .Where(c => c != index && checkpoints[c].IsNamed)
            .Select(c => (Title: checkpoints[c].Title, Players: leftReachers.Count(p => reaches[c][p] >= 0)))
            .Where(o => o.Players > 0)
            .OrderByDescending(o => o.Players)
            .Select(o => new DetailCount(o.Title, o.Players, context.Percent(o.Players, leftReachers.Length)))];

        return new ExplorerDetail(Top(context, next, reachers.Length), Top(context, last, leftReachers.Length), others);
    }

    /// <summary>The most common titles: the most players first, a tie by title (ordinal), at most <see cref="TopCount"/>.</summary>
    /// <param name="context">The report's context.</param>
    /// <param name="titles">One title per player.</param>
    /// <param name="whole">The players the shares are of.</param>
    /// <returns>The lines.</returns>
    private static DetailCount[] Top(ReportContext context, IEnumerable<string> titles, int whole) =>
        [.. titles
            .GroupBy(t => t, StringComparer.Ordinal)
            .Select(g => (Title: g.Key, Players: g.Count()))
            .OrderByDescending(g => g.Players)
            .ThenBy(g => g.Title, StringComparer.Ordinal)
            .Take(TopCount)
            .Select(g => new DetailCount(g.Title, g.Players, context.Percent(g.Players, whole)))];

    /// <summary>An event's title, as its unnamed checkpoint shows it.</summary>
    /// <param name="playerEvent">The event.</param>
    /// <returns><c>event_type / detail / outcome</c>.</returns>
    private static string TitleOf(MetricsEvent playerEvent) =>
        ReportCheckpoint.Unnamed(playerEvent.EventType, playerEvent.Detail, playerEvent.Outcome).Title;
}
