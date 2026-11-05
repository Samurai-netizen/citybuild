using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>One action at a stop of a <see cref="SetRouteRequest"/> (CR-0008). Every field is required and a JSON string.</summary>
/// <param name="Kind">The action, JSON <c>kind</c>: <c>take</c> or <c>put</c>.</param>
/// <param name="ItemId">The good, JSON <c>itemId</c>, for example <c>logs</c>.</param>
/// <param name="Amount">How much, JSON <c>amount</c>: a preset id (<c>half</c>), a percentage (<c>50%</c>) or a number (<c>12</c>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record RouteActionRequest(
    [property: JsonRequired] string? Kind,
    [property: JsonRequired] string? ItemId,
    [property: JsonRequired] string? Amount);
