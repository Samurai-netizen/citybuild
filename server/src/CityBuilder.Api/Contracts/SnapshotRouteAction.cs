namespace CityBuilder.Api.Contracts;

/// <summary>One action at a route stop in <see cref="SnapshotRouteStop.Actions"/> (CR-0008), as <c>set-route</c> sends it.</summary>
/// <param name="Kind">The action, JSON <c>kind</c>: <c>take</c> (from the building's store) or <c>put</c> (into the building).</param>
/// <param name="ItemId">The good, JSON <c>itemId</c>.</param>
/// <param name="Amount">How much, JSON <c>amount</c>, in the API's form (<c>05</c> → Amounts): a preset id, a percentage or a number.</param>
public sealed record SnapshotRouteAction(string Kind, string ItemId, string Amount);
