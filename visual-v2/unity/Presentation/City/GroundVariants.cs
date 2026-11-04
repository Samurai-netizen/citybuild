#nullable enable

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Which grass tile a ground tile shows (visual v2, CR-0009): four grass models, picked by a fixed hash of the tile's coordinates, so the
    /// ground looks varied but the same on every device and every run. The plain tile shows on 4 tiles in 10, the flowers, the pebbles and the
    /// clover on 2 in 10 each. The art previews use the same rule (<c>ground_variant</c> in <c>visual-v2/blender/render_previews.py</c>). Display only.
    /// </summary>
    public static class GroundVariants
    {
        /// <summary>The number of grass variants.</summary>
        public const int Count = 4;

        /// <summary>The model names of the variants under <c>Models/common/</c>, in the order <see cref="For"/> picks them.</summary>
        private static readonly string[] Names = { "tile_grass", "tile_grass_b", "tile_grass_c", "tile_grass_d" };

        /// <summary>The path, under <c>Resources</c>, of a variant's model: <c>Models/common/tile_grass_b</c>.</summary>
        /// <param name="variant">The variant, 0 to <see cref="Count"/> - 1.</param>
        /// <returns>The path, without an extension.</returns>
        public static string PathOf(int variant)
        {
            return BuildingLooks.ModelsFolder + "/common/" + Names[variant];
        }

        /// <summary>The variant of the tile at (<paramref name="x"/>, <paramref name="y"/>): a fixed mix of the coordinates, then its last digit picks the variant.</summary>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>0 (plain), 1 (flowers), 2 (pebbles) or 3 (clover).</returns>
        public static int For(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                uint bucket = h % 10u;
                return bucket < 4u ? 0 : bucket < 6u ? 1 : bucket < 8u ? 2 : 3;
            }
        }
    }
}
