#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's checks across definitions: unique ids, eras numbered 1..n, references to eras and items that exist, base caps that
    /// match the capped items, and effects whose target exists and fits. A duplicate definition is reported once and then left out of the
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
            CheckBaseStorageCaps(draft, errors);
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
            ItemsExist(draft, draft.Rules.BaseStorageCaps, "rules.baseStorageCaps", errors);
            ItemsExist(draft, draft.StartingCity.Inventory, "startingCity.inventory", errors);
            foreach (BuildingDefinition building in draft.Buildings.Where(draft.IsFirst))
            {
                ItemsExist(draft, building.Cost, ContentPaths.Building(building) + ".cost", errors);
                foreach (BuildingLevelDefinition level in building.Levels)
                {
                    string levelPath = ContentPaths.Level(building, level);
                    ItemsExist(draft, level.UpgradeCost, levelPath + ".upgradeCost", errors);
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
        }

        /// <summary>A capped item has a base storage cap and an uncapped item has none (<see cref="ContentErrorCode.StorageCapMismatch"/>).</summary>
        private static void CheckBaseStorageCaps(ContentDraft draft, ContentErrorList errors)
        {
            // A base cap for an item that doesn't exist is an unknown reference (above); here every defined item is checked.
            foreach (ItemDefinition item in draft.Items.Where(draft.IsFirst))
            {
                bool hasCap = draft.Rules.TryGetBaseStorageCap(item.Id, out _);
                string path = "rules.baseStorageCaps." + item.Id.Value;
                if (item.IsCapped && !hasCap)
                {
                    errors.Add(ContentErrorCode.StorageCapMismatch, path, item.Id.Value + " is capped, so it needs a base storage cap.");
                }
                else if (!item.IsCapped && hasCap)
                {
                    errors.Add(ContentErrorCode.StorageCapMismatch, path, item.Id.Value + " is not capped, so it has no base storage cap.");
                }
            }
        }

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

        /// <summary>A labor reduction names an existing producer: a house has no labor (<see cref="ContentErrorCode.InvalidEffectTarget"/>).</summary>
        private static void CheckLaborReductionTarget(ContentDraft draft, LaborReductionEffect reduction, string path, ContentErrorList errors)
        {
            if (BuildingExists(draft, reduction.Building, path, errors) && draft.BuildingsById[reduction.Building].Kind != BuildingKind.Producer)
            {
                errors.Add(ContentErrorCode.InvalidEffectTarget, path, reduction.Building.Value + " is a house; only producers have labor to reduce.");
            }
        }

        /// <summary>A storage bonus names an existing capped item: on an uncapped one it would do nothing (<see cref="ContentErrorCode.InvalidEffectTarget"/>).</summary>
        private static void CheckStorageBonusTarget(ContentDraft draft, StorageCapBonusEffect bonus, string path, ContentErrorList errors)
        {
            if (ItemExists(draft, bonus.Item, path, errors) && !draft.ItemsById[bonus.Item].IsCapped)
            {
                errors.Add(ContentErrorCode.InvalidEffectTarget, path, bonus.Item.Value + " is not capped, so a storage bonus does nothing.");
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
