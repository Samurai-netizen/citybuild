using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/assign-workers</c> (core-instructions/05-api-contract.md): which building and how many workers it
/// should have, nothing else. It has no free-worker, max-worker, time, cost or player field, and a body that sends one is refused (400)
/// instead of having it ignored, so a client can't believe it set one. Every field is required: a missing <c>workers</c> would otherwise read
/// as 0 and pause the building. Numbers must be JSON numbers: the web defaults would also read <c>"3"</c>.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="BuildingId">The building, JSON <c>buildingId</c>: its <c>id</c> in the snapshot.</param>
/// <param name="Workers">The building's new worker count, JSON <c>workers</c>: absolute, 0 pauses it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AssignWorkersRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] int BuildingId,
    [property: JsonRequired] int Workers);
