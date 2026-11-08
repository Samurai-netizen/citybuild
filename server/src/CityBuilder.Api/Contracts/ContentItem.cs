namespace CityBuilder.Api.Contracts;

/// <summary>One item in <see cref="ContentResponse.Items"/>: a good that sits in building stores, or the currency, which sits in the purse.</summary>
/// <param name="Id">The item's id, for example <c>logs</c>, JSON <c>id</c>.</param>
/// <param name="Name">The display name, JSON <c>name</c>.</param>
/// <param name="Era">The era the item belongs to, JSON <c>era</c>.</param>
/// <param name="Currency">Whether the item is the currency, JSON <c>currency</c>: exactly one is, kept in the purse, never in a store or carried (CR-0008).</param>
/// <param name="Tradable">Whether players can trade the item on the market (Era II on), JSON <c>tradable</c>.</param>
public sealed record ContentItem(string Id, string Name, int Era, bool Currency, bool Tradable);
