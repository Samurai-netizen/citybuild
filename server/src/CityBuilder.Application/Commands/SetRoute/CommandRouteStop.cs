namespace CityBuilder.Application.Commands.SetRoute;

/// <summary>
/// A stop of the route a <see cref="SetRouteCommand"/> sets (<c>core-instructions/05-api-contract.md</c>, <c>stops: [{ buildingId, actions }]</c>):
/// the building the player chose and what the courier does there, in order. A record with properties, so the command's canonical payload
/// (<see cref="CommandPayload"/>) holds every value.
/// </summary>
/// <param name="BuildingId">The building.</param>
/// <param name="Actions">The actions in the player's order; 1 to <see cref="SetRouteCommand.MaxStopActions"/>.</param>
public sealed record CommandRouteStop(int BuildingId, IReadOnlyList<CommandRouteAction?>? Actions);
