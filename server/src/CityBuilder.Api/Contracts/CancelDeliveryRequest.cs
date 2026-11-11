using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/commands/cancel-delivery</c> (core-instructions/05-api-contract.md, CR-0008 amendment 1): which courier, and one
/// of its delivery lines or <c>null</c> for all of them and its route. It is intent: it names no goods, no quantity and no time, and a body that
/// sends an unknown property is refused (400) instead of having it ignored.
/// </summary>
/// <param name="RequestId">The command's id, JSON <c>requestId</c>: a UUID the client made and reuses on every retry.</param>
/// <param name="StationId">The courier station's building id, JSON <c>stationId</c>.</param>
/// <param name="Slot">The courier's slot at the station, JSON <c>slot</c>.</param>
/// <param name="LineId">The delivery line to cancel, JSON <c>lineId</c>; <c>null</c> cancels every line of the courier and its route ("Cancel all").</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record CancelDeliveryRequest(
    [property: JsonRequired] Guid RequestId,
    [property: JsonRequired] int StationId,
    [property: JsonRequired] int Slot,
    [property: JsonRequired] int? LineId);
