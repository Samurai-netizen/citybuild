namespace CityBuilder.Api.Contracts;

/// <summary>The <c>research</c> object of a <see cref="SnapshotCity"/>: the research slot and what is completed (<c>core-instructions/25-eras-and-research.md</c>).</summary>
/// <param name="ActiveId">The research node running, JSON <c>activeId</c>; <see langword="null"/> when the slot is free.</param>
/// <param name="StartedAt">The city-clock second the running research's timer started, JSON <c>startedAt</c>; <see langword="null"/> when none runs
/// or it waits for goods.</param>
/// <param name="EndsAt">The city-clock second the running research completes, JSON <c>endsAt</c>; <see langword="null"/> when none runs or it waits
/// for goods.</param>
/// <param name="Needs">What the running research still needs at the science building, JSON <c>needs</c>: item id to quantity, by item id;
/// empty when its timer runs or none runs (CR-0008).</param>
/// <param name="Completed">The ids of the completed research, JSON <c>completed</c>, in ordinal id order.</param>
public sealed record SnapshotResearch(string? ActiveId, long? StartedAt, long? EndsAt, IReadOnlyDictionary<string, long> Needs, IReadOnlyList<string> Completed);
