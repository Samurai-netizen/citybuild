using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/start-trip</c> (core-instructions/05-api-contract.md, CR-0008): which goods go where, nothing else.
/// It has no quantity, time, worker or player field, and a body that sends one is refused (400) instead of having it ignored, so a client can't
/// believe it set one. Every field is required. Numbers must be JSON numbers, and the amount is a JSON string in one of its three forms.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="SourceId">The building whose store the goods come from, JSON <c>sourceId</c>.</param>
/// <param name="TargetId">The building they go to, JSON <c>targetId</c>.</param>
/// <param name="ItemId">The good, JSON <c>itemId</c>, for example <c>logs</c>.</param>
/// <param name="Amount">How much of the source's stock, JSON <c>amount</c>: a preset id (<c>half</c>), a percentage (<c>50%</c>) or a number (<c>12</c>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record StartTripRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] int SourceId,
    [property: JsonRequired] int TargetId,
    [property: JsonRequired] string? ItemId,
    [property: JsonRequired] string? Amount);
