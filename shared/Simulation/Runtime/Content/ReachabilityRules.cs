#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Rule 16 of <c>core-instructions/04-simulation-model.md</c> §14 (M0-70): plays the content from the start, era by era, and reports what a
    /// city can never reach in its era. It tracks what could exist, not amounts or time: which goods can be had (the currency from a starting
    /// purse above 0; a good once a recipe that makes it can run), which building types can be built and to which level (unlocked, era not
    /// later, cost payable), and which research nodes can be done (the science building can be built, prerequisites done, cost payable, of
    /// this era: a city starts only its own era's research). A recipe runs once its building is built to that level, a house can be built
    /// (producers need workers) and its inputs can be had. Each era repeats until nothing new is reached; then each building type of the era
    /// that can't be built, or its first level that can't be reached, gives <see cref="ContentErrorCode.BuildingUnreachable"/>, and each
    /// research node of the era not done gives <see cref="ContentErrorCode.ResearchUnreachable"/>, in content order.
    /// </summary>
    internal static class ReachabilityRules
    {
        /// <summary>Plays every era in order and reports what it can't reach (see the type).</summary>
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            var reach = new Reach(draft);
            foreach (int era in draft.ErasByNumber.Keys.OrderBy(number => number))
            {
                while (reach.Step(era))
                {
                }

                reach.Report(era, errors);
            }
        }

        /// <summary>What a city could have reached so far; built new for each check.</summary>
        private sealed class Reach
        {
            /// <summary>The content being played.</summary>
            private readonly ContentDraft _draft;

            /// <summary>The goods (and the currency) a city can have.</summary>
            private readonly HashSet<ItemId> _items = new HashSet<ItemId>();

            /// <summary>The building types a city may place: available at the start or unlocked by a done node.</summary>
            private readonly HashSet<BuildingTypeId> _unlocked = new HashSet<BuildingTypeId>();

            /// <summary>The highest level each building type can reach; a type that can't be built is missing.</summary>
            private readonly Dictionary<BuildingTypeId, int> _levels = new Dictionary<BuildingTypeId, int>();

            /// <summary>The research nodes that can be done.</summary>
            private readonly HashSet<ResearchId> _done = new HashSet<ResearchId>();

            /// <summary>Starts from the starting city: its purse and the building types available at the start.</summary>
            public Reach(ContentDraft draft)
            {
                _draft = draft;
                if (draft.StartingCity.Purse > 0)
                {
                    _items.Add(draft.Items.Single(item => item.IsCurrency).Id);
                }

                _unlocked.UnionWith(draft.Buildings.Where(building => building.IsAvailableAtStart).Select(building => building.Id));
            }

            /// <summary>One pass over everything the era allows; returns whether anything new was reached, so the caller repeats until not.</summary>
            public bool Step(int era)
            {
                bool changed = false;
                foreach (BuildingDefinition building in _draft.Buildings.Where(building => building.Era <= era && _unlocked.Contains(building.Id)))
                {
                    changed |= Build(building);
                }

                bool hasWorkers = IsBuilt(BuildingKind.House);
                foreach (BuildingDefinition building in _draft.Buildings.Where(building => hasWorkers && _levels.ContainsKey(building.Id)))
                {
                    for (int number = 1; number <= _levels[building.Id]; number++)
                    {
                        RecipeDefinition? recipe = building.GetLevel(number).Recipe;
                        if (recipe is not null && CanPay(recipe.Inputs))
                        {
                            foreach (ItemQuantity output in recipe.Outputs)
                            {
                                changed |= _items.Add(output.Item);
                            }
                        }
                    }
                }

                bool hasScience = IsBuilt(BuildingKind.Science);
                foreach (ResearchDefinition node in _draft.Research.Where(node => hasScience && node.Era == era && !_done.Contains(node.Id)))
                {
                    if (node.Prerequisites.All(_done.Contains) && CanPay(node.Cost))
                    {
                        _done.Add(node.Id);
                        _unlocked.UnionWith(node.Effects.OfType<UnlockBuildingEffect>().Select(unlock => unlock.Building));
                        changed = true;
                    }
                }

                return changed;
            }

            /// <summary>Reports the era's building types and levels that can't be reached, then its research nodes that can't be done.</summary>
            public void Report(int era, ContentErrorList errors)
            {
                string inEra = " in era " + era.ToString(CultureInfo.InvariantCulture);
                foreach (BuildingDefinition building in _draft.Buildings.Where(building => building.Era == era))
                {
                    if (!_levels.TryGetValue(building.Id, out int level))
                    {
                        string reason = _unlocked.Contains(building.Id)
                            ? "its cost needs " + Missing(building.Cost) + ", which no reachable recipe makes"
                            : "no research node that can be done unlocks it";
                        errors.Add(
                            ContentErrorCode.BuildingUnreachable,
                            ContentPaths.Building(building),
                            building.Id.Value + " can never be built" + inEra + ": " + reason + ".");
                    }
                    else if (level < building.MaxLevel)
                    {
                        BuildingLevelDefinition next = building.GetLevel(level + 1);
                        errors.Add(
                            ContentErrorCode.BuildingUnreachable,
                            ContentPaths.Level(building, next),
                            building.Id.Value + " can never be upgraded to level " + next.Level.ToString(CultureInfo.InvariantCulture) + inEra
                            + ": the upgrade needs " + Missing(next.UpgradeCost) + ", which no reachable recipe makes.");
                    }
                }

                foreach (ResearchDefinition node in _draft.Research.Where(node => node.Era == era && !_done.Contains(node.Id)))
                {
                    errors.Add(
                        ContentErrorCode.ResearchUnreachable,
                        ContentPaths.Research(node),
                        node.Id.Value + " can never be done" + inEra + ": " + Reason(node) + ".");
                }
            }

            /// <summary>Builds the type, then upgrades it while the next level's cost can be paid; returns whether its level rose.</summary>
            private bool Build(BuildingDefinition building)
            {
                bool built = _levels.TryGetValue(building.Id, out int level);
                int start = level;
                if (!built && CanPay(building.Cost))
                {
                    level = 1;
                }

                while (level >= 1 && level < building.MaxLevel && CanPay(building.GetLevel(level + 1).UpgradeCost))
                {
                    level++;
                }

                if (level > start)
                {
                    _levels[building.Id] = level;
                    return true;
                }

                return false;
            }

            /// <summary>Why a node can't be done: no science building, a prerequisite never done, or a cost good never had.</summary>
            private string Reason(ResearchDefinition node)
            {
                if (!IsBuilt(BuildingKind.Science))
                {
                    return "the science building can never be built";
                }

                List<ResearchId> missing = node.Prerequisites.Where(prerequisite => !_done.Contains(prerequisite)).ToList();
                return missing.Count > 0
                    ? "it requires " + string.Join(", ", missing.Select(id => id.Value)) + ", which can never be done"
                    : "its cost needs " + Missing(node.Cost) + ", which no reachable recipe makes";
            }

            /// <summary>Whether a building type of the kind can be built.</summary>
            private bool IsBuilt(BuildingKind kind) =>
                _draft.Buildings.Any(building => building.Kind == kind && _levels.ContainsKey(building.Id));

            /// <summary>Whether every item of a cost or a recipe's inputs can be had; an amount of 0 asks for nothing.</summary>
            private bool CanPay(IReadOnlyList<ItemQuantity> amounts) => amounts.All(amount => amount.Quantity == 0 || _items.Contains(amount.Item));

            /// <summary>The items of a list that can't be had (amounts above 0), by id, for a message.</summary>
            private string Missing(IReadOnlyList<ItemQuantity> amounts) => string.Join(
                ", ", amounts.Where(amount => amount.Quantity > 0 && !_items.Contains(amount.Item)).Select(amount => amount.Item.Value));
        }
    }
}
