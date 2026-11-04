#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The route of one courier station slot (<c>05</c> Snapshot, <c>04</c> section 19; CR-0008), in the form <c>set-route</c> sends it.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotRouteDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotRouteDto(int stationId, int slot, List<SnapshotRouteStopDto> stops)
        {
            StationId = stationId;
            Slot = slot;
            Stops = stops;
        }

        /// <summary>The courier station's building id.</summary>
        public int StationId { get; }

        /// <summary>The slot, from 1.</summary>
        public int Slot { get; }

        /// <summary>The stops in walking order.</summary>
        public List<SnapshotRouteStopDto> Stops { get; }
    }
}
