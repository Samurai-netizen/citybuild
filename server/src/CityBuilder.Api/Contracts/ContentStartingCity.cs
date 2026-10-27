namespace CityBuilder.Api.Contracts;

/// <summary>
/// What a new city starts with, the <c>startingCity</c> object of <see cref="ContentResponse"/>. A player's city comes from <c>GET /city</c>;
/// this is here because the content is only complete with it: the validating content factory the client builds its <c>GameContent</c> with
/// (<c>core-instructions/09-unity-client.md</c>) requires it, as the server does.
/// </summary>
/// <param name="Era">The number of the era a new city starts in, JSON <c>era</c>.</param>
/// <param name="Inventory">The starting stock, JSON <c>inventory</c>: item id to quantity, by item id; an item that is not listed starts at 0.</param>
public sealed record ContentStartingCity(int Era, IReadOnlyDictionary<string, long> Inventory);
