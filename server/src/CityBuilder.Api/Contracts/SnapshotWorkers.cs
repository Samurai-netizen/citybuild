namespace CityBuilder.Api.Contracts;

/// <summary>The <c>workers</c> object of a <see cref="SnapshotCity"/>: the city's workforce.</summary>
/// <param name="Provided">The workers the completed houses provide, JSON <c>provided</c>.</param>
/// <param name="Assigned">The workers assigned to producers, JSON <c>assigned</c>.</param>
/// <param name="Free">The workers not assigned, JSON <c>free</c>: provided minus assigned.</param>
public sealed record SnapshotWorkers(long Provided, long Assigned, long Free);
