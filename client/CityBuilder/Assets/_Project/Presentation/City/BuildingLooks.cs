#nullable enable

using System.Collections.Generic;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The placeholder looks of the buildings (M0-44), found by building type and era (<c>09</c>, "UX rules": art is era-aware from the
    /// start). M0 has only Era I, so every type has one look, filed under era 1; a later era adds entries for the types whose look changes, and a
    /// type without an entry for the city's era keeps its latest earlier look. A type this table doesn't know gets <see cref="Fallback"/>,
    /// so a building from newer content is still drawn. The table is a code table, not an asset (M0-44 decision 1); it holds colours and shapes only,
    /// never a game number. Display only.
    /// </summary>
    public static class BuildingLooks
    {
        /// <summary>The Cottage's type id in the content file.</summary>
        public const string House = "house";

        /// <summary>The Woodcutter's Hut's type id in the content file.</summary>
        public const string Woodcutter = "woodcutter";

        /// <summary>The Sawmill's type id in the content file.</summary>
        public const string Sawmill = "sawmill";

        /// <summary>The Trading Post's type id in the content file.</summary>
        public const string TradingPost = "trading_post";

        /// <summary>The height of a level 1 building, and of a finished one, in thousandths: the unit of <see cref="HeightPermille"/>.</summary>
        public const int FullHeightPermille = 1000;

        /// <summary>How much taller each level above 1 makes a building, in thousandths of its level 1 height (20 %).</summary>
        public const int HeightPerLevelPermille = 200;

        /// <summary>How tall a building under construction is, in thousandths: a low foundation, so nothing finished shows before the server says it is done.</summary>
        public const int ConstructionHeightPermille = 250;

        /// <summary>The earliest era a look can be filed under; the lookup stops there.</summary>
        private const int FirstEra = 1;

        /// <summary>The wall colour of the plain buildings.</summary>
        private static readonly Color Plaster = new Color(0.86f, 0.80f, 0.64f);

        /// <summary>The colour of a roof of red tiles.</summary>
        private static readonly Color RedRoof = new Color(0.62f, 0.25f, 0.18f);

        /// <summary>The colour of timber walls and flat roofs.</summary>
        private static readonly Color Timber = new Color(0.45f, 0.31f, 0.19f);

        /// <summary>A darker timber, for roofs and wheels.</summary>
        private static readonly Color DarkTimber = new Color(0.30f, 0.21f, 0.13f);

        /// <summary>The colour of cut logs.</summary>
        private static readonly Color Log = new Color(0.62f, 0.45f, 0.26f);

        /// <summary>The looks, filed by type id and era.</summary>
        private static readonly Dictionary<(string TypeId, int Era), BuildingLook> Table = CreateTable();

        /// <summary>The look of a type the table doesn't know: a plain grey box.</summary>
        public static readonly BuildingLook Fallback = new BuildingLook(
            "unknown",
            new[] { BuildingPart.Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.5f), new Color(0.6f, 0.55f, 0.65f)) });

        /// <summary>The number of looks in the table.</summary>
        public static int Count => Table.Count;

        /// <summary>
        /// The look for a building type in an era: the entry filed under that era, or else the latest earlier one, or else <see cref="Fallback"/>.
        /// Allocates nothing.
        /// </summary>
        /// <param name="typeId">The building type's id in the content.</param>
        /// <param name="era">The era to draw it in, at least 1.</param>
        /// <returns>The look; never null.</returns>
        public static BuildingLook Get(string typeId, int era)
        {
            for (int candidate = era; candidate >= FirstEra; candidate--)
            {
                if (Table.TryGetValue((typeId, candidate), out BuildingLook? look))
                {
                    return look;
                }
            }

            return Fallback;
        }

        /// <summary>Whether the table has a look filed under exactly this type and era (no fallback, no earlier era).</summary>
        /// <param name="typeId">The building type's id.</param>
        /// <param name="era">The era.</param>
        /// <returns>True when an entry exists.</returns>
        public static bool Has(string typeId, int era)
        {
            return Table.ContainsKey((typeId, era));
        }

        /// <summary>
        /// How tall a building is drawn, in thousandths of its level 1 height: a building under construction is a low foundation, a finished or
        /// upgrading one is 20 % taller for each level above 1 (the spec: level 2 is 20 % taller).
        /// </summary>
        /// <param name="level">The building's current level, at least 1.</param>
        /// <param name="phase">What the building is doing.</param>
        /// <returns>The height in thousandths.</returns>
        public static int HeightPermille(int level, BuildingPhase phase)
        {
            if (phase == BuildingPhase.Constructing)
            {
                return ConstructionHeightPermille;
            }

            int extraLevels = level > 1 ? level - 1 : 0;
            return FullHeightPermille + HeightPerLevelPermille * extraLevels;
        }

        /// <summary>Builds the table of the Era I looks.</summary>
        private static Dictionary<(string TypeId, int Era), BuildingLook> CreateTable()
        {
            return new Dictionary<(string TypeId, int Era), BuildingLook>
            {
                // Cottage: a small box with a pitched roof (a box turned 45 degrees; its lower half sits inside the walls).
                [(House, 1)] = new BuildingLook("cottage", new[]
                {
                    BuildingPart.Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.5f, 0.6f), Plaster),
                    BuildingPart.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.42f, 0.42f, 0.7f), new Vector3(0f, 0f, 45f), RedRoof),
                }),

                // Woodcutter's Hut: a low box with a flat roof and a pile of logs in front.
                [(Woodcutter, 1)] = new BuildingLook("woodcutters-hut", new[]
                {
                    BuildingPart.Box(new Vector3(0f, 0.15f, -0.1f), new Vector3(0.6f, 0.3f, 0.45f), Timber),
                    BuildingPart.Box(new Vector3(0f, 0.32f, -0.1f), new Vector3(0.66f, 0.04f, 0.5f), DarkTimber),
                    BuildingPart.CylinderAlongZ(new Vector3(-0.1f, 0.06f, 0.3f), 0.12f, 0.3f, Log),
                    BuildingPart.CylinderAlongZ(new Vector3(0.1f, 0.06f, 0.3f), 0.12f, 0.3f, Log),
                    BuildingPart.CylinderAlongZ(new Vector3(0f, 0.17f, 0.3f), 0.12f, 0.3f, Log),
                }),

                // Sawmill: a longer box with a wheel on its front.
                [(Sawmill, 1)] = new BuildingLook("sawmill", new[]
                {
                    BuildingPart.Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 0.5f), new Color(0.58f, 0.50f, 0.40f)),
                    BuildingPart.Box(new Vector3(0f, 0.42f, 0f), new Vector3(0.94f, 0.04f, 0.56f), DarkTimber),
                    BuildingPart.CylinderAlongZ(new Vector3(0.2f, 0.22f, 0.3f), 0.4f, 0.06f, DarkTimber),
                }),

                // Trading Post: a box with a red awning tilted down over the front.
                [(TradingPost, 1)] = new BuildingLook("trading-post", new[]
                {
                    BuildingPart.Box(new Vector3(0f, 0.22f, -0.1f), new Vector3(0.7f, 0.44f, 0.5f), new Color(0.78f, 0.68f, 0.48f)),
                    BuildingPart.Box(new Vector3(0f, 0.42f, 0.22f), new Vector3(0.76f, 0.04f, 0.34f), new Vector3(25f, 0f, 0f), new Color(0.75f, 0.22f, 0.20f)),
                }),
            };
        }
    }
}
