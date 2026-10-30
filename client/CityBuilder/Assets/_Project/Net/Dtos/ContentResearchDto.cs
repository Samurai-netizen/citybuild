#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One research node of <c>GET /content</c> (<c>05</c> Content).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentResearchDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentResearchDto(string id, string name, int era, Dictionary<string, long> cost, long durationSeconds, List<string> prerequisites, List<ContentResearchEffectDto> effects)
        {
            Id = id;
            Name = name;
            Era = era;
            Cost = cost;
            DurationSeconds = durationSeconds;
            Prerequisites = prerequisites;
            Effects = effects;
        }

        /// <summary>The content id.</summary>
        public string Id { get; }

        /// <summary>The display name.</summary>
        public string Name { get; }

        /// <summary>The era the node belongs to.</summary>
        public int Era { get; }

        /// <summary>Item id to the quantity starting it costs.</summary>
        public Dictionary<string, long> Cost { get; }

        /// <summary>Seconds the research runs.</summary>
        public long DurationSeconds { get; }

        /// <summary>Ids of the research that must be completed first, sorted by id.</summary>
        public List<string> Prerequisites { get; }

        /// <summary>What it does once completed.</summary>
        public List<ContentResearchEffectDto> Effects { get; }
    }
}
