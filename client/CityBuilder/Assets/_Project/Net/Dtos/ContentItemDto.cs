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
        public ContentItemDto(string id, string name, int era, bool capped, bool tradable)
        {
            Id = id;
            Name = name;
            Era = era;
            Capped = capped;
            Tradable = tradable;
        }

        /// <summary>The content id.</summary>
        public string Id { get; }

        /// <summary>The display name.</summary>
        public string Name { get; }

        /// <summary>The era the item belongs to.</summary>
        public int Era { get; }

        /// <summary>Whether storage caps apply to it.</summary>
        public bool Capped { get; }

        /// <summary>Whether the open market takes it (from Era II).</summary>
        public bool Tradable { get; }
    }
}
