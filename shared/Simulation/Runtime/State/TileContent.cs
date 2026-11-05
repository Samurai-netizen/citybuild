#nullable enable

namespace CityBuilder.Simulation.State
{
    /// <summary>What lies on a tile of a city (<c>core-instructions/04-simulation-model.md</c>, section 17).</summary>
    public enum TileContent
    {
        /// <summary>Nothing: open ground.</summary>
        Ground,

        /// <summary>A road (<see cref="RoadTile"/>).</summary>
        Road,

        /// <summary>A building, in any phase.</summary>
        Building,
    }
}
