#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's era checks of item use: every item in a cost, a recipe or the starting inventory belongs to the era of what uses it
    /// (building type, research node, starting city) or an earlier one, so no city meets an item before its era (owner directive OD-10).
    /// Unknown items and eras are reported by the reference rules and left out here; research effects are not checked, because the engine's
    /// era checks (placement, research start) already keep their targets out of reach until their era.
    /// </summary>
    internal static class EraRules
    {
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            CheckItems(draft, draft.StartingCity.Era, draft.StartingCity.Inventory, "startingCity.inventory", "the starting city", errors);
            foreach (BuildingDefinition building in draft.Buildings.Where(draft.IsFirst))
            {
                string buildingPath = ContentPaths.Building(building);
                string user = building.Id.Value;
                CheckItems(draft, building.Era, building.Cost, buildingPath + ".cost", user, errors);
                foreach (BuildingLevelDefinition level in building.Levels)
                {
                    string levelPath = ContentPaths.Level(building, level);
                    CheckItems(draft, building.Era, level.UpgradeCost, levelPath + ".upgradeCost", user, errors);
                    if (level.Recipe is not null)
                    {
                        CheckItems(draft, building.Era, level.Recipe.Inputs, levelPath + ".recipe.inputs", user, errors);
                        CheckItems(draft, building.Era, level.Recipe.Outputs, levelPath + ".recipe.outputs", user, errors);
                    }
                }
            }

            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                CheckItems(draft, node.Era, node.Cost, ContentPaths.Research(node) + ".cost", node.Id.Value, errors);
            }
        }

        private static void CheckItems(
            ContentDraft draft, int userEra, IReadOnlyList<ItemQuantity> amounts, string path, string user, ContentErrorList errors)
        {
            if (!draft.ErasByNumber.ContainsKey(userEra))
            {
                return;
            }

            foreach (ItemQuantity amount in amounts)
            {
                if (draft.ItemsById.TryGetValue(amount.Item, out ItemDefinition? item) && draft.ErasByNumber.ContainsKey(item.Era) && item.Era > userEra)
                {
                    errors.Add(
                        ContentErrorCode.ItemInLaterEra,
                        path + "." + amount.Item.Value,
                        $"{user} (era {Format(userEra)}) uses {amount.Item.Value} of the later era {Format(item.Era)}; a city of era {Format(userEra)} can't have it yet.");
                }
            }
        }

        private static string Format(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
