namespace CityBuilder.MetricsReport;

/// <summary>
/// A checkpoint as the report counts it (docs/plan/CR-0009-T2-PLAN.md §3.3): a named one from the checkpoint file, or an unnamed one
/// made from a distinct <c>(event_type, detail, outcome)</c> of the data. <see cref="CheckpointMatcher"/> says which events match it.
/// </summary>
/// <param name="Key">The unique key: the file's id for a named checkpoint, the title for an unnamed one.</param>
/// <param name="Title">The text the page shows.</param>
/// <param name="IsNamed">Whether it comes from the checkpoint file.</param>
/// <param name="Event">The event type it matches.</param>
/// <param name="Detail">The detail: for a named checkpoint <see langword="null"/> means any, for an unnamed one none.</param>
/// <param name="Outcome">The outcome: for a named checkpoint <see langword="null"/> means any, for an unnamed one none.</param>
/// <param name="MinValue">The least value an event must carry, or <see langword="null"/> for any.</param>
/// <param name="AtLeast">How many matching events reach it.</param>
public sealed record ReportCheckpoint(
    string Key, string Title, bool IsNamed, string Event, string? Detail, string? Outcome, long? MinValue, int AtLeast)
{
    /// <summary>The mark of a missing detail in an unnamed checkpoint's title when an outcome follows it.</summary>
    public const string MissingDetail = "—";

    /// <summary>A named checkpoint of the checkpoint file.</summary>
    /// <param name="checkpoint">The checkpoint as the file gives it.</param>
    /// <returns>The checkpoint, keyed by its id.</returns>
    public static ReportCheckpoint Named(Checkpoint checkpoint) =>
        new(checkpoint.Id, checkpoint.Title, true, checkpoint.Event, checkpoint.Detail, checkpoint.Outcome, checkpoint.MinValue,
            checkpoint.AtLeast);

    /// <summary>
    /// An unnamed checkpoint for one triple of the data, titled <c>event_type / detail / outcome</c>: a missing outcome is left out,
    /// a missing detail before an outcome shows as <c>—</c>.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="detail">The detail, or none.</param>
    /// <param name="outcome">The outcome, or none.</param>
    /// <returns>The checkpoint, reached by one exact match.</returns>
    public static ReportCheckpoint Unnamed(string eventType, string? detail, string? outcome)
    {
        string title = (detail, outcome) switch
        {
            (null, null) => eventType,
            (_, null) => $"{eventType} / {detail}",
            _ => $"{eventType} / {detail ?? MissingDetail} / {outcome}",
        };
        return new ReportCheckpoint(title, title, false, eventType, detail, outcome, null, 1);
    }
}
