#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Body of <c>POST /api/v1/commands/set-paused</c> (<c>core-instructions/05-api-contract.md</c>, CR-0008 amendment 2): which building and
    /// whether it is paused. Intent only: no time, worker or courier.
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SetPausedRequestDto
    {
        /// <summary>Creates the request body.</summary>
        /// <param name="requestId">The idempotency key for this command.</param>
        /// <param name="buildingId">The building's id.</param>
        /// <param name="paused">True to pause, false to resume.</param>
        [JsonConstructor]
        public SetPausedRequestDto(Guid requestId, int buildingId, bool paused)
        {
            RequestId = requestId;
            BuildingId = buildingId;
            Paused = paused;
        }

        /// <summary>The command's id, reused on every retry.</summary>
        public Guid RequestId { get; }

        /// <summary>The building's id.</summary>
        public int BuildingId { get; }

        /// <summary>True to pause, false to resume.</summary>
        public bool Paused { get; }
    }
}
