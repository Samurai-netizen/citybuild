#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Body of <c>POST /api/v1/commands/reenergize-couriers</c> (<c>core-instructions/05-api-contract.md</c>, CR-0008): intent only, so
    /// it carries the request id and nothing else (the server answers 400 to any other member).
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ReenergizeCouriersRequestDto
    {
        /// <summary>Creates the request body.</summary>
        /// <param name="requestId">The idempotency key for this command.</param>
        [JsonConstructor]
        public ReenergizeCouriersRequestDto(Guid requestId)
        {
            RequestId = requestId;
        }

        /// <summary>The command's id, reused on every retry.</summary>
        public Guid RequestId { get; }
    }
}
