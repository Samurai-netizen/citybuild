#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>Answer of the dev login (<c>05</c> Authentication): the access token every later request sends as <c>Authorization: Bearer</c>.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class DevLoginResponseDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public DevLoginResponseDto(string accessToken, DateTimeOffset expiresAtUtc)
        {
            AccessToken = accessToken;
            ExpiresAtUtc = expiresAtUtc;
        }

        /// <summary>The signed token (a JWT). It is a secret: it is never logged or written to disk outside a development build.</summary>
        public string AccessToken { get; }

        /// <summary>When the token stops working (UTC); the client logs in again after that.</summary>
        public DateTimeOffset ExpiresAtUtc { get; }
    }
}
