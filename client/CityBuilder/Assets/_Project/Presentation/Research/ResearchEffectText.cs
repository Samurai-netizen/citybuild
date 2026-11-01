#nullable enable

using System.Globalization;
using System.Text;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Client.Presentation.Research
{
    /// <summary>
    /// Words what a research does, from the research's effects and the content's names: <c>Unlocks Trading Post</c>, <c>Woodcutter's Hut: −20% labor</c>,
    /// <c>+250 Logs storage</c>. The research screen's nodes (M0-52) and the welcome-back report (M0-53) share it, so a research reads the same
    /// on both. No game number is written here: the numbers come from the effects. Pure code.
    /// </summary>
    public static class ResearchEffectText
    {
        /// <summary>Permille in a percent: a unit of measure, not a game number.</summary>
        private const int PermillePerPercent = 10;

        /// <summary>The words of every effect of <paramref name="node"/>, joined by a comma; empty for a research with no effect that has words.</summary>
        /// <param name="content">The game content the names come from.</param>
        /// <param name="node">A research of the content.</param>
        public static string Of(GameContent content, ResearchDefinition node)
        {
            var text = new StringBuilder();
            for (int index = 0; index < node.Effects.Count; index++)
            {
                string line = Line(content, node.Effects[index]);
                if (line.Length > 0)
                {
                    text.Append(text.Length == 0 ? string.Empty : ", ").Append(line);
                }
            }

            return text.ToString();
        }

        /// <summary>One effect in words: <c>Unlocks Trading Post</c>, <c>Woodcutter's Hut: −20% labor</c>, <c>+250 Logs storage</c>.</summary>
        private static string Line(GameContent content, ResearchEffect effect)
        {
            switch (effect)
            {
                case UnlockBuildingEffect unlock:
                    return "Unlocks " + BuildingName(content, unlock.Building);
                case LaborReductionEffect labor:
                    return BuildingName(content, labor.Building) + ": −" + Percent(labor.Permille) + " labor";
                case StorageCapBonusEffect storage:
                    return "+" + storage.Amount.ToString(CultureInfo.InvariantCulture) + " " + ItemText.ItemName(content, storage.Item) + " storage";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The building type's name from the content; the id when the content has none (it always has).</summary>
        private static string BuildingName(GameContent content, BuildingTypeId type)
        {
            return content.TryGetBuilding(type, out BuildingDefinition? definition) ? definition.Name : type.Value;
        }

        /// <summary>A permille as a percent: <c>20%</c> for 200, <c>12.5%</c> for 125 (one tenth of a percent at most, rounded toward zero).</summary>
        private static string Percent(int permille)
        {
            string text = (permille / PermillePerPercent).ToString(CultureInfo.InvariantCulture);
            int tenth = permille % PermillePerPercent;
            return (tenth == 0 ? text : text + "." + tenth.ToString(CultureInfo.InvariantCulture)) + "%";
        }
    }
}
