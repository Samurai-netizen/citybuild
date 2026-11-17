namespace CityBuilder.MetricsReport;

/// <summary>
/// One checkpoint of the checkpoint file (docs/plan/CR-0009-T2-PLAN.md §3.2): a player reached it with at least
/// <see cref="AtLeast"/> events that match its event type and the parts it names.
/// </summary>
/// <param name="Id">The unique id, <c>[a-z0-9_]{1,48}</c>.</param>
/// <param name="Title">The text the page shows.</param>
/// <param name="Event">The event type it matches (<see cref="KnownEvents"/>).</param>
/// <param name="Detail">The content id the event must name, or <see langword="null"/> for any.</param>
/// <param name="Outcome">The outcome the event must have (<c>accepted</c> or an error code), or <see langword="null"/> for any.</param>
/// <param name="MinValue">The least value the event must have (a building level, for example), or <see langword="null"/> for any.</param>
/// <param name="AtLeast">How many matching events reach it; 1 or more.</param>
public sealed record Checkpoint(string Id, string Title, string Event, string? Detail, string? Outcome, long? MinValue, int AtLeast);
