#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's checks across definitions: unique ids, eras numbered 1..n, references to eras and items that exist, one currency used
    /// only where goods aren't needed, one science building, and effects whose target exists and fits. A duplicate definition is reported once and then left out of the
    /// cross-checks, which use the first definition of each id.
    /// </summary>
    internal static class ReferenceRules
    {
        /// <summary>Runs every cross-definition check, duplicates first, so the later checks can rely on the first definition of each id.</summary>
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            CheckDuplicates(draft, errors);
            CheckEraNumbers(draft, errors);
            CheckEraReferences(draft, errors);
            CheckItemReferences(draft, errors);
            CheckCurrency(draft, errors);
            CheckScienceBuilding(draft, errors);
            CheckEffectTargets(draft, errors);
            CheckPrerequisiteReferences(draft, errors);
        }

        /// <summary>
        /// Reports a repeated era id or era number, item id, building type id, research id, and a prerequisite listed twice
        /// (<see cref="ContentErrorCode.DuplicateId"/>). An era that repeats both id and number is one mistake, reported once.
        /// </summary>
        private static void CheckDuplicates(ContentDraft draft, ContentErrorList errors)
        {
            var eraIds = new HashSet<EraId>();
            var eraNumbers = new HashSet<int>();
            foreach (EraDefinition era in draft.Eras)
            {
                // Both sets learn every era. A copy that repeats the id and the number is one mistake, so the id is reported, not both.
                bool idIsTaken = !eraIds.Add(era.Id);
                bool numberIsTaken = !eraNumbers.Add(era.Number);
                if (idIsTaken)
                {
                    Duplicate(ContentPaths.Era(era), "era id " + era.Id.Value, errors);
                }
                else if (numberIsTaken)
                {
                    Duplicate(ContentPaths.Era(era) + ".number", "era number " + Format(era.Number), errors);
                }
            }

            foreach (ItemDefinition item in draft.Items.Where(item => !draft.IsFirst(item)))
            {
                Duplicate(ContentPaths.Item(item), "item id " + item.Id.Value, errors);
            }

            foreach (BuildingDefinition building in draft.Buildings.Where(building => !draft.IsFirst(building)))
            {
                Duplicate(ContentPaths.Building(building), "building type id " + building.Id.Value, errors);
            }

            foreach (ResearchDefinition node in draft.Research)
            {
                if (!draft.IsFirst(node))
                {
                    Duplicate(ContentPaths.Research(node), "research id " + node.Id.Value, errors);
                }

                var seen = new HashSet<ResearchId>();
                foreach (ResearchId prerequisite in node.Prerequisites.Where(prerequisite => !seen.Add(prerequisite)))
                {
                    Duplicate(ContentPaths.Prerequisite(node, prerequisite), "prerequisite " + prerequisite.Value, errors);
                }
            }

            var presetIds = new HashSet<AmountPresetId>();
            foreach (AmountPresetDefinition preset in draft.AmountPresets.Where(preset => !presetIds.Add(preset.Id)))
            {
                Duplicate(ContentPaths.AmountPreset(preset), "amount preset id " + preset.Id.Value, errors);
            }
        }

        /// <summary>The eras are numbered 1, 2, … without gaps, at least era 1 (<see cref="ContentErrorCode.ErasNotContiguous"/>).</summary>
        private static void CheckEraNumbers(ContentDraft draft, ContentErrorList errors)
        {
            // Duplicate numbers are reported above; here only the set of numbers counts.
            int[] numbers = draft.Eras.Select(era => era.Number).Distinct().OrderBy(number => number).ToArray();
            if (numbers.Length == 0 || numbers.Where((number, index) => number != index + 1).Any())
            {
                errors.Add(
                    ContentErrorCode.ErasNotContiguous,
                    "eras",
                    "The eras must be numbered 1, 2, … without gaps, at least era 1, but are: "
                    + (numbers.Length == 0 ? "none" : string.Join(", ", numbers.Select(Format))) + ".");
            }
        }

        /// <summary>Every era number used by the starting city, an item, a building type or a research node names an existing era.</summary>
        private static void CheckEraReferences(ContentDraft draft, ContentErrorList errors)
        {
            // Without any era every reference would be unknown; that one problem is already reported as ErasNotContiguous.
            if (draft.Eras.Count == 0)
            {
                return;
            }

            EraExists(draft, draft.StartingCity.Era, "startingCity.era", errors);
            foreach (ItemDefinition item in draft.Items.Where(draft.IsFirst))
            {
                EraExists(draft, item.Era, ContentPaths.Item(item) + ".era", errors);
            }

            foreach (BuildingDefinition building in draft.Buildings.Where(draft.IsFirst))
            {
                EraExists(draft, building.Era, ContentPaths.Building(building) + ".era", errors);
            }

            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                EraExists(draft, node.Era, ContentPaths.Research(node) + ".era", errors);
            }
        }

        /// <summary>Every item named in base caps, the starting stock, costs, upgrade costs, recipes and research costs exists.</summary>
        private static void CheckItemReferences(ContentDraft draft, ContentErrorList errors)
        {
            foreach (BuildingDefinition building in draft.Buildings.Where(draft.IsFirst))
            {
                ItemsExist(draft, building.Cost, ContentPaths.Building(building) + ".cost", errors);
                foreach (BuildingLevelDefinition level in building.Levels)
                {
                    string levelPath = ContentPaths.Level(building, level);
                    ItemsExist(draft, level.UpgradeCost, levelPath + ".upgradeCost", errors);
                    ItemsExist(draft, level.Storage, levelPath + ".storage", errors);
                    if (level.Recipe is not null)
                    {
                        ItemsExist(draft, level.Recipe.Inputs, levelPath + ".recipe.inputs", errors);
                        ItemsExist(draft, level.Recipe.Outputs, levelPath + ".recipe.outputs", errors);
                    }
                }
            }

            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                ItemsExist(draft, node.Cost, ContentPaths.Research(node) + ".cost", errors);
            }

            foreach (RoadLevelDefinition level in draft.RoadLevels)
            {
                ItemsExist(draft, level.Cost, ContentPaths.RoadLevel(level) + ".cost", errors);
            }
        }

        /// <summary>
        /// Rule 4: exactly one item is the currency, and it is never a recipe input (a cycle takes its inputs from its own store, which never
        /// holds the currency; the storage rules report a store that does). Rule 11: a road level costs the currency only (CR-0008 D6: roads
        /// build at once, so no good could be delivered to them).
        /// </summary>
        private static void CheckCurrency(ContentDraft draft, ContentErrorList errors)
        {
            int currencies = draft.Items.Where(draft.IsFirst).Count(item => item.IsCurrency);
            if (currencies != 1)
            {
                errors.Add(
                    ContentErrorCode.CurrencyCount,
                    "items",
                    FormattableString.Invariant($"Exactly one item is the currency (currency: true), but {currencies} are."));
            }

            foreach (BuildingDefinition building in draft.Buildings.Where(draft.IsFirst))
            {
                foreach (BuildingLevelDefinition level in building.Levels.Where(level => level.Recipe is not null))
                {
                    foreach (ItemQuantity input in level.Recipe!.Inputs.Where(input => IsCurrency(draft, input.Item)))
                    {
                        errors.Add(
                            ContentErrorCode.InvalidCurrencyUse,
                            ContentPaths.Level(building, level) + ".recipe.inputs." + input.Item.Value,
                            input.Item.Value + " is the currency; a cycle takes its inputs from its own store, which holds goods only.");
                    }
                }
            }

            foreach (RoadLevelDefinition level in draft.RoadLevels)
            {
                foreach (ItemQuantity good in level.Cost.Where(amount => draft.ItemsById.ContainsKey(amount.Item) && !IsCurrency(draft, amount.Item)))
                {
                    errors.Add(
                        ContentErrorCode.InvalidCurrencyUse,
                        ContentPaths.RoadLevel(level) + ".cost." + good.Item.Value,
                        good.Item.Value + " is a good; a road costs the currency only, as it is built at once.");
                }
            }
        }

        /// <summary>Rule 10: exactly one building type is the science building (kind <c>science</c>), with <c>maxPerCity</c> 1.</summary>
        private static void CheckScienceBuilding(ContentDraft draft, ContentErrorList errors)
        {
            BuildingDefinition[] science = draft.Buildings.Where(draft.IsFirst).Where(building => building.Kind == BuildingKind.Science).ToArray();
            if (science.Length != 1)
            {
                errors.Add(
                    ContentErrorCode.ScienceBuildingMismatch,
                    "buildings",
                    FormattableString.Invariant($"Exactly one building type is the science building (kind science), but {science.Length} are."));
            }

            foreach (BuildingDefinition building in science.Where(building => building.MaxPerCity != 1))
            {
                errors.Add(
                    ContentErrorCode.ScienceBuildingMismatch,
                    ContentPaths.Building(building) + ".maxPerCity",
                    "Research happens at the one science building of a city, so its maxPerCity is 1.");
            }
        }

        /// <summary>Whether the content defines the item and it is the currency.</summary>
        private static bool IsCurrency(ContentDraft draft, ItemId item) => draft.ItemsById.TryGetValue(item, out ItemDefinition? definition) && definition.IsCurrency;

        /// <summary>Every effect's target exists and fits its type: an unlock names a building type, a labor reduction a producer, a storage bonus a capped item.</summary>
        private static void CheckEffectTargets(ContentDraft draft, ContentErrorList errors)
        {
            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                for (int index = 0; index < node.Effects.Count; index++)
                {
                    string path = ContentPaths.Effect(node, index);
                    switch (node.Effects[index])
                    {
                        case UnlockBuildingEffect unlock:
                            BuildingExists(draft, unlock.Building, path + ".building", errors);
                            break;
                        case LaborReductionEffect reduction:
                            CheckLaborReductionTarget(draft, reduction, path + ".building", errors);
                            break;
                        case StorageCapBonusEffect bonus:
                            CheckStorageBonusTarget(draft, bonus, path + ".item", errors);
                            break;
                    }
                }
            }
        }

        /// <summary>A labor reduction names an existing producer: no other kind has labor (<see cref="ContentErrorCode.InvalidEffectTarget"/>).</summary>
        private static void CheckLaborReductionTarget(ContentDraft draft, LaborReductionEffect reduction, string path, ContentErrorList errors)
        {
            if (BuildingExists(draft, reduction.Building, path, errors) && draft.BuildingsById[reduction.Building].Kind != BuildingKind.Producer)
            {
                errors.Add(ContentErrorCode.InvalidEffectTarget, path, reduction.Building.Value + " is not a producer; only producers have labor to reduce.");
            }
        }

        /// <summary>A storage bonus names an existing good: on the currency, which no store holds, it would do nothing (<see cref="ContentErrorCode.InvalidEffectTarget"/>).</summary>
        private static void CheckStorageBonusTarget(ContentDraft draft, StorageCapBonusEffect bonus, string path, ContentErrorList errors)
        {
            if (ItemExists(draft, bonus.Item, path, errors) && draft.ItemsById[bonus.Item].IsCurrency)
            {
                errors.Add(ContentErrorCode.InvalidEffectTarget, path, bonus.Item.Value + " is the currency, which no store holds, so a storage bonus does nothing.");
            }
        }

        /// <summary>Every prerequisite names an existing research node.</summary>
        private static void CheckPrerequisiteReferences(ContentDraft draft, ContentErrorList errors)
        {
            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                foreach (ResearchId prerequisite in node.Prerequisites.Distinct().Where(id => !draft.ResearchById.ContainsKey(id)))
                {
                    Unknown(ContentPaths.Prerequisite(node, prerequisite), "research " + prerequisite.Value, errors);
                }
            }
        }

        /// <summary>Reports an era number that no era has.</summary>
        private static void EraExists(ContentDraft draft, int era, string path, ContentErrorList errors)
        {
            if (!draft.ErasByNumber.ContainsKey(era))
            {
                Unknown(path, "era " + Format(era), errors);
            }
        }

        /// <summary>Reports every item of a list that the content doesn't define.</summary>
        private static void ItemsExist(ContentDraft draft, IReadOnlyList<ItemQuantity> amounts, string path, ContentErrorList errors)
        {
            foreach (ItemQuantity amount in amounts)
            {
                ItemExists(draft, amount.Item, path + "." + amount.Item.Value, errors);
            }
        }

        /// <summary>Reports an item the content doesn't define; true when it exists, so the caller can go on checking it.</summary>
        private static bool ItemExists(ContentDraft draft, ItemId item, string path, ContentErrorList errors)
        {
            bool exists = draft.ItemsById.ContainsKey(item);
            if (!exists)
            {
                Unknown(path, "item " + item.Value, errors);
            }

            return exists;
        }

        /// <summary>Reports a building type the content doesn't define; true when it exists, so the caller can go on checking it.</summary>
        private static bool BuildingExists(ContentDraft draft, BuildingTypeId building, string path, ContentErrorList errors)
        {
            bool exists = draft.BuildingsById.ContainsKey(building);
            if (!exists)
            {
                Unknown(path, "building type " + building.Value, errors);
            }

            return exists;
        }

        /// <summary>Adds an <see cref="ContentErrorCode.UnknownReference"/> error: "The content defines no …".</summary>
        private static void Unknown(string path, string what, ContentErrorList errors) =>
            errors.Add(ContentErrorCode.UnknownReference, path, "The content defines no " + what + ".");

        /// <summary>Adds a <see cref="ContentErrorCode.DuplicateId"/> error: "The … is used more than once".</summary>
        private static void Duplicate(string path, string what, ContentErrorList errors) =>
            errors.Add(ContentErrorCode.DuplicateId, path, "The " + what + " is used more than once; ids are unique per kind.");

        /// <summary>An era number as text, the same in every culture.</summary>
        private static string Format(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
