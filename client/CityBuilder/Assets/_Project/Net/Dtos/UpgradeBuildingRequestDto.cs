#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>Body of <c>POST /api/v1/commands/upgrade-building</c> (<c>05</c> Commands).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class UpgradeBuildingRequestDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public UpgradeBuildingRequestDto(Guid requestId, int buildingId)
        {
            RequestId = requestId;
            BuildingId = buildingId;
        }

        /// <summary>The command's id, reused on every retry.</summary>
        public Guid RequestId { get; }

        /// <summary>The building's id in the player's own city (ids count from 1 in each city).</summary>
        public int BuildingId { get; }
    }
}
