#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>One level of a building type as read, before any check (see <see cref="BuildingLevelDefinition"/>).</summary>
    public sealed class BuildingLevelData
    {
        /// <summary>Required. The level number: 1 for a newly built building.</summary>
        public int? Level { get; set; }

        /// <summary>Optional, 0 when left out. How many workers a producer may have at this level.</summary>
        public int? MaxWorkers { get; set; }

        /// <summary>Optional, 0 when left out. How many workers a completed house provides at this level.</summary>
        public int? WorkersProvided { get; set; }

        /// <summary>Optional: none when left out. What a producer makes at this level.</summary>
        public RecipeData? Recipe { get; set; }

        /// <summary>
        /// The goods the level's store holds, each with its limit, keyed by item id (<c>04</c> section 16): required for producers (their
        /// recipe's goods) and warehouses; absent for the other kinds.
        /// </summary>
        public Dictionary<string, long?>? Storage { get; set; }

        /// <summary>Courier stations only, required there: how much each courier carries, all goods together (<c>04</c> section 19).</summary>
        public long? CarryLimit { get; set; }

        /// <summary>Courier stations only, required there: each courier's stamina in seconds of walking while the player is away (<c>04</c> section 2).</summary>
        public long? StaminaSeconds { get; set; }

        /// <summary>Required from level 2 (may be empty), not allowed on level 1. What upgrading to this level costs, keyed by item id.</summary>
        public Dictionary<string, long?>? UpgradeCost { get; set; }

        /// <summary>Required from level 2, not allowed on level 1. How long upgrading to this level takes, in seconds.</summary>
        public long? UpgradeSeconds { get; set; }
    }
}
