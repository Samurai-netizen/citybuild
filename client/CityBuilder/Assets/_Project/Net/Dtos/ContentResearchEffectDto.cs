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
        public ContentResearchEffectDto(ResearchEffectTypeDto type, string? building, string? item, int? permille, long? amount, string? mode)
        {
            Type = type;
            Building = building;
            Item = item;
            Permille = permille;
            Amount = amount;
            Mode = mode;
        }

        /// <summary>The effect's kind.</summary>
        public ResearchEffectTypeDto Type { get; }

        /// <summary>The building type id (unlock and labor reduction), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? Building { get; }

        /// <summary>The item id (storage cap bonus), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? Item { get; }

        /// <summary>The share in permille (labor reduction, walk speed bonus), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public int? Permille { get; }

        /// <summary>The storage cap bonus (storage cap bonus), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public long? Amount { get; }

        /// <summary>The amount mode it unlocks, <c>percent</c> or <c>exact</c> (unlock amount mode; CR-0008), else <c>null</c>.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string? Mode { get; }

        /// <summary>
        /// The letter of the area it opens (open land; M1-03), else <c>null</c>. Set only when the answer has the member, and written back
        /// only then, so the M0 answer, which has none, reads and writes back unchanged (decision 14 of M1-02).
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string? Area
        {
            get => _area;
            private set
            {
                _area = value;
                _hasArea = true;
            }
        }

        /// <summary>The area letter read from the answer.</summary>
        private string? _area;

        /// <summary>Whether the answer had the <c>area</c> member (null or not).</summary>
        private bool _hasArea;

        /// <summary>Newtonsoft writes <see cref="Area"/> only when the answer had it.</summary>
        /// <returns>Whether to write the member.</returns>
        public bool ShouldSerializeArea() => _hasArea;
    }
}
