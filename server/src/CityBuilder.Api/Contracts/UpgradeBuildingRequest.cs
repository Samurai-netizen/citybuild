using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/upgrade-building</c> (core-instructions/05-api-contract.md): which building, nothing else. It has no
/// cost, level, time, worker or player field, and a body that sends one is refused (400) instead of having it ignored, so a client can't
/// believe it set one. Every field is required: a missing <c>buildingId</c> would otherwise read as building 0. Numbers must be JSON numbers:
/// the web defaults would also read <c>"3"</c>.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="BuildingId">The building to upgrade, JSON <c>buildingId</c>: its <c>id</c> in the snapshot.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record UpgradeBuildingRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] int BuildingId);
