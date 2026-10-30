#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>Body of <c>POST /api/v1/commands/assign-workers</c> (<c>05</c> Commands): an absolute target, never a change.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class AssignWorkersRequestDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public AssignWorkersRequestDto(Guid requestId, int buildingId, int workers)
        {
            RequestId = requestId;
            BuildingId = buildingId;
            Workers = workers;
        }

        /// <summary>The command's id, reused on every retry.</summary>
        public Guid RequestId { get; }

        /// <summary>The building's id in the player's own city.</summary>
        public int BuildingId { get; }

        /// <summary>The absolute number of workers the building should have; 0 pauses it.</summary>
        public int Workers { get; }
    }
}
