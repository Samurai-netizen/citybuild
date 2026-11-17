namespace CityBuilder.MetricsReport;

/// <summary>
/// The funnels section (docs/plan/CR-0009-T2-PLAN.md §3.3, §3.4): per funnel of the checkpoint file, its steps in order. A player counts
/// at step k when they reached steps 1 to k, each at or after the step before.
/// </summary>
public static class Funnels
{
    /// <summary>Builds one table per funnel, in the file's order.</summary>
    /// <param name="context">The population and the least sample.</param>
    /// <param name="file">The checkpoint file; its funnels name its checkpoints (the file checks that).</param>
    /// <returns>The tables.</returns>
    public static IReadOnlyList<FunnelTable> Build(ReportContext context, CheckpointFile file)
    {
        Dictionary<string, ReportCheckpoint> byId = file.Checkpoints.ToDictionary(c => c.Id, ReportCheckpoint.Named, StringComparer.Ordinal);
        return [.. file.Funnels.Select(funnel => Table(context, funnel, [.. funnel.Steps.Select(id => byId[id])]))];
    }

    /// <summary>Computes one funnel's table.</summary>
    /// <param name="context">The report's context.</param>
    /// <param name="funnel">The funnel.</param>
    /// <param name="steps">Its checkpoints in step order.</param>
    /// <returns>The table.</returns>
    private static FunnelTable Table(ReportContext context, Funnel funnel, IReadOnlyList<ReportCheckpoint> steps)
    {
        IReadOnlyList<PlayerTimeline> timelines = context.Timelines;
        // Per player, the event index of the previous step's reach, or -1 before step 1; null once the player dropped out.
        int?[] previous = [.. timelines.Select(_ => (int?)-1)];
        int before = timelines.Count;
        var rows = new List<FunnelStep>(steps.Count);
        foreach (ReportCheckpoint step in steps)
        {
            var seconds = new List<long>();
            for (int player = 0; player < timelines.Count; player++)
            {
                if (previous[player] is not int last)
                {
                    continue;
                }

                int reach = timelines[player].ReachIndex(step);
                if (reach < 0 || reach < last)
                {
                    previous[player] = null;
                    continue;
                }

                DateTimeOffset from = last < 0 ? timelines[player].Player.CreatedAt : timelines[player].Events[last].CreatedAt;
                seconds.Add((long)(timelines[player].Events[reach].CreatedAt - from).TotalSeconds);
                previous[player] = reach;
            }

            int reached = seconds.Count;
            rows.Add(new FunnelStep(
                step.Key, step.Title, reached, context.Percent(reached, timelines.Count), context.Percent(reached, before), before - reached,
                Stats.Median(seconds)));
            before = reached;
        }

        return new FunnelTable(funnel.Name, rows);
    }
}

/// <summary>One funnel as the page shows it.</summary>
/// <param name="Name">The funnel's name.</param>
/// <param name="Steps">Its steps in order.</param>
public sealed record FunnelTable(string Name, IReadOnlyList<FunnelStep> Steps);

/// <summary>One step of a funnel.</summary>
/// <param name="CheckpointId">The step's checkpoint id.</param>
/// <param name="Title">The checkpoint's title.</param>
/// <param name="Reached">The players who reached this step and every one before it, in order.</param>
/// <param name="OfPopulation">Their share of the population.</param>
/// <param name="OfPrevious">Their share of the step before (of the population for step 1).</param>
/// <param name="Drop">The players of the step before (the population for step 1) not counted here.</param>
/// <param name="MedianSecondsFromPrevious">The median real time from the step before (from the creation for step 1), or none.</param>
public sealed record FunnelStep(
    string CheckpointId, string Title, int Reached, Rate OfPopulation, Rate OfPrevious, int Drop, long? MedianSecondsFromPrevious);
