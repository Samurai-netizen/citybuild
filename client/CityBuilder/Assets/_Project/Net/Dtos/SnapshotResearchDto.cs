#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The city's research (<c>05</c> Snapshot): the running node, if any, and the completed ones.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotResearchDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotResearchDto(string? activeId, long? startedAt, long? endsAt, List<string> completed)
        {
            ActiveId = activeId;
            StartedAt = startedAt;
            EndsAt = endsAt;
            Completed = completed;
        }

        /// <summary>The id of the research running now, or <c>null</c> when the one slot is free.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? ActiveId { get; }

        /// <summary>City-clock second the running research started, or <c>null</c> when none runs.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public long? StartedAt { get; }

        /// <summary>City-clock second the running research completes, or <c>null</c> when none runs.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public long? EndsAt { get; }

        /// <summary>Ids of the completed research, sorted by id (ordinal).</summary>
        public List<string> Completed { get; }
    }
}
