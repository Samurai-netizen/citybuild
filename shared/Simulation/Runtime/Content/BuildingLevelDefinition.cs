#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>One level of a building type: its workers, its recipe, its store, its couriers' carry limit and stamina, and what upgrading to it costs.</summary>
    public sealed class BuildingLevelDefinition
    {
        /// <summary>
        /// Creates the level from values the shape phase has read (<see cref="ContentReader"/>); the content rules check them afterwards.
        /// Each value is described on its property.
        /// </summary>
        internal BuildingLevelDefinition(
            int level,
            int maxWorkers,
            int workersProvided,
            RecipeDefinition? recipe,
            IReadOnlyList<ItemQuantity> storage,
            long carryLimit,
            long staminaSeconds,
            IReadOnlyList<ItemQuantity> upgradeCost,
            long upgradeSeconds)
        {
            Level = level;
            MaxWorkers = maxWorkers;
            WorkersProvided = workersProvided;
            Recipe = recipe;
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            CarryLimit = carryLimit;
            StaminaSeconds = staminaSeconds;
            UpgradeCost = upgradeCost ?? throw new ArgumentNullException(nameof(upgradeCost));
            UpgradeSeconds = upgradeSeconds;
        }

        /// <summary>The level number, from 1 (newly built) to the building type's max level.</summary>
        public int Level { get; }

        /// <summary>
        /// How many workers a producer or a courier station (its couriers) may have at this level: at least 1, and never fewer than at the level
        /// below. 0 for houses, warehouses and the science building.
        /// </summary>
        public int MaxWorkers { get; }

        /// <summary>How many workers a completed house provides at this level: at least 1, never fewer than below. 0 for every other kind.</summary>
        public int WorkersProvided { get; }

        /// <summary>What a producer makes at this level; null for every other kind.</summary>
        public RecipeDefinition? Recipe { get; }

        /// <summary>
        /// The goods the level's store holds, each with its limit (before research bonuses), sorted by item id (<c>04</c> section 16): a
        /// producer's recipe goods, a warehouse's goods; empty for houses, courier stations and the science building. Never the currency.
        /// </summary>
        public IReadOnlyList<ItemQuantity> Storage { get; }

        /// <summary>A courier station level's carry limit per courier, all goods together, greater than 0; 0 for every other kind.</summary>
        public long CarryLimit { get; }

        /// <summary>A courier station level's stamina per courier in seconds, greater than 0; 0 for every other kind.</summary>
        public long StaminaSeconds { get; }

        /// <summary>Whether the level's store holds a good, and its limit there (before research bonuses).</summary>
        /// <param name="item">The good.</param>
        /// <param name="limit">The level's limit for it; 0 when the store doesn't hold it.</param>
        /// <returns>True when the store holds the good.</returns>
        public bool TryGetStorageLimit(ItemId item, out long limit)
        {
            for (int index = 0; index < Storage.Count; index++)
            {
                if (Storage[index].Item == item)
                {
                    limit = Storage[index].Quantity;
                    return true;
                }
            }

            limit = 0;
            return false;
        }

        /// <summary>What upgrading from the level below to this one costs, sorted by item id. Empty for level 1, which is built, not upgraded to.</summary>
        public IReadOnlyList<ItemQuantity> UpgradeCost { get; }

        /// <summary>How long upgrading from the level below to this one takes, in seconds: greater than zero, and 0 for level 1.</summary>
        public long UpgradeSeconds { get; }
    }
}
