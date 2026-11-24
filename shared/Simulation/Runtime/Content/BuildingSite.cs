#nullable enable
namespace CityBuilder.Simulation.Content
{
    /// <summary>Where a building type may stand on the map (<c>04</c> section 8, M1-03). Every type stands only on grass or fertile soil.</summary>
    public enum BuildingSite
    {
        /// <summary>Grass or fertile soil, nothing more (JSON: no <c>site</c>).</summary>
        Any,

        /// <summary>Grass or fertile soil with a forest tile beside it, in one of the four directions (JSON <c>nearForest</c>).</summary>
        NearForest,

        /// <summary>Grass or fertile soil with a rock tile beside it, in one of the four directions (JSON <c>nearRock</c>).</summary>
        NearRock,

        /// <summary>Fertile soil only (JSON <c>fertileSoil</c>).</summary>
        FertileSoil,
    }
}
