using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/set-route</c> (core-instructions/05-api-contract.md, CR-0008): which station slot walks which stops,
/// nothing else. It has no walk time, load, stamina or player field, and a body that sends one is refused (400) instead of having it ignored.
/// Every field is required; an empty list of stops clears the route. A too long list or a wrong action is <c>INVALID_ARGUMENT</c> from the
/// command's shape check, before the city is locked.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="StationId">The courier station, JSON <c>stationId</c>.</param>
/// <param name="Slot">The slot, from 1, JSON <c>slot</c>.</param>
/// <param name="Stops">The stops in walking order, JSON <c>stops</c>: 0 to 16 objects <c>{ buildingId, actions }</c>.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record SetRouteRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] int StationId,
    [property: JsonRequired] int Slot,
    [property: JsonRequired] IReadOnlyList<RouteStopRequest?>? Stops);
