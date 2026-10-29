using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/start-research</c> (core-instructions/05-api-contract.md): which research node, nothing else. It has
/// no cost, duration, end time, completed-research or player field, and a body that sends one is refused (400) instead of having it ignored,
/// so a client can't believe it set one. Every field is required. The body has no number, so it needs no number handling of its own; a
/// number sent for <c>researchId</c> is refused by the binding, as any value that isn't a JSON string.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="ResearchId">The node to start, JSON <c>researchId</c>, for example <c>sharp_axes</c>.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartResearchRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] string? ResearchId);
