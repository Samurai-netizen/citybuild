namespace CityBuilder.Application.Commands.SetRoute;

/// <summary>
/// An action at a stop of a <see cref="SetRouteCommand"/>'s route (<c>core-instructions/05-api-contract.md</c>, <c>{ kind, itemId, amount }</c>),
/// as text the shape check reads: what the player chose, never what is in a store.
/// </summary>
/// <param name="Kind"><c>take</c> (from the building's store onto the courier) or <c>put</c> (from the courier into the building).</param>
/// <param name="ItemId">The good, an item id of the content.</param>
/// <param name="Amount">How much, in the API's form (<c>05</c> → Amounts): a preset id, <c>1%</c>…<c>100%</c>, or a number.</param>
public sealed record CommandRouteAction(string Kind, string ItemId, string Amount);
