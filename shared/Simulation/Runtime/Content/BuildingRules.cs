#nullable enable
using System;
using System.Globalization;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The rule phase's checks of building types (<c>core-instructions/04-simulation-model.md</c>, section 14 rules 3 and 9): levels 1..max;
    /// what each kind's levels must have and must not have; no level with fewer workers, a smaller carry limit or less stamina than the one
    /// below it; a producer's store holding its recipe's goods; stores that never shrink. An upgrade keeps the assigned workers, so a smaller
    /// max would break <c>assigned ≤ MaxWorkers</c>, and a smaller house would break <c>Σ assigned ≤ provided</c> (invariant 9); a store's
    /// goods stay in it through an upgrade, so a smaller store would leave goods above its limit.
    /// </summary>
    internal static class BuildingRules
    {
        /// <summary>
        /// Checks every building type, duplicates included: its level numbers, each level against its kind, that workers, carry limits and
        /// stamina never drop, and that its stores never shrink.
        /// </summary>
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            foreach (BuildingDefinition building in draft.Buildings)
            {
                CheckLevelNumbers(building, errors);
                foreach (BuildingLevelDefinition level in building.Levels)
                {
                    CheckLevel(draft, building, level, errors);
                }

                CheckWorkersNeverDrop(building, errors);
                CheckCourierLevelsNeverDrop(building, errors);
                CheckStoresNeverShrink(building, errors);
            }
        }

        /// <summary>
        /// Reports a building type whose levels aren't numbered exactly 1, 2, … up to the max level, each number once, or that has no level: one
        /// error for the type, listing its numbers (<see cref="ContentErrorCode.LevelsNotContiguous"/>).
        /// </summary>
        private static void CheckLevelNumbers(BuildingDefinition building, ContentErrorList errors)
        {
            // Levels are sorted by number, so 1..max means exactly: the level at position i has the number i + 1.
            bool contiguous = building.Levels.Count > 0 && building.Levels.Select((level, index) => level.Level == index + 1).All(isNext => isNext);
            if (!contiguous)
            {
                string numbers = string.Join(", ", building.Levels.Select(level => level.Level.ToString(CultureInfo.InvariantCulture)));
                errors.Add(
                    ContentErrorCode.LevelsNotContiguous,
                    ContentPaths.Building(building) + ".levels",
                    "The levels must be numbered 1, 2, … up to the max level, each once, but are: " + (numbers.Length == 0 ? "none" : numbers) + ".");
            }
        }

        /// <summary>Checks one level against its kind (rule 3); a producer's store against its recipe and every store against the currency (rules 4, 9).</summary>
        private static void CheckLevel(ContentDraft draft, BuildingDefinition building, BuildingLevelDefinition level, ContentErrorList errors)
        {
            string path = ContentPaths.Level(building, level);
            switch (building.Kind)
            {
                case BuildingKind.House:
                    Require(level.WorkersProvided >= 1, ContentErrorCode.InvalidHouseLevel, path + ".workersProvided", "A house level provides at least 1 worker.", errors);
                    Forbid(level.MaxWorkers != 0, ContentErrorCode.InvalidHouseLevel, path + ".maxWorkers", "A house takes no workers; leave maxWorkers out.", errors);
                    Forbid(level.Recipe is not null, ContentErrorCode.InvalidHouseLevel, path + ".recipe", "A house has no recipe.", errors);
                    ForbidStoreAndCouriers(level, ContentErrorCode.InvalidHouseLevel, path, "A house", errors);
                    break;
                case BuildingKind.Producer:
                    CheckProducerLevel(draft, level, path, errors);
                    break;
                case BuildingKind.Warehouse:
                    Require(level.Storage.Count > 0, ContentErrorCode.InvalidWarehouseLevel, path + ".storage", "A warehouse level has a store: list its goods and limits.", errors);
                    ForbidWorkersAndRecipe(level, ContentErrorCode.InvalidWarehouseLevel, path, "A warehouse", errors);
                    ForbidCouriers(level, ContentErrorCode.InvalidWarehouseLevel, path, "A warehouse", errors);
                    break;
                case BuildingKind.CourierStation:
                    Require(level.MaxWorkers >= 1, ContentErrorCode.InvalidCourierStationLevel, path + ".maxWorkers", "A courier station level has at least 1 courier slot.", errors);
                    Require(level.CarryLimit >= 1, ContentErrorCode.InvalidCourierStationLevel, path + ".carryLimit", "A courier station level has a carry limit of at least 1.", errors);
                    Require(level.StaminaSeconds >= 1, ContentErrorCode.InvalidCourierStationLevel, path + ".staminaSeconds", "A courier station level has a stamina of at least 1 s.", errors);
                    Forbid(level.WorkersProvided != 0, ContentErrorCode.InvalidCourierStationLevel, path + ".workersProvided", "Only houses provide workers; leave workersProvided out.", errors);
                    Forbid(level.Recipe is not null, ContentErrorCode.InvalidCourierStationLevel, path + ".recipe", "A courier station has no recipe.", errors);
                    Forbid(level.Storage.Count > 0, ContentErrorCode.InvalidCourierStationLevel, path + ".storage", "A courier station has no store; its couriers carry the goods.", errors);
                    break;
                default:
                    ForbidWorkersAndRecipe(level, ContentErrorCode.InvalidScienceLevel, path, "The science building", errors);
                    ForbidStoreAndCouriers(level, ContentErrorCode.InvalidScienceLevel, path, "The science building", errors);
                    break;
            }

            foreach (ItemQuantity good in level.Storage)
            {
                if (draft.ItemsById.TryGetValue(good.Item, out ItemDefinition? item) && item.IsCurrency)
                {
                    errors.Add(
                        ContentErrorCode.StoreMismatch,
                        path + ".storage." + good.Item.Value,
                        good.Item.Value + " is the currency; it lives in the purse, never in a store.");
                }
            }
        }

        /// <summary>
        /// A producer level (rules 3 and 9): at least 1 worker slot, no workers provided, a recipe with an output, no carry limit or stamina,
        /// and a store that holds every good of the recipe (inputs and outputs, not the currency) with a limit of at least one cycle's quantity.
        /// </summary>
        private static void CheckProducerLevel(ContentDraft draft, BuildingLevelDefinition level, string path, ContentErrorList errors)
        {
            Require(level.MaxWorkers >= 1, ContentErrorCode.InvalidProducerLevel, path + ".maxWorkers", "A producer level has at least 1 worker slot.", errors);
            Forbid(level.WorkersProvided != 0, ContentErrorCode.InvalidProducerLevel, path + ".workersProvided", "Only houses provide workers; leave workersProvided out.", errors);
            ForbidCouriers(level, ContentErrorCode.InvalidProducerLevel, path, "A producer", errors);
            if (level.Recipe is null)
            {
                errors.Add(ContentErrorCode.InvalidProducerLevel, path + ".recipe", "A producer level has a recipe.");
                return;
            }

            if (level.Recipe.Outputs.Count == 0)
            {
                errors.Add(ContentErrorCode.InvalidProducerLevel, path + ".recipe.outputs", "A recipe makes at least one item.");
            }

            Require(level.Storage.Count > 0, ContentErrorCode.InvalidProducerLevel, path + ".storage", "A producer level has a store for its recipe's goods.", errors);
            foreach (ItemQuantity good in level.Recipe.Inputs.Concat(level.Recipe.Outputs))
            {
                bool isCurrency = draft.ItemsById.TryGetValue(good.Item, out ItemDefinition? item) && item.IsCurrency;
                if (isCurrency || level.Storage.Count == 0)
                {
                    continue;
                }

                if (!level.TryGetStorageLimit(good.Item, out long limit))
                {
                    errors.Add(ContentErrorCode.StoreMismatch, path + ".storage", good.Item.Value + " is a good of the recipe, so the store holds it.");
                }
                else if (limit < good.Quantity)
                {
                    errors.Add(
                        ContentErrorCode.StoreMismatch,
                        path + ".storage." + good.Item.Value,
                        FormattableString.Invariant($"The limit {limit} is below the recipe's {good.Quantity}, so a cycle could never start or never end."));
                }
            }
        }

        /// <summary>Reports a level of a kind without workers that has worker slots, workers provided or a recipe.</summary>
        private static void ForbidWorkersAndRecipe(BuildingLevelDefinition level, ContentErrorCode code, string path, string kind, ContentErrorList errors)
        {
            Forbid(level.MaxWorkers != 0, code, path + ".maxWorkers", kind + " takes no workers; leave maxWorkers out.", errors);
            Forbid(level.WorkersProvided != 0, code, path + ".workersProvided", "Only houses provide workers; leave workersProvided out.", errors);
            Forbid(level.Recipe is not null, code, path + ".recipe", kind + " has no recipe.", errors);
        }

        /// <summary>Reports a level of a kind without a store or couriers that has a store, a carry limit or stamina.</summary>
        private static void ForbidStoreAndCouriers(BuildingLevelDefinition level, ContentErrorCode code, string path, string kind, ContentErrorList errors)
        {
            Forbid(level.Storage.Count > 0, code, path + ".storage", kind + " has no store; leave storage out.", errors);
            ForbidCouriers(level, code, path, kind, errors);
        }

        /// <summary>Reports a level that isn't a courier station's and has a carry limit or stamina.</summary>
        private static void ForbidCouriers(BuildingLevelDefinition level, ContentErrorCode code, string path, string kind, ContentErrorList errors)
        {
            Forbid(level.CarryLimit != 0, code, path + ".carryLimit", kind + " has no couriers; leave carryLimit out.", errors);
            Forbid(level.StaminaSeconds != 0, code, path + ".staminaSeconds", kind + " has no couriers; leave staminaSeconds out.", errors);
        }

        /// <summary>Reports <paramref name="code"/> at <paramref name="path"/> when a required value is missing (<paramref name="holds"/> is false).</summary>
        private static void Require(bool holds, ContentErrorCode code, string path, string message, ContentErrorList errors)
        {
            if (!holds)
            {
                errors.Add(code, path, message);
            }
        }

        /// <summary>Reports <paramref name="code"/> at <paramref name="path"/> when a value is given where the kind has none (<paramref name="isGiven"/>).</summary>
        private static void Forbid(bool isGiven, ContentErrorCode code, string path, string message, ContentErrorList errors)
        {
            if (isGiven)
            {
                errors.Add(code, path, message);
            }
        }

        /// <summary>
        /// Reports a level with fewer workers than the level below: max workers for a producer or a courier station, workers provided for a house
        /// (<see cref="ContentErrorCode.WorkersDecrease"/>). An upgrade keeps the assigned workers, so a drop would break invariant 9.
        /// </summary>
        private static void CheckWorkersNeverDrop(BuildingDefinition building, ContentErrorList errors)
        {
            // Only the number that matters for the kind is compared; the other one is 0 by the checks above, or already reported. A count
            // below 1 is already reported as an invalid level, so it is not compared again.
            Func<BuildingLevelDefinition, int> workers = building.Kind == BuildingKind.House
                ? level => level.WorkersProvided
                : level => level.MaxWorkers;
            if (building.Kind != BuildingKind.House && building.Kind != BuildingKind.Producer && building.Kind != BuildingKind.CourierStation)
            {
                return;
            }

            string field = building.Kind == BuildingKind.House ? ".workersProvided" : ".maxWorkers";
            for (int index = 1; index < building.Levels.Count; index++)
            {
                BuildingLevelDefinition below = building.Levels[index - 1];
                BuildingLevelDefinition level = building.Levels[index];
                if (workers(below) >= 1 && workers(level) >= 1 && workers(level) < workers(below))
                {
                    errors.Add(
                        ContentErrorCode.WorkersDecrease,
                        ContentPaths.Level(building, level) + field,
                        FormattableString.Invariant(
                            $"Level {level.Level} has {workers(level)}, fewer than the {workers(below)} of level {below.Level}; an upgrade keeps the assigned workers."));
                }
            }
        }

        /// <summary>A courier station level never carries less or tires sooner than the one below it (rule 3): an upgrade keeps its couriers.</summary>
        private static void CheckCourierLevelsNeverDrop(BuildingDefinition building, ContentErrorList errors)
        {
            if (building.Kind != BuildingKind.CourierStation)
            {
                return;
            }

            for (int index = 1; index < building.Levels.Count; index++)
            {
                BuildingLevelDefinition below = building.Levels[index - 1];
                BuildingLevelDefinition level = building.Levels[index];
                string path = ContentPaths.Level(building, level);
                if (below.CarryLimit >= 1 && level.CarryLimit >= 1 && level.CarryLimit < below.CarryLimit)
                {
                    errors.Add(
                        ContentErrorCode.CourierLevelDecrease,
                        path + ".carryLimit",
                        FormattableString.Invariant($"Level {level.Level} carries {level.CarryLimit}, less than the {below.CarryLimit} of level {below.Level}."));
                }

                if (below.StaminaSeconds >= 1 && level.StaminaSeconds >= 1 && level.StaminaSeconds < below.StaminaSeconds)
                {
                    errors.Add(
                        ContentErrorCode.CourierLevelDecrease,
                        path + ".staminaSeconds",
                        FormattableString.Invariant($"Level {level.Level} has {level.StaminaSeconds} s of stamina, less than the {below.StaminaSeconds} s of level {below.Level}."));
                }
            }
        }

        /// <summary>
        /// A level's store holds every good of the store below it with at least its limit (rule 9): an upgrade keeps the store's goods, so a
        /// smaller store would hold goods above its limit, or goods it no longer holds.
        /// </summary>
        private static void CheckStoresNeverShrink(BuildingDefinition building, ContentErrorList errors)
        {
            for (int index = 1; index < building.Levels.Count; index++)
            {
                BuildingLevelDefinition below = building.Levels[index - 1];
                BuildingLevelDefinition level = building.Levels[index];
                foreach (ItemQuantity good in below.Storage)
                {
                    string path = ContentPaths.Level(building, level) + ".storage." + good.Item.Value;
                    if (!level.TryGetStorageLimit(good.Item, out long limit))
                    {
                        errors.Add(
                            ContentErrorCode.StoreShrinks,
                            path,
                            FormattableString.Invariant($"Level {below.Level}'s store holds {good.Item.Value}, so level {level.Level}'s holds it too."));
                    }
                    else if (limit < good.Quantity)
                    {
                        errors.Add(
                            ContentErrorCode.StoreShrinks,
                            path,
                            FormattableString.Invariant($"Level {level.Level}'s limit {limit} is below level {below.Level}'s {good.Quantity}."));
                    }
                }
            }
        }
    }
}
