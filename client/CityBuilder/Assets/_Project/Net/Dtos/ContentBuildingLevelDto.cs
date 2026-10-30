#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One level of a building type (<c>05</c> Content).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentBuildingLevelDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentBuildingLevelDto(int level, int maxWorkers, int workersProvided, ContentRecipeDto? recipe, Dictionary<string, long>? upgradeCost, long? upgradeSeconds)
        {
            Level = level;
            MaxWorkers = maxWorkers;
            WorkersProvided = workersProvided;
            Recipe = recipe;
            UpgradeCost = upgradeCost;
            UpgradeSeconds = upgradeSeconds;
        }

        /// <summary>The level, from 1.</summary>
        public int Level { get; }

        /// <summary>Most workers this level takes; 0 for houses.</summary>
        public int MaxWorkers { get; }

        /// <summary>Workers this level provides; 0 for producers.</summary>
        public int WorkersProvided { get; }

        /// <summary>The recipe, or <c>null</c> for a house.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public ContentRecipeDto? Recipe { get; }

        /// <summary>Cost of upgrading to this level, or <c>null</c> for level 1.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public Dictionary<string, long>? UpgradeCost { get; }

        /// <summary>Seconds that upgrade takes, or <c>null</c> for level 1.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public long? UpgradeSeconds { get; }
    }
}
