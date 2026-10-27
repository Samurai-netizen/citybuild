using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/place-building</c> (core-instructions/05-api-contract.md): what the player chose, nothing else. It has
/// no cost, level, time, worker or player field, and a body that sends one is refused (400) instead of having it ignored, so a client can't
/// believe it set one. Every field is required: a missing <c>x</c> would otherwise read as column 0. Numbers must be JSON numbers: the web
/// defaults would also read <c>"2"</c>, and the contract's quantities are integers.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="BuildingTypeId">The type to place, JSON <c>buildingTypeId</c>, for example <c>woodcutter</c>.</param>
/// <param name="X">The tile's column, JSON <c>x</c>.</param>
/// <param name="Y">The tile's row, JSON <c>y</c>.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record PlaceBuildingRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] string? BuildingTypeId,
    [property: JsonRequired] int X,
    [property: JsonRequired] int Y);
