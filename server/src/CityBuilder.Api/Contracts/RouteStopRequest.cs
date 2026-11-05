using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>One stop of a <see cref="SetRouteRequest"/> (CR-0008): a building and the courier's actions there. Both fields are required.</summary>
/// <param name="BuildingId">The building, JSON <c>buildingId</c>.</param>
/// <param name="Actions">The actions in order, JSON <c>actions</c>: 1 to 8 objects <c>{ kind, itemId, amount }</c>.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record RouteStopRequest(
    [property: JsonRequired] int BuildingId,
    [property: JsonRequired] IReadOnlyList<RouteActionRequest?>? Actions);
