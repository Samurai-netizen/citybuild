#nullable enable

using System;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The arms of a road tile (visual v2, for CR-0008's roads): a road model is a grass tile with a centre patch and four child meshes that
    /// reach from the centre to the tile's edges, named by the grid direction they point to. A road shows the arms that point at neighbouring
    /// roads, so neighbours join and a road alone is a patch. Each level has <see cref="Variants"/> models with the same arm widths at the tile
    /// edges, picked per tile by <see cref="VariantFor"/>. Grid +X is Unity +X and grid +Y is Unity +Z (<see cref="CityGrid.ToWorldCenter"/>).
    /// Display only.
    /// </summary>
    public static class RoadArms
    {
        /// <summary>The arm toward the tile at grid x + 1.</summary>
        public const string XPlus = "Road_XPlus";

        /// <summary>The arm toward the tile at grid x - 1.</summary>
        public const string XMinus = "Road_XMinus";

        /// <summary>The arm toward the tile at grid y + 1.</summary>
        public const string YPlus = "Road_YPlus";

        /// <summary>The arm toward the tile at grid y - 1.</summary>
        public const string YMinus = "Road_YMinus";

        /// <summary>The number of variants of each road level (the same arms at the tile edges, different details).</summary>
        public const int Variants = 3;

        /// <summary>What a variant's model name adds after <c>road_l&lt;level&gt;</c>, by variant.</summary>
        private static readonly string[] Suffixes = { string.Empty, "_b", "_c" };

        /// <summary>The path, under <c>Resources</c>, of the first road model of a road level: <c>Models/common/road_l1</c>.</summary>
        /// <param name="level">The road level, at least 1.</param>
        /// <returns>The path, without an extension.</returns>
        public static string PathOf(int level)
        {
            return PathOf(level, 0);
        }

        /// <summary>The path, under <c>Resources</c>, of one variant of a road level's model: <c>Models/common/road_l2_c</c>.</summary>
        /// <param name="level">The road level, at least 1.</param>
        /// <param name="variant">The variant, 0 to <see cref="Variants"/> - 1.</param>
        /// <returns>The path, without an extension.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The level is below 1 or the variant out of range.</exception>
        public static string PathOf(int level, int variant)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Road levels start at 1.");
            }

            if (variant < 0 || variant >= Variants)
            {
                throw new ArgumentOutOfRangeException(nameof(variant), variant, "A road level has " + Variants + " variants.");
            }

            return BuildingLooks.ModelsFolder + "/common/road_l" + level + Suffixes[variant];
        }

        /// <summary>The road variant of the tile at (<paramref name="x"/>, <paramref name="y"/>): from the top bits of <see cref="GroundVariants.Hash"/>.</summary>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>0 to <see cref="Variants"/> - 1.</returns>
        public static int VariantFor(int x, int y)
        {
            return (int)((GroundVariants.Hash(x, y) >> 24) % (uint)Variants);
        }

        /// <summary>Shows the arms of a road instance that point at neighbouring roads and hides the others. Allocates nothing.</summary>
        /// <param name="road">An instance of a road model.</param>
        /// <param name="xPlus">Whether the tile at grid x + 1 is a road.</param>
        /// <param name="xMinus">Whether the tile at grid x - 1 is a road.</param>
        /// <param name="yPlus">Whether the tile at grid y + 1 is a road.</param>
        /// <param name="yMinus">Whether the tile at grid y - 1 is a road.</param>
        /// <returns>The number of arms found (4 for a road model).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="road"/> is null.</exception>
        public static int Show(Transform road, bool xPlus, bool xMinus, bool yPlus, bool yMinus)
        {
            if (road == null)
            {
                throw new ArgumentNullException(nameof(road));
            }

            return Set(road, XPlus, xPlus) + Set(road, XMinus, xMinus) + Set(road, YPlus, yPlus) + Set(road, YMinus, yMinus);
        }

        /// <summary>Shows or hides one arm, if the road has it; returns 1 when it does.</summary>
        private static int Set(Transform road, string arm, bool shown)
        {
            Transform? child = BuildingViewFactory.FindByName(road, arm);
            if (child == null)
            {
                return 0;
            }

            if (child.gameObject.activeSelf != shown)
            {
                child.gameObject.SetActive(shown);
            }

            return 1;
        }
    }
}
