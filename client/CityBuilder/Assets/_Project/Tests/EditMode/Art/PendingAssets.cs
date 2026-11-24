#nullable enable

namespace CityBuilder.Client.Tests.EditMode.Art
{
    /// <summary>
    /// The "pending" list of <c>core-instructions/27-asset-pipeline.md</c> → Placeholders: the assets the content needs that have not arrived
    /// yet, so the client draws their code-made placeholders. Every id here is asked for in <c>docs/assets/requests/AR-M1-09.md</c> (the
    /// pictures also in <c>BATCH-01-pictures.md</c>); M1-09 empties the list. <see cref="ArtModelsTests"/> and <see cref="UiIconsTests"/> skip
    /// these ids and fail on one whose file has arrived, so a delivered asset leaves the list in the same change.
    /// </summary>
    internal static class PendingAssets
    {
        /// <summary>The building models (<c>&lt;typeId&gt;_l&lt;level&gt;</c>, Era I) still drawn as tinted boxes or as their lower level's primitives.</summary>
        public static readonly string[] Models =
        {
            "bakery_l1", "bakery_l2", "bakery_l3", "courier_station_l3", "farm_l1", "farm_l2", "farm_l3", "house_l3",
            "quarry_l1", "quarry_l2", "quarry_l3", "sawmill_l3", "stonemason_l1", "stonemason_l2", "stonemason_l3",
            "town_hall_l1", "town_hall_l2", "town_hall_l3", "trading_post_l3", "warehouse_l3", "windmill_l1", "windmill_l2",
            "windmill_l3", "woodcutter_l3",
        };

        /// <summary>The carried-good models (<c>carry_&lt;item&gt;</c>) still drawn as a plain box on the walker.</summary>
        public static readonly string[] CarryModels =
        {
            "carry_bread", "carry_cut_stone", "carry_flour", "carry_grain", "carry_stone",
        };

        /// <summary>The UI icons (<c>item_</c>, <c>bld_</c>, <c>res_</c> and the id) still shown as initials tiles.</summary>
        public static readonly string[] Icons =
        {
            "item_bread", "item_cut_stone", "item_flour", "item_grain", "item_stone",
            "bld_bakery", "bld_farm", "bld_quarry", "bld_stonemason", "bld_town_hall", "bld_windmill",
            "res_bridge_building", "res_guild_of_masons", "res_honest_scales", "res_land_survey", "res_masonry", "res_milling",
            "res_open_fields", "res_stone_cutting", "res_timber_framing", "res_town_charter",
        };

        /// <summary>Whether a name is on one of the lists.</summary>
        /// <param name="list">The list.</param>
        /// <param name="name">The name.</param>
        /// <returns>True when the list holds it.</returns>
        public static bool Contains(string[] list, string name) => System.Array.IndexOf(list, name) >= 0;
    }
}
