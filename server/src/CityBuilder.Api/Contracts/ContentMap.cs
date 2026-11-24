namespace CityBuilder.Api.Contracts;

/// <summary>
/// The map every city stands on, the <c>map</c> object of <see cref="ContentResponse"/> (<c>core-instructions/04-simulation-model.md</c>
/// section 17, M1-03), spelled like the content file so the client builds the same content from it.
/// </summary>
/// <param name="Id">The map's id, JSON <c>id</c>.</param>
/// <param name="StartArea">The area letter open from the start, JSON <c>startArea</c>.</param>
/// <param name="RoughTileMillis">How long a walker takes to cross a forest or rock tile, in milliseconds, JSON <c>roughTileMillis</c>.</param>
/// <param name="Tiles">The terrain, JSON <c>tiles</c>: one string per row from y = 0, one letter per tile from x = 0: <c>.</c> grass,
/// <c>F</c> forest, <c>R</c> rock, <c>S</c> fertile soil, <c>W</c> water.</param>
/// <param name="Areas">The areas, JSON <c>areas</c>: one string per row, one capital letter per tile; an area other than the start area
/// opens with an <c>openLand</c> effect.</param>
public sealed record ContentMap(string Id, string StartArea, long RoughTileMillis, IReadOnlyList<string> Tiles, IReadOnlyList<string> Areas);
