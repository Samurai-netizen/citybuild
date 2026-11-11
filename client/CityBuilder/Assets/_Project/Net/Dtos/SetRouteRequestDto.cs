#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Request body of <c>POST /api/v1/commands/set-route</c> (<c>core-instructions/05-api-contract.md</c>, CR-0008). An empty stop
    /// list clears the route; the server decides every rule about it.
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SetRouteRequestDto
    {
        /// <summary>Creates the request body.</summary>
        /// <param name="requestId">The idempotency key for this command.</param>
        /// <param name="stationId">The courier station.</param>
        /// <param name="slot">The courier slot of the station, from 1.</param>
        /// <param name="stops">The stops in order; empty clears the route.</param>
        [JsonConstructor]
        public SetRouteRequestDto(Guid requestId, int stationId, int slot, IReadOnlyList<RouteStopRequestDto> stops)
        {
            RequestId = requestId;
            StationId = stationId;
            Slot = slot;
            Stops = stops ?? throw new ArgumentNullException(nameof(stops));
        }

        /// <summary>The idempotency key for this command.</summary>
        public Guid RequestId { get; }

        /// <summary>The courier station.</summary>
        public int StationId { get; }

        /// <summary>The courier slot of the station.</summary>
        public int Slot { get; }

        /// <summary>The stops in order.</summary>
        public IReadOnlyList<RouteStopRequestDto> Stops { get; }
    }
}
