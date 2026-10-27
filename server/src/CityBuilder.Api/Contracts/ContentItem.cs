namespace CityBuilder.Api.Contracts;

/// <summary>One item in <see cref="ContentResponse.Items"/>: a resource that can sit in a city's inventory.</summary>
/// <param name="Id">The item's id, for example <c>logs</c>, JSON <c>id</c>.</param>
/// <param name="Name">The display name, JSON <c>name</c>.</param>
/// <param name="Era">The era the item belongs to, JSON <c>era</c>.</param>
/// <param name="Capped">Whether storage caps limit the item, JSON <c>capped</c>; only a capped item appears in a snapshot's <c>storageCaps</c>.</param>
/// <param name="Tradable">Whether players can trade the item on the market (Era II on), JSON <c>tradable</c>.</param>
public sealed record ContentItem(string Id, string Name, int Era, bool Capped, bool Tradable);
