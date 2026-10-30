#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The members of an RFC 9457 Problem Details answer the client uses (<c>05</c> Errors). Every member is optional here: a body
    /// that lacks <c>code</c> is not the server's and becomes <c>CLIENT_INVALID_RESPONSE</c> in <see cref="ApiResponseParser"/>.
    /// </summary>
    internal sealed class ProblemDetailsDto
    {
        /// <summary>The stable error code, for example <c>INSUFFICIENT_RESOURCE</c>.</summary>
        [JsonProperty("code")]
        public string? Code { get; set; }

        /// <summary>The human sentence; may change between versions.</summary>
        [JsonProperty("detail")]
        public string? Detail { get; set; }

        /// <summary>The short human title; used only when there is no detail.</summary>
        [JsonProperty("title")]
        public string? Title { get; set; }

        /// <summary>The W3C trace id of the HTTP request.</summary>
        [JsonProperty("traceId")]
        public string? TraceId { get; set; }

        /// <summary>The command's request id, present only when the request had one; kept as text and parsed by the caller.</summary>
        [JsonProperty("requestId")]
        public string? RequestId { get; set; }
    }
}
