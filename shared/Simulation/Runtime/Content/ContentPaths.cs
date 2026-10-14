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
        public static string Era(EraDefinition era) => "eras[" + era.Id.Value + "]";

        public static string Item(ItemDefinition item) => "items[" + item.Id.Value + "]";

        public static string Building(BuildingDefinition building) => "buildings[" + building.Id.Value + "]";

        public static string Level(BuildingDefinition building, BuildingLevelDefinition level) =>
            Building(building) + ".levels[" + level.Level.ToString(CultureInfo.InvariantCulture) + "]";

        public static string Research(ResearchDefinition node) => "research[" + node.Id.Value + "]";

        /// <summary>An effect by its position in the node's list, which is the file's order (the shape phase skipped nothing).</summary>
        public static string Effect(ResearchDefinition node, int index) =>
            Research(node) + ".effects[#" + index.ToString(CultureInfo.InvariantCulture) + "]";
    }
}
