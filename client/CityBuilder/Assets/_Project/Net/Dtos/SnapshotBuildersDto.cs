#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The city's builder slots (<c>05</c> Snapshot): one is busy while a building is constructed or upgraded.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotBuildersDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotBuildersDto(int slots, int busy)
        {
            Slots = slots;
            Busy = busy;
        }

        /// <summary>How many buildings may be under construction or upgrade at once.</summary>
        public int Slots { get; }

        /// <summary>How many slots are in use now.</summary>
        public int Busy { get; }
    }
}
