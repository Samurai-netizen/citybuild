#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>A courier whose stamina ran out during the settlement (<c>05</c> Snapshot, <c>settlement.tiredCouriers</c>; CR-0008).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotTiredCourierDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotTiredCourierDto(int stationId, int slot, long at)
        {
            StationId = stationId;
            Slot = slot;
            At = at;
        }

        /// <summary>The courier station's building id.</summary>
        public int StationId { get; }

        /// <summary>The courier's slot at its station, from 1.</summary>
        public int Slot { get; }

        /// <summary>The city second its stamina ran out.</summary>
        public long At { get; }
    }
}
