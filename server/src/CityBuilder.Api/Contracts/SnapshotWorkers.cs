namespace CityBuilder.Api.Contracts;

/// <summary>The <c>workers</c> object of a <see cref="SnapshotCity"/>: the city's workforce.</summary>
/// <param name="Provided">The workers the completed houses provide, JSON <c>provided</c>.</param>
/// <param name="Assigned">The workers assigned to producers and courier stations, JSON <c>assigned</c>.</param>
/// <param name="OnTrips">The workers carrying goods on manual trips, JSON <c>onTrips</c>: one per trip under way (CR-0008).</param>
/// <param name="Free">The workers neither assigned nor on a trip, JSON <c>free</c>: provided minus assigned minus on trips.</param>
public sealed record SnapshotWorkers(long Provided, long Assigned, long OnTrips, long Free);
