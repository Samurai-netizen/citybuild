namespace CityBuilder.Api.Contracts;

/// <summary>The <c>research</c> object of a <see cref="SnapshotCity"/>: the research slot and what is completed (<c>core-instructions/25-eras-and-research.md</c>).</summary>
/// <param name="ActiveId">The research node running, JSON <c>activeId</c>; <see langword="null"/> when the slot is free.</param>
/// <param name="StartedAt">The city-clock second the running research started, JSON <c>startedAt</c>; <see langword="null"/> when none runs.</param>
/// <param name="EndsAt">The city-clock second the running research completes, JSON <c>endsAt</c>; <see langword="null"/> when none runs.</param>
/// <param name="Completed">The ids of the completed research, JSON <c>completed</c>, in ordinal id order.</param>
public sealed record SnapshotResearch(string? ActiveId, long? StartedAt, long? EndsAt, IReadOnlyList<string> Completed);
