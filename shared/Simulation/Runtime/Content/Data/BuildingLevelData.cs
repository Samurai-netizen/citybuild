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

        /// <summary>Required from level 2 (may be empty), not allowed on level 1. What upgrading to this level costs, keyed by item id.</summary>
        public Dictionary<string, long?>? UpgradeCost { get; set; }

        /// <summary>Required from level 2, not allowed on level 1. How long upgrading to this level takes, in seconds.</summary>
        public long? UpgradeSeconds { get; set; }
    }
}
