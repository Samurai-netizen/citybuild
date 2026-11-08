using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/place-roads</c>, <c>upgrade-roads</c> and <c>remove-roads</c> (core-instructions/05-api-contract.md,
/// CR-0008): which tiles, nothing else. It has no level, cost or player field, and a body that sends one is refused (400) instead of having it
/// ignored. Both fields are required; an empty, too long or repeating list is <c>INVALID_ARGUMENT</c> from the command's shape check, before
/// the city is locked.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="Tiles">The tiles, JSON <c>tiles</c>: 1 to 64 objects <c>{ x, y }</c>, each tile once.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record RoadTilesRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] IReadOnlyList<TileRequest?>? Tiles);
