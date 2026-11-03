namespace CityBuilder.Application.Commands.Roads;

/// <summary>
/// The tile list the three road commands share (<c>core-instructions/05-api-contract.md</c>, <c>place-roads</c>, <c>upgrade-roads</c>,
/// <c>remove-roads</c>): its shape check before the lock and its form for the engine's <c>RoadRules</c>.
/// </summary>
internal static class RoadTileList
{
    /// <summary>
    /// The most tiles one road command may name: a dragged line across a large grid (build log BL-20261102-07, decision 14). A fixed limit
    /// of the API, not of the content, so the boundary checks it without reading the content (owner directive OD-13).
    /// </summary>
    public const int MaxTiles = 64;

    /// <summary>
    /// Refuses what no city could make valid (OD-13): no tile, more than <see cref="MaxTiles"/>, a missing tile, a negative coordinate, or a
    /// tile named twice. Everything else is the rule's: a tile outside the grid is <c>OUT_OF_BOUNDS</c> (placing) or <c>ROAD_NOT_FOUND</c>.
    /// </summary>
    /// <param name="tiles">The command's tiles.</param>
    /// <returns><see langword="null"/>, or what is wrong, naming the first tile that is.</returns>
    public static string? ValidateShape(IReadOnlyList<CommandTile?>? tiles)
    {
        if (tiles is null || tiles.Count == 0 || tiles.Count > MaxTiles)
        {
            return $"tiles must list 1 to {MaxTiles} tiles.";
        }

        var seen = new HashSet<CommandTile>();
        foreach (CommandTile? tile in tiles)
        {
            if (tile is null)
            {
                return "tiles must hold tile objects { x, y }, not null.";
            }

            if (tile.X < 0 || tile.Y < 0)
            {
                return $"tiles must have coordinates of at least 0: ({tile.X}, {tile.Y}).";
            }

            if (!seen.Add(tile))
            {
                return $"tiles must name each tile once: ({tile.X}, {tile.Y}) is there twice.";
            }
        }

        return null;
    }

    /// <summary>The tiles in the engine's form, in the command's order. Call it only after <see cref="ValidateShape"/> accepted them.</summary>
    /// <param name="tiles">The command's tiles, every one set.</param>
    /// <returns>The tiles as the engine's <c>(X, Y)</c> pairs.</returns>
    public static IReadOnlyList<(int X, int Y)> ToEngine(IReadOnlyList<CommandTile?> tiles) =>
        [.. tiles.Select(tile => (tile!.X, tile.Y))];
}
