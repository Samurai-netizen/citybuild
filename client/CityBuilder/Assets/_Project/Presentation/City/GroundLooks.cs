#nullable enable

using CityBuilder.Simulation.Content;
using UnityEngine;
using Terrain = CityBuilder.Simulation.Content.Terrain;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The look of a ground tile by its terrain (M1-03, AR-M1-03): which model it shows, turned by how many quarter turns, and the flat tint it
    /// shows while that model is missing. Grass keeps the eight weighted variants of visual v2 (<see cref="GroundVariants"/>); forest, rock and
    /// soil pick one of three models and water one of two, by the tile's fixed hash, so the same map always looks the same. Soil and water turn
    /// only by half turns, so the furrows and the river keep their direction. Display only.
    /// </summary>
    public static class GroundLooks
    {
        /// <summary>The forest tile models under <c>Models/common/</c>.</summary>
        private static readonly string[] ForestNames = { "tile_forest", "tile_forest_b", "tile_forest_c" };

        /// <summary>The rock tile models under <c>Models/common/</c>.</summary>
        private static readonly string[] RockNames = { "tile_rock", "tile_rock_b", "tile_rock_c" };

        /// <summary>The fertile soil tile models under <c>Models/common/</c>.</summary>
        private static readonly string[] SoilNames = { "tile_soil", "tile_soil_b", "tile_soil_c" };

        /// <summary>The river tile models under <c>Models/common/</c>.</summary>
        private static readonly string[] WaterNames = { "tile_water", "tile_water_b" };

        /// <summary>The placeholder tint of grass: the lighter chessboard green of the ground.</summary>
        private static readonly Color GrassTint = new Color(0.45f, 0.62f, 0.32f);

        /// <summary>The placeholder tint of forest: a dark green.</summary>
        private static readonly Color ForestTint = new Color(0.18f, 0.38f, 0.20f);

        /// <summary>The placeholder tint of rock: a grey.</summary>
        private static readonly Color RockTint = new Color(0.55f, 0.55f, 0.52f);

        /// <summary>The placeholder tint of fertile soil: a brown.</summary>
        private static readonly Color SoilTint = new Color(0.50f, 0.36f, 0.22f);

        /// <summary>The placeholder tint of water: a blue.</summary>
        private static readonly Color WaterTint = new Color(0.25f, 0.50f, 0.75f);

        /// <summary>The path, under <c>Resources</c>, of the model the tile at (<paramref name="x"/>, <paramref name="y"/>) shows for its terrain.</summary>
        /// <param name="terrain">The tile's terrain.</param>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>The path, without an extension: <c>Models/common/tile_forest_b</c>.</returns>
        public static string ModelPathFor(Terrain terrain, int x, int y)
        {
            switch (terrain)
            {
                case Terrain.Forest:
                    return PathOf(ForestNames, x, y);
                case Terrain.Rock:
                    return PathOf(RockNames, x, y);
                case Terrain.Soil:
                    return PathOf(SoilNames, x, y);
                case Terrain.Water:
                    return PathOf(WaterNames, x, y);
                default:
                    return GroundVariants.PathOf(GroundVariants.For(x, y));
            }
        }

        /// <summary>
        /// How many quarter turns about the vertical axis the tile's model is turned by: the grass turn of visual v2, any quarter turn for forest
        /// and rock (bits 20 and 21 of the hash), and 0 or 2 for soil and water (bit 20).
        /// </summary>
        /// <param name="terrain">The tile's terrain.</param>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>0 to 3.</returns>
        public static int QuarterTurnsFor(Terrain terrain, int x, int y)
        {
            switch (terrain)
            {
                case Terrain.Soil:
                case Terrain.Water:
                    return (int)((GroundVariants.Hash(x, y) >> 20) & 1u) * 2;
                default:
                    return GroundVariants.RotationFor(x, y);
            }
        }

        /// <summary>The flat tint a tile of <paramref name="terrain"/> shows while its model is missing (the AR-M1-03 placeholder).</summary>
        /// <param name="terrain">The tile's terrain.</param>
        /// <returns>The terrain's own colour.</returns>
        public static Color PlaceholderColorFor(Terrain terrain)
        {
            switch (terrain)
            {
                case Terrain.Forest:
                    return ForestTint;
                case Terrain.Rock:
                    return RockTint;
                case Terrain.Soil:
                    return SoilTint;
                case Terrain.Water:
                    return WaterTint;
                default:
                    return GrassTint;
            }
        }

        /// <summary>The path of one of <paramref name="names"/>, picked by the tile's hash (bits 8 and up, apart from the turn's bits).</summary>
        /// <param name="names">The terrain's model names.</param>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>The path under <c>Resources</c>.</returns>
        private static string PathOf(string[] names, int x, int y)
        {
            int index = (int)((GroundVariants.Hash(x, y) >> 8) % (uint)names.Length);
            return BuildingLooks.ModelsFolder + "/common/" + names[index];
        }
    }
}
