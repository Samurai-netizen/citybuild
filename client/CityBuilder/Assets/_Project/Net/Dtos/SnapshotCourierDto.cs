#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One courier of a courier station (<c>05</c> Snapshot, <c>04</c> section 19; CR-0008): the worker of a slot, walking its route.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotCourierDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotCourierDto(
            int stationId, int slot, CourierStateDto state, int toStop, long? departedAt, long? arrivesAt, long stamina, List<SnapshotCourierLoadDto> load)
        {
            StationId = stationId;
            Slot = slot;
            State = state;
            ToStop = toStop;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
            Stamina = stamina;
            Load = load;
        }

        /// <summary>The courier station's building id.</summary>
        public int StationId { get; }

        /// <summary>The slot in its station, from 1.</summary>
        public int Slot { get; }

        /// <summary>What it does.</summary>
        public CourierStateDto State { get; }

        /// <summary>The stop (1-based) it walks to while walking, else 0.</summary>
        public int ToStop { get; }

        /// <summary>The city-clock second its walk started; null when it doesn't walk.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public long? DepartedAt { get; }

        /// <summary>The city-clock second its walk ends; null when it doesn't walk.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public long? ArrivesAt { get; }

        /// <summary>Its stamina in seconds (part H spends it).</summary>
        public long Stamina { get; }

        /// <summary>What it carries, by item id, then origin.</summary>
        public List<SnapshotCourierLoadDto> Load { get; }
    }
}
