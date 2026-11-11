#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Request body of <c>POST /api/v1/commands/place-roads</c>, <c>upgrade-roads</c> and <c>remove-roads</c>
    /// (<c>core-instructions/05-api-contract.md</c>, CR-0008). The client sends the tile list and its request id only.
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class RoadTilesRequestDto
    {
        /// <summary>Creates the request body.</summary>
        /// <param name="requestId">The idempotency key for this command.</param>
        /// <param name="tiles">The tiles to place, upgrade or remove, 1 to 64 distinct coordinates.</param>
        [JsonConstructor]
        public RoadTilesRequestDto(Guid requestId, IReadOnlyList<RoadTileRequestDto> tiles)
        {
            RequestId = requestId;
            Tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        }

        /// <summary>The idempotency key for this command.</summary>
        public Guid RequestId { get; }

        /// <summary>The tiles to place, upgrade or remove, in order.</summary>
        public IReadOnlyList<RoadTileRequestDto> Tiles { get; }
    }
}
