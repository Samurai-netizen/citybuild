#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The city's workers (<c>05</c> Snapshot, <c>04</c> section 3): how many houses provide, how many buildings use, how many are free.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotWorkersDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotWorkersDto(long provided, long assigned, long onTrips, long free)
        {
            Provided = provided;
            Assigned = assigned;
            OnTrips = onTrips;
            Free = free;
        }

        /// <summary>Workers the city's buildings provide.</summary>
        public long Provided { get; }

        /// <summary>Workers assigned to buildings.</summary>
        public long Assigned { get; }

        /// <summary>Workers carrying goods on manual trips, one per trip (CR-0008).</summary>
        public long OnTrips { get; }

        /// <summary>Workers still free: provided minus assigned minus on trips.</summary>
        public long Free { get; }
    }
}
