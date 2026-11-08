namespace CityBuilder.Api.Contracts;

/// <summary>
/// The route of one courier station slot in <see cref="SnapshotCity.Routes"/> (<c>core-instructions/04-simulation-model.md</c>, section 19;
/// CR-0008), in the form <c>set-route</c> sends it, so the client can show and edit it. A slot has a route even without a courier.
/// </summary>
/// <param name="StationId">The courier station's building id, JSON <c>stationId</c>.</param>
/// <param name="Slot">The slot, from 1, JSON <c>slot</c>.</param>
/// <param name="Stops">The stops in walking order, JSON <c>stops</c>: 2 or more.</param>
public sealed record SnapshotRoute(int StationId, int Slot, IReadOnlyList<SnapshotRouteStop> Stops);
