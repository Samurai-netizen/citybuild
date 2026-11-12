using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/set-paused</c> (core-instructions/05-api-contract.md, CR-0008 amendment 2): which building and whether it
/// is paused, nothing else. It is intent: it names no time, worker or courier, and a body that sends an unknown property is refused (400)
/// instead of having it ignored. Every field is required: a missing <c>paused</c> would otherwise read as false and resume the building.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="BuildingId">The building, JSON <c>buildingId</c>: its <c>id</c> in the snapshot.</param>
/// <param name="Paused">JSON <c>paused</c>: <c>true</c> pauses the building, <c>false</c> resumes it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record SetPausedRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] int BuildingId,
    [property: JsonRequired] bool Paused);
