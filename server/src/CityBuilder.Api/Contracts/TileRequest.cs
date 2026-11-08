using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// One tile of a request's <c>tiles</c> list (core-instructions/05-api-contract.md, CR-0008): a column and a row, both required JSON numbers,
/// and no other property (a <c>level</c> or <c>cost</c> is refused, 400).
/// </summary>
/// <param name="X">The tile column, JSON <c>x</c>.</param>
/// <param name="Y">The tile row, JSON <c>y</c>.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record TileRequest(
    [property: JsonRequired] int X,
    [property: JsonRequired] int Y);
