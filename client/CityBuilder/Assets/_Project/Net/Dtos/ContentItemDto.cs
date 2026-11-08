#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One item of <c>GET /content</c>.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentItemDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentItemDto(string id, string name, int era, bool currency, bool tradable)
        {
            Id = id;
            Name = name;
            Era = era;
            Currency = currency;
            Tradable = tradable;
        }

        /// <summary>The content id.</summary>
        public string Id { get; }

        /// <summary>The display name.</summary>
        public string Name { get; }

        /// <summary>The era the item belongs to.</summary>
        public int Era { get; }

        /// <summary>Whether it is the currency: kept in the purse, never in a store or carried (CR-0008).</summary>
        public bool Currency { get; }

        /// <summary>Whether the open market takes it (from Era II).</summary>
        public bool Tradable { get; }
    }
}
