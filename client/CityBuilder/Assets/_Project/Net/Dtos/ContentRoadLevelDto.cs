#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One road level of <c>GET /content</c> (<c>05</c> Content, <c>04</c> section 17).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentRoadLevelDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentRoadLevelDto(int level, string name, long tileMillis, Dictionary<string, long> cost)
        {
            Level = level;
            Name = name;
            TileMillis = tileMillis;
            Cost = cost;
        }

        /// <summary>The level number, 1 for a newly placed road.</summary>
        public int Level { get; }

        /// <summary>Its display name.</summary>
        public string Name { get; }

        /// <summary>Milliseconds a walker takes to cross one tile of it.</summary>
        public long TileMillis { get; }

        /// <summary>Item id to the quantity one tile costs: to place it at level 1, to upgrade it to this level above.</summary>
        public Dictionary<string, long> Cost { get; }
    }
}
