#nullable enable

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Which grass tile a ground tile shows and how it is turned (visual v2, CR-0009): eight grass models, picked by a fixed hash of the tile's
    /// coordinates with weights (of every 20 tiles: plain 5, meadow 2, stones 2, clover 3, dry grass 2, mushrooms 2, molehill 2, bluebells 2),
    /// and a quarter turn from other bits of the same hash, so the ground looks varied but the same on every device and every run. The road
    /// variants use the hash too (<see cref="RoadArms.VariantFor"/>). The art previews use the same rules (<c>ground_variant</c>,
    /// <c>ground_turns</c> and <c>road_variant</c> in <c>visual-v2/blender/render_previews.py</c>). Display only.
    /// </summary>
    public static class GroundVariants
    {
        /// <summary>The number of grass variants.</summary>
        public const int Count = 8;

        /// <summary>The sum of <see cref="Weights"/>: the variants repeat their mix over every this many hash values.</summary>
        public const int WeightTotal = 20;

        /// <summary>The model names of the variants under <c>Models/common/</c>, in the order <see cref="For"/> picks them.</summary>
        private static readonly string[] Names =
        {
            "tile_grass", "tile_grass_b", "tile_grass_c", "tile_grass_d", "tile_grass_e", "tile_grass_f", "tile_grass_g", "tile_grass_h",
        };

        /// <summary>How many of every <see cref="WeightTotal"/> tiles show each variant, in the order of <see cref="Names"/>; never changed.</summary>
        private static readonly int[] Weights = { 5, 2, 2, 3, 2, 2, 2, 2 };

        /// <summary>The path, under <c>Resources</c>, of a variant's model: <c>Models/common/tile_grass_b</c>.</summary>
        /// <param name="variant">The variant, 0 to <see cref="Count"/> - 1.</param>
        /// <returns>The path, without an extension.</returns>
        public static string PathOf(int variant)
        {
            return BuildingLooks.ModelsFolder + "/common/" + Names[variant];
        }

        /// <summary>The fixed hash of a tile: a mix of its coordinates in unsigned 32-bit arithmetic, the same in the art's previews.</summary>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>The hash.</returns>
        public static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>The grass variant of the tile at (<paramref name="x"/>, <paramref name="y"/>): the hash modulo 20 falls into one of the weights.</summary>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>0 to <see cref="Count"/> - 1.</returns>
        public static int For(int x, int y)
        {
            int bucket = (int)(Hash(x, y) % (uint)WeightTotal);
            for (int variant = 0; variant < Weights.Length; variant++)
            {
                if (bucket < Weights[variant])
                {
                    return variant;
                }

                bucket -= Weights[variant];
            }

            return 0;
        }

        /// <summary>How many quarter turns the tile's grass is turned by (about the vertical axis): bits 20 and 21 of the hash.</summary>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>0 to 3.</returns>
        public static int RotationFor(int x, int y)
        {
            return (int)((Hash(x, y) >> 20) & 3u);
        }
    }
}
