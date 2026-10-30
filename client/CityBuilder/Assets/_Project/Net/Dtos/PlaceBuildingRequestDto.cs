#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>Body of <c>POST /api/v1/commands/place-building</c> (<c>05</c> Commands). It holds the player's intent and nothing the server decides: no cost, level or player id.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class PlaceBuildingRequestDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public PlaceBuildingRequestDto(Guid requestId, string buildingTypeId, int x, int y)
        {
            RequestId = requestId;
            BuildingTypeId = buildingTypeId;
            X = x;
            Y = y;
        }

        /// <summary>The command's id, made once per player intent and reused on every retry (<c>05</c> Idempotency rules).</summary>
        public Guid RequestId { get; }

        /// <summary>The content id of the building type to place, for example <c>sawmill</c>.</summary>
        public string BuildingTypeId { get; }

        /// <summary>The tile's column, from 0.</summary>
        public int X { get; }

        /// <summary>The tile's row, from 0.</summary>
        public int Y { get; }
    }
}
