using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/reenergize-couriers</c> (core-instructions/05-api-contract.md, CR-0008): the request id only. It is
/// intent: it has no stamina, courier or player field, and a body that sends one is refused (400) instead of having it ignored.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record ReenergizeCouriersRequest([property: JsonRequired] Guid RequestId);
