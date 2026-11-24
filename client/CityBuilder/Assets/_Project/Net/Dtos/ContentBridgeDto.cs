#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The bridge (<c>05</c> Content → <c>bridge</c>, M1-03): a road kind built on water with the place-roads command once research allows it.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentBridgeDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentBridgeDto(string name, long tileMillis, Dictionary<string, long> cost)
        {
            Name = name;
            TileMillis = tileMillis;
            Cost = cost;
        }

        /// <summary>The display name.</summary>
        public string Name { get; }

        /// <summary>How many milliseconds a walker takes to cross one bridge tile.</summary>
        public long TileMillis { get; }

        /// <summary>What one bridge tile costs, by item id.</summary>
        public Dictionary<string, long> Cost { get; }
    }
}
