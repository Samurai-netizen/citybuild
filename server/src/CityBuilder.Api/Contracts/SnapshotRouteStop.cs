namespace CityBuilder.Api.Contracts;

/// <summary>One stop of a route in <see cref="SnapshotRoute.Stops"/> (CR-0008): a building and what the courier does there, in order.</summary>
/// <param name="BuildingId">The building, JSON <c>buildingId</c>.</param>
/// <param name="Actions">The actions in order, JSON <c>actions</c>: 1 or more.</param>
public sealed record SnapshotRouteStop(int BuildingId, IReadOnlyList<SnapshotRouteAction> Actions);
