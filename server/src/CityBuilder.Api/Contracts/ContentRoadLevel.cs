namespace CityBuilder.Api.Contracts;

/// <summary>One road level in <see cref="ContentResponse.Roads"/> (<c>core-instructions/04-simulation-model.md</c>, section 17).</summary>
/// <param name="Level">The level number, from 1 (a newly placed road), JSON <c>level</c>.</param>
/// <param name="Name">The display name, for example <c>Dirt path</c>, JSON <c>name</c>.</param>
/// <param name="TileMillis">How long a walker takes to cross one tile of it, in milliseconds, JSON <c>tileMillis</c>.</param>
/// <param name="Cost">What one tile costs, JSON <c>cost</c>: to place it at level 1, to upgrade it to this level above; item id to quantity, by item id.</param>
public sealed record ContentRoadLevel(int Level, string Name, long TileMillis, IReadOnlyDictionary<string, long> Cost);
