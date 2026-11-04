#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One amount preset of <c>GET /content</c> (<c>05</c> Content, CR-0008): a share a trip or a route may always use, by its id.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentAmountPresetDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentAmountPresetDto(string id, string name, int percent)
        {
            Id = id;
            Name = name;
            Percent = percent;
        }

        /// <summary>The preset's id, for example <c>half</c>; a request's amount names it.</summary>
        public string Id { get; }

        /// <summary>The display name.</summary>
        public string Name { get; }

        /// <summary>The share of the base, 1 to 100.</summary>
        public int Percent { get; }
    }
}
