namespace CityBuilder.Api.Contracts;

/// <summary>
/// The bridge, the <c>bridge</c> object of <see cref="ContentResponse"/> (M1-03): a road placed on a water tile once an
/// <c>unlockBridge</c> effect is researched; it has one level and can't be upgraded.
/// </summary>
/// <param name="Name">The display name, JSON <c>name</c>.</param>
/// <param name="TileMillis">How long a walker takes to cross a bridge tile, in milliseconds, JSON <c>tileMillis</c>.</param>
/// <param name="Cost">What one bridge tile costs, JSON <c>cost</c>: item id to quantity, by item id (coins only).</param>
public sealed record ContentBridge(string Name, long TileMillis, IReadOnlyDictionary<string, long> Cost);
