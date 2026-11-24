#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One building type of <c>GET /content</c> (<c>05</c> Content).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentBuildingDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentBuildingDto(string id, string name, BuildingKindDto kind, int era, bool availableAtStart, string? unlockedBy, int? maxPerCity, Dictionary<string, long> cost, long buildSeconds, List<ContentBuildingLevelDto> levels, int? baseMaxLevel = null)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Era = era;
            AvailableAtStart = availableAtStart;
            UnlockedBy = unlockedBy;
            MaxPerCity = maxPerCity;
            Cost = cost;
            BuildSeconds = buildSeconds;
            BaseMaxLevel = baseMaxLevel;
            Levels = levels;
        }

        /// <summary>The content id.</summary>
        public string Id { get; }

        /// <summary>The display name.</summary>
        public string Name { get; }

        /// <summary>House, producer, warehouse, courier station or science building.</summary>
        public BuildingKindDto Kind { get; }

        /// <summary>The era the type belongs to.</summary>
        public int Era { get; }

        /// <summary>Whether a new city can place it without research.</summary>
        public bool AvailableAtStart { get; }

        /// <summary>The research id that unlocks it, or <c>null</c> when none is needed.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? UnlockedBy { get; }

        /// <summary>The most buildings of the type one city may have, or <c>null</c> for no limit (CR-0008: the science building).</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public int? MaxPerCity { get; }

        /// <summary>Item id to the quantity placing it costs; the coins are paid at once, the goods delivered to the site (CR-0008).</summary>
        public Dictionary<string, long> Cost { get; }

        /// <summary>Seconds construction takes.</summary>
        public long BuildSeconds { get; }

        /// <summary>
        /// The highest level without research (M1-02); <c>maxLevelBonus</c> effects raise it. Absent in answers recorded before it (the last
        /// level then), and left out when absent, so those answers read and write back unchanged (decision 14).
        /// </summary>
        [JsonProperty(Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public int? BaseMaxLevel { get; }

        /// <summary>The levels, by level.</summary>
        public List<ContentBuildingLevelDto> Levels { get; }
    }
}
