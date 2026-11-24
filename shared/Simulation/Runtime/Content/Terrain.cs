#nullable enable
namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// What a tile of the map is (<c>core-instructions/22-game-mechanics.md</c> → Terrain, <c>04</c> section 17). Terrain never changes and never
    /// runs out. Grass and fertile soil take buildings and roads; forest and rock take neither and are walked slowly; water takes only a bridge.
    /// </summary>
    public enum Terrain
    {
        /// <summary>Grass (<c>.</c>): buildings and roads, walked at the ground time.</summary>
        Grass,

        /// <summary>Forest (<c>F</c>): no building or road; a Woodcutter's Hut needs one beside it; walked at the rough time.</summary>
        Forest,

        /// <summary>Rock (<c>R</c>): no building or road; a Quarry needs one beside it; walked at the rough time.</summary>
        Rock,

        /// <summary>Fertile soil (<c>S</c>): like grass, and the only terrain a Farm stands on.</summary>
        Soil,

        /// <summary>Water (<c>W</c>): no building; only a bridge, and walked only on a bridge.</summary>
        Water,
    }
}
