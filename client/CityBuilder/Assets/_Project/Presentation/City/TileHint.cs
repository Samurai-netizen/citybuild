namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// What the placement flow tells the player about a tile before the server is asked (<c>09</c> UX rules): a hint from the latest city, never
    /// a decision. The ground paints a free tile green, an occupied one red and a blocked one grey; a closed tile keeps its ground under the
    /// mist (M1-03).
    /// </summary>
    public enum TileHint
    {
        /// <summary>No building stands on the tile in the displayed city.</summary>
        Free,

        /// <summary>A building, in any phase, stands on the tile in the displayed city.</summary>
        Occupied,

        /// <summary>The picked type can never stand on the tile: water, forest or rock, or its site is unmet (M1-03).</summary>
        Blocked,

        /// <summary>The tile's land is still under mist: research opens it (M1-03).</summary>
        Closed,
    }
}
