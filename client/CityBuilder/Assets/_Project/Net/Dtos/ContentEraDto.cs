#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One era of <c>GET /content</c>.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentEraDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentEraDto(int number, string id, string name)
        {
            Number = number;
            Id = id;
            Name = name;
        }

        /// <summary>The era's number.</summary>
        public int Number { get; }

        /// <summary>Its content id.</summary>
        public string Id { get; }

        /// <summary>Its display name.</summary>
        public string Name { get; }
    }
}
