namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// What the placement flow tells the player about a tile before the server is asked (<c>09</c> UX rules): a hint from the latest city, never
    /// a decision. The ground paints a free tile green and an occupied one red.
    /// </summary>
    public enum TileHint
    {
        /// <summary>No building stands on the tile in the displayed city.</summary>
        Free,

        /// <summary>A building, in any phase, stands on the tile in the displayed city.</summary>
        Occupied,
    }
}
