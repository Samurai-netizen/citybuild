#nullable enable
using System.Globalization;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The error paths of the rule phase, in the same form as the shape phase's (<see cref="ContentError.Path"/>). The content has the right
    /// shape by then, so every element has its id, and a level its number.
    /// </summary>
    internal static class ContentPaths
    {
        /// <summary>An era by its text id: <c>eras[early_medieval]</c>.</summary>
        public static string Era(EraDefinition era) => "eras[" + era.Id.Value + "]";

        /// <summary>An item by its id: <c>items[planks]</c>.</summary>
        public static string Item(ItemDefinition item) => "items[" + item.Id.Value + "]";

        /// <summary>A building type by its id: <c>buildings[sawmill]</c>.</summary>
        public static string Building(BuildingDefinition building) => "buildings[" + building.Id.Value + "]";

        /// <summary>A level by its number inside its building type: <c>buildings[sawmill].levels[2]</c>.</summary>
        public static string Level(BuildingDefinition building, BuildingLevelDefinition level) =>
            Building(building) + ".levels[" + level.Level.ToString(CultureInfo.InvariantCulture) + "]";

        /// <summary>A research node by its id: <c>research[frame_saw]</c>.</summary>
        public static string Research(ResearchDefinition node) => "research[" + node.Id.Value + "]";

        /// <summary>A prerequisite by its id (the list is sorted, so a position would not match the file).</summary>
        public static string Prerequisite(ResearchDefinition node, ResearchId prerequisite) =>
            Research(node) + ".prerequisites[" + prerequisite.Value + "]";

        /// <summary>An effect by its position in the node's list, which is the file's order (the shape phase skipped nothing).</summary>
        public static string Effect(ResearchDefinition node, int index) =>
            Research(node) + ".effects[#" + index.ToString(CultureInfo.InvariantCulture) + "]";
    }
}
