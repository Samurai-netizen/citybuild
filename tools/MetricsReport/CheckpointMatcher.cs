namespace CityBuilder.MetricsReport;

/// <summary>
/// Which events match a checkpoint, and the full list of checkpoints the report counts (docs/plan/CR-0009-T2-PLAN.md §3.3): the
/// named ones of the file and one unnamed one per distinct <c>(event_type, detail, outcome)</c> of the data.
/// </summary>
public static class CheckpointMatcher
{
    /// <summary>
    /// Whether an event matches a checkpoint: the same event type; for a named checkpoint the detail and outcome equal when it names
    /// them and the value at least its least value when it names one; for an unnamed one the exact detail and outcome.
    /// </summary>
    /// <param name="checkpoint">The checkpoint.</param>
    /// <param name="playerEvent">The event.</param>
    /// <returns><see langword="true"/> when the event counts toward the checkpoint.</returns>
    public static bool Matches(ReportCheckpoint checkpoint, MetricsEvent playerEvent)
    {
        if (!string.Equals(checkpoint.Event, playerEvent.EventType, StringComparison.Ordinal))
        {
            return false;
        }

        if (!checkpoint.IsNamed)
        {
            return string.Equals(checkpoint.Detail, playerEvent.Detail, StringComparison.Ordinal)
                && string.Equals(checkpoint.Outcome, playerEvent.Outcome, StringComparison.Ordinal);
        }

        return (checkpoint.Detail is null || string.Equals(checkpoint.Detail, playerEvent.Detail, StringComparison.Ordinal))
            && (checkpoint.Outcome is null || string.Equals(checkpoint.Outcome, playerEvent.Outcome, StringComparison.Ordinal))
            && (checkpoint.MinValue is null || playerEvent.Value >= checkpoint.MinValue);
    }

    /// <summary>One unnamed checkpoint per distinct <c>(event_type, detail, outcome)</c>, ordered by those parts (ordinal, none first).</summary>
    /// <param name="events">The events of the data.</param>
    /// <returns>The unnamed checkpoints.</returns>
    public static IReadOnlyList<ReportCheckpoint> Unnamed(IEnumerable<MetricsEvent> events) =>
        [.. events
            .Select(e => (e.EventType, e.Detail, e.Outcome))
            .Distinct()
            .OrderBy(t => t.EventType, StringComparer.Ordinal)
            .ThenBy(t => t.Detail, StringComparer.Ordinal)
            .ThenBy(t => t.Outcome, StringComparer.Ordinal)
            .Select(t => ReportCheckpoint.Unnamed(t.EventType, t.Detail, t.Outcome))];

    /// <summary>Every checkpoint the report counts: the named ones in file order, then the unnamed ones.</summary>
    /// <param name="named">The checkpoints of the file.</param>
    /// <param name="events">The events of the data.</param>
    /// <returns>The checkpoints, each with a unique key (a file id can't contain the spaces and dots of an unnamed title).</returns>
    public static IReadOnlyList<ReportCheckpoint> All(IEnumerable<Checkpoint> named, IEnumerable<MetricsEvent> events) =>
        [.. named.Select(ReportCheckpoint.Named), .. Unnamed(events)];
}
