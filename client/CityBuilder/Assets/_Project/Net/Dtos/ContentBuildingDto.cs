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
        public ContentBuildingDto(string id, string name, BuildingKindDto kind, int era, bool availableAtStart, string? unlockedBy, Dictionary<string, long> cost, long buildSeconds, List<ContentBuildingLevelDto> levels)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Era = era;
            AvailableAtStart = availableAtStart;
            UnlockedBy = unlockedBy;
            Cost = cost;
            BuildSeconds = buildSeconds;
            Levels = levels;
        }

        /// <summary>The content id.</summary>
        public string Id { get; }

        /// <summary>The display name.</summary>
        public string Name { get; }

        /// <summary>House or producer.</summary>
        public BuildingKindDto Kind { get; }

        /// <summary>The era the type belongs to.</summary>
        public int Era { get; }

        /// <summary>Whether a new city can place it without research.</summary>
        public bool AvailableAtStart { get; }

        /// <summary>The research id that unlocks it, or <c>null</c> when none is needed.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? UnlockedBy { get; }

        /// <summary>Item id to the quantity placing it costs.</summary>
        public Dictionary<string, long> Cost { get; }

        /// <summary>Seconds construction takes.</summary>
        public long BuildSeconds { get; }

        /// <summary>The levels, by level.</summary>
        public List<ContentBuildingLevelDto> Levels { get; }
    }
}
