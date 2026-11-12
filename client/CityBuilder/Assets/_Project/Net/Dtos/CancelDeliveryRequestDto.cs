#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Body of <c>POST /api/v1/commands/cancel-delivery</c> (<c>core-instructions/05-api-contract.md</c>, CR-0008 amendment 1): which courier,
    /// and one of its delivery lines or null for all of them and its route. Intent only: no goods, no quantity, no time.
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class CancelDeliveryRequestDto
    {
        /// <summary>Creates the request body.</summary>
        /// <param name="requestId">The idempotency key for this command.</param>
        /// <param name="stationId">The courier station's building id.</param>
        /// <param name="slot">The courier's slot.</param>
        /// <param name="lineId">The delivery line to cancel, or null for every line and the route.</param>
        [JsonConstructor]
        public CancelDeliveryRequestDto(Guid requestId, int stationId, int slot, int? lineId)
        {
            RequestId = requestId;
            StationId = stationId;
            Slot = slot;
            LineId = lineId;
        }

        /// <summary>The command's id, reused on every retry.</summary>
        public Guid RequestId { get; }

        /// <summary>The courier station's building id.</summary>
        public int StationId { get; }

        /// <summary>The courier's slot.</summary>
        public int Slot { get; }

        /// <summary>The delivery line to cancel; null (sent as JSON <c>null</c>) cancels all ("Cancel all").</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public int? LineId { get; }
    }
}
