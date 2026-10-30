#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The answer of <c>GET /api/v1/city</c> and of every successful command (<c>05</c> Snapshot): the city as the server holds it. The client shows it and never changes it.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class CitySnapshotDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public CitySnapshotDto(Guid? requestId, long stateVersion, string serverTimeUtc, string contentVersion, SnapshotCityDto city, SnapshotSettlementDto settlement)
        {
            RequestId = requestId;
            StateVersion = stateVersion;
            ServerTimeUtc = serverTimeUtc;
            ContentVersion = contentVersion;
            City = city;
            Settlement = settlement;
        }

        /// <summary>The command's id, or <c>null</c> on <c>GET /city</c>, which is no command.</summary>
        [JsonProperty(Required = Required.AllowNull)]
        public Guid? RequestId { get; }

        /// <summary>The stored version after the request; it rises only when the request changed the city. A lower value than the one shown is an old answer and is ignored (<c>09</c>).</summary>
        public long StateVersion { get; }

        /// <summary>The server time of the request in whole seconds, UTC, with a literal <c>Z</c>, kept as text.</summary>
        public string ServerTimeUtc { get; }

        /// <summary>The content version the numbers come from; a different one than the client's means refetch <c>/content</c>.</summary>
        public string ContentVersion { get; }

        /// <summary>The city.</summary>
        public SnapshotCityDto City { get; }

        /// <summary>What this request's settlement did.</summary>
        public SnapshotSettlementDto Settlement { get; }
    }
}
