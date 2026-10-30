#nullable enable

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// What came back from the wire: an HTTP answer of any status, or the news that none came. It carries text only; reading it is
    /// <see cref="ApiResponseParser"/>'s job.
    /// </summary>
    public sealed class ApiTransportResponse
    {
        /// <summary>Creates a response; use <see cref="Http"/> or <see cref="NetworkFailure"/>.</summary>
        private ApiTransportResponse(int statusCode, string body, string? retryAfterHeader, string? networkFailureMessage)
        {
            StatusCode = statusCode;
            Body = body;
            RetryAfterHeader = retryAfterHeader;
            NetworkFailureMessage = networkFailureMessage;
        }

        /// <summary>The HTTP status, or 0 when no HTTP answer arrived.</summary>
        public int StatusCode { get; }

        /// <summary>The body as text; empty when there was none.</summary>
        public string Body { get; }

        /// <summary>The raw <c>Retry-After</c> header, or null when the answer had none.</summary>
        public string? RetryAfterHeader { get; }

        /// <summary>Why no HTTP answer arrived (the engine's error text), or null when one did.</summary>
        public string? NetworkFailureMessage { get; }

        /// <summary>An HTTP answer, of any status.</summary>
        public static ApiTransportResponse Http(int statusCode, string body, string? retryAfterHeader)
        {
            return new ApiTransportResponse(statusCode, body, retryAfterHeader, null);
        }

        /// <summary>The request never got an HTTP answer (no connection, a reset, a DNS failure).</summary>
        public static ApiTransportResponse NetworkFailure(string message)
        {
            return new ApiTransportResponse(0, string.Empty, null, message);
        }
    }
}
