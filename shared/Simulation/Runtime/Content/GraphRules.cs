#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The graph phase (M0-70, <c>core-instructions/04-simulation-model.md</c> §14 rules 14 to 16), which runs only when every other rule passed,
    /// so its findings are never echoes of an error already reported. First the static checks: every used good is made by some recipe (the
    /// currency may come from the starting purse instead), every good is used (rule 14), no level's recipe is worse than the one below and
    /// upgrade costs never fall (rule 15). If those pass too, <see cref="ReachabilityRules"/> plays the content from the start (rule 16).
    /// </summary>
    internal static class GraphRules
    {
        /// <summary>Runs the static graph checks and, when they find nothing, the reachability check.</summary>
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            int before = errors.Count;
            CheckItems(draft, errors);
            // Exactly one currency exists: the currency rule passed.
            ItemId currency = draft.Items.Single(item => item.IsCurrency).Id;
            foreach (BuildingDefinition building in draft.Buildings)
            {
                CheckUpgrades(building, currency, errors);
            }

            if (errors.Count == before)
            {
                ReachabilityRules.Check(draft, errors);
            }
        }

        /// <summary>
        /// Rule 14: reports each used good that no recipe makes (<see cref="ContentErrorCode.ItemNeverProduced"/>; the currency only when the
        /// starting purse is 0 too) and each good that nothing uses (<see cref="ContentErrorCode.ItemNeverUsed"/>), at the item, in content order.
        /// A use is a recipe input or an amount in a cost (building, upgrade, research, road).
        /// </summary>
        private static void CheckItems(ContentDraft draft, ContentErrorList errors)
        {
            var uses = new Dictionary<ItemId, List<string>>();
            var produced = new HashSet<ItemId>();
            foreach (BuildingDefinition building in draft.Buildings)
            {
                AddUses(uses, building.Cost, ContentPaths.Building(building) + ".cost");
                foreach (BuildingLevelDefinition level in building.Levels)
                {
                    string path = ContentPaths.Level(building, level);
                    AddUses(uses, level.UpgradeCost, path + ".upgradeCost");
                    if (level.Recipe is not null)
                    {
                        AddUses(uses, level.Recipe.Inputs, path + ".recipe.inputs");
                        produced.UnionWith(level.Recipe.Outputs.Select(output => output.Item));
                    }
                }
            }

            foreach (ResearchDefinition node in draft.Research)
            {
                AddUses(uses, node.Cost, ContentPaths.Research(node) + ".cost");
            }

            foreach (RoadLevelDefinition road in draft.RoadLevels)
            {
                AddUses(uses, road.Cost, ContentPaths.RoadLevel(road) + ".cost");
            }

            foreach (ItemDefinition item in draft.Items)
            {
                ReportItem(draft, item, uses, produced, errors);
            }
        }

        /// <summary>Reports one item under rule 14, if it is used but never made, or a good that is never used.</summary>
        private static void ReportItem(
            ContentDraft draft, ItemDefinition item, Dictionary<ItemId, List<string>> uses, HashSet<ItemId> produced, ContentErrorList errors)
        {
            bool isUsed = uses.TryGetValue(item.Id, out List<string>? paths);
            bool isGranted = item.IsCurrency && draft.StartingCity.Purse > 0;
            if (isUsed && !produced.Contains(item.Id) && !isGranted)
            {
                string source = item.IsCurrency ? "no recipe makes it and the starting purse is 0" : "no recipe makes it";
                errors.Add(
                    ContentErrorCode.ItemNeverProduced,
                    ContentPaths.Item(item),
                    item.Id.Value + " is used (" + string.Join(", ", paths!) + "), but " + source + ", so it can never be had.");
            }
            else if (!isUsed && !item.IsCurrency)
            {
                errors.Add(
                    ContentErrorCode.ItemNeverUsed,
                    ContentPaths.Item(item),
                    item.Id.Value + " is no recipe input and in no cost; use it somewhere or remove it.");
            }
        }

        /// <summary>Records each item of an amount list with an amount above 0 as used at the list's path.</summary>
        private static void AddUses(Dictionary<ItemId, List<string>> uses, IReadOnlyList<ItemQuantity> amounts, string path)
        {
            // A zero amount (allowed in costs, rule 1) asks for nothing, so it is no use.
            foreach (ItemQuantity amount in amounts.Where(amount => amount.Quantity > 0))
            {
                if (!uses.TryGetValue(amount.Item, out List<string>? paths))
                {
                    paths = new List<string>();
                    uses.Add(amount.Item, paths);
                }

                paths.Add(path);
            }
        }

        /// <summary>
        /// Rule 15 for one building type, level by level from 2: from level 3, the upgrade's currency and goods in all are at least the upgrade
        /// below's (<see cref="ContentErrorCode.UpgradeCostDecrease"/>; the build cost is not an upgrade, so level 2 is free to differ from it),
        /// and the recipe is not worse than the level below
        /// (<see cref="ContentErrorCode.RecipeWorse"/>). The levels are 1..max here, because the level rule passed.
        /// </summary>
        private static void CheckUpgrades(BuildingDefinition building, ItemId currency, ContentErrorList errors)
        {
            for (int number = 2; number <= building.MaxLevel; number++)
            {
                BuildingLevelDefinition below = building.GetLevel(number - 1);
                BuildingLevelDefinition level = building.GetLevel(number);
                string path = ContentPaths.Level(building, level);
                if (number >= 3
                    && (Sum(level.UpgradeCost, currency, true) < Sum(below.UpgradeCost, currency, true)
                        || Sum(level.UpgradeCost, currency, false) < Sum(below.UpgradeCost, currency, false)))
                {
                    errors.Add(
                        ContentErrorCode.UpgradeCostDecrease,
                        path + ".upgradeCost",
                        Invariant($"The upgrade to level {number} of {building.Id.Value} costs less of the currency or fewer goods in all than the upgrade to level {number - 1}; upgrade costs never fall."));
                }

                if (below.Recipe is not null && level.Recipe is not null && IsWorse(below, level))
                {
                    errors.Add(
                        ContentErrorCode.RecipeWorse,
                        path + ".recipe",
                        Invariant($"Level {number} of {building.Id.Value} makes a good more slowly with all max workers, or needs more of an input for each output, than level {number - 1}; a level is never worse than the one below."));
                }
            }
        }

        /// <summary>The currency part (<paramref name="ofCurrency"/> true) or the goods part (false) of a cost, summed.</summary>
        private static long Sum(IReadOnlyList<ItemQuantity> cost, ItemId currency, bool ofCurrency)
        {
            long sum = 0;
            foreach (ItemQuantity amount in cost)
            {
                if ((amount.Item == currency) == ofCurrency)
                {
                    sum = checked(sum + amount.Quantity);
                }
            }

            return sum;
        }

        /// <summary>
        /// Whether <paramref name="level"/>'s recipe is worse than <paramref name="below"/>'s: with all max workers it makes less of one of
        /// below's outputs per second (a missing output makes none), or it needs more of an input for each of below's outputs. Cross-multiplied
        /// in <see cref="BigInteger"/>, because content numbers up to 10^12 overflow a <c>long</c> when three are multiplied.
        /// </summary>
        private static bool IsWorse(BuildingLevelDefinition below, BuildingLevelDefinition level)
        {
            RecipeDefinition old = below.Recipe!;
            RecipeDefinition next = level.Recipe!;
            foreach (ItemQuantity output in old.Outputs)
            {
                long made = Quantity(next.Outputs, output.Item);
                if (new BigInteger(made) * level.MaxWorkers * old.Labor < new BigInteger(output.Quantity) * below.MaxWorkers * next.Labor)
                {
                    return true;
                }

                foreach (ItemQuantity input in next.Inputs)
                {
                    if (new BigInteger(input.Quantity) * output.Quantity > new BigInteger(Quantity(old.Inputs, input.Item)) * made)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The quantity of an item in an amount list, 0 when it isn't there.</summary>
        private static long Quantity(IReadOnlyList<ItemQuantity> amounts, ItemId item)
        {
            foreach (ItemQuantity amount in amounts)
            {
                if (amount.Item == item)
                {
                    return amount.Quantity;
                }
            }

            return 0;
        }

        /// <summary>Formats a message with the invariant culture.</summary>
        private static string Invariant(System.FormattableString text) => System.FormattableString.Invariant(text);
    }
}
