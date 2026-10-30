#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One flat research effect (<c>05</c> Content): <see cref="Type"/> says which of the other members are set; the rest are <c>null</c>.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentResearchEffectDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentResearchEffectDto(ResearchEffectTypeDto type, string? building, string? item, int? permille, long? amount)
        {
            Type = type;
            Building = building;
            Item = item;
            Permille = permille;
            Amount = amount;
        }

        /// <summary>The effect's kind.</summary>
        public ResearchEffectTypeDto Type { get; }

        /// <summary>The building type id (unlock and labor reduction), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? Building { get; }

        /// <summary>The item id (storage cap bonus), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? Item { get; }

        /// <summary>The labor reduction in permille (labor reduction), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public int? Permille { get; }

        /// <summary>The storage cap bonus (storage cap bonus), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public long? Amount { get; }
    }
}
