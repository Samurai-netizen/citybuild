#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One stop of a route (<c>05</c> Snapshot; CR-0008): a building and what the courier does there, in order.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotRouteStopDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotRouteStopDto(int buildingId, List<SnapshotRouteActionDto> actions)
        {
            BuildingId = buildingId;
            Actions = actions;
        }

        /// <summary>The building.</summary>
        public int BuildingId { get; }

        /// <summary>The actions in order.</summary>
        public List<SnapshotRouteActionDto> Actions { get; }
    }
}
