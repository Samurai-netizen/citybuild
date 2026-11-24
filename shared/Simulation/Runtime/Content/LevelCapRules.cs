#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The level cap rule of M1-02 (<c>04</c> section 14): a building type's base max level is at most its last level, and the
    /// <c>maxLevelBonus</c> effects of all research together never raise it past the last level (<see cref="ContentErrorCode.LevelCapMismatch"/>).
    /// A bonus on a type without a level above its base is a bad target (the reference rules), not reported again here; that every level above the
    /// base can be opened is the reachability rule's (rule 16). Runs after the reference rules, so targets exist.
    /// </summary>
    internal static class LevelCapRules
    {
        /// <summary>
        /// Whether the type's levels are numbered 1..n, each once: only then is its last level its <see cref="BuildingDefinition.MaxLevel"/>.
        /// Broken numbering is the building rules' error (<see cref="ContentErrorCode.LevelsNotContiguous"/>), so the cap rules skip it.
        /// </summary>
        /// <param name="building">The building type as read.</param>
        /// <returns>True when the numbering is whole.</returns>
        public static bool HasLevelsOneToN(BuildingDefinition building)
        {
            for (int index = 0; index < building.Levels.Count; index++)
            {
                if (building.Levels[index].Level != index + 1)
                {
                    return false;
                }
            }

            return building.Levels.Count > 0;
        }

        /// <summary>Reports each type whose base max level is above its last level, then each whose bonuses would go past it.</summary>
        /// <param name="draft">The content as read.</param>
        /// <param name="errors">Where the errors go.</param>
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            var bonuses = new Dictionary<BuildingTypeId, long>();
            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                foreach (MaxLevelBonusEffect bonus in node.Effects.OfType<MaxLevelBonusEffect>().Where(bonus => bonus.Amount > 0))
                {
                    bonuses.TryGetValue(bonus.Building, out long sum);
                    bonuses[bonus.Building] = checked(sum + bonus.Amount);
                }
            }

            foreach (BuildingDefinition building in draft.Buildings.Where(building => draft.IsFirst(building) && HasLevelsOneToN(building)))
            {
                string path = ContentPaths.Building(building);
                string last = building.MaxLevel.ToString(CultureInfo.InvariantCulture);
                if (building.BaseMaxLevel > building.MaxLevel)
                {
                    errors.Add(
                        ContentErrorCode.LevelCapMismatch,
                        path + ".baseMaxLevel",
                        building.Id.Value + "'s base max level " + building.BaseMaxLevel.ToString(CultureInfo.InvariantCulture)
                        + " is above its last level " + last + ".");
                }
                else if (building.BaseMaxLevel >= 1 && building.BaseMaxLevel < building.MaxLevel && bonuses.TryGetValue(building.Id, out long sum) && building.BaseMaxLevel + sum > building.MaxLevel)
                {
                    errors.Add(
                        ContentErrorCode.LevelCapMismatch,
                        path,
                        building.Id.Value + "'s max level bonuses (" + sum.ToString(CultureInfo.InvariantCulture) + ") would raise it past its last level " + last + ".");
                }
            }
        }
    }
}
