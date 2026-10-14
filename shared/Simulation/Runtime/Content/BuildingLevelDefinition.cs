#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>One level of a building type: its workers, its recipe, and what upgrading to it costs.</summary>
    public sealed class BuildingLevelDefinition
    {
        internal BuildingLevelDefinition(
            int level, int maxWorkers, int workersProvided, RecipeDefinition? recipe, IReadOnlyList<ItemQuantity> upgradeCost, long upgradeSeconds)
        {
            Level = level;
            MaxWorkers = maxWorkers;
            WorkersProvided = workersProvided;
            Recipe = recipe;
            UpgradeCost = upgradeCost ?? throw new ArgumentNullException(nameof(upgradeCost));
            UpgradeSeconds = upgradeSeconds;
        }

        /// <summary>The level number, from 1 (newly built) to the building type's max level.</summary>
        public int Level { get; }

        /// <summary>How many workers a producer may have at this level: at least 1, and never fewer than at the level below. 0 for houses.</summary>
        public int MaxWorkers { get; }

        /// <summary>How many workers a completed house provides at this level: at least 1, never fewer than below. 0 for producers.</summary>
        public int WorkersProvided { get; }

        /// <summary>What a producer makes at this level; null for houses.</summary>
        public RecipeDefinition? Recipe { get; }

        /// <summary>What upgrading from the level below to this one costs, sorted by item id. Empty for level 1, which is built, not upgraded to.</summary>
        public IReadOnlyList<ItemQuantity> UpgradeCost { get; }

        /// <summary>How long upgrading from the level below to this one takes, in seconds: greater than zero, and 0 for level 1.</summary>
        public long UpgradeSeconds { get; }
    }
}
