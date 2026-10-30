#nullable enable

namespace CityBuilder.Client.Net
{
    /// <summary>One HTTP request, described without any engine type, so a test can stand in for the network (<see cref="IApiTransport"/>).</summary>
    public sealed class ApiTransportRequest
    {
        /// <summary>Creates the description.</summary>
        /// <param name="method">The HTTP method, <c>GET</c> or <c>POST</c>.</param>
        /// <param name="url">The absolute URL.</param>
        /// <param name="jsonBody">The JSON text to send as <c>application/json</c>, or null for no body.</param>
        /// <param name="accessToken">The bearer token to send, or null for none.</param>
        public ApiTransportRequest(string method, string url, string? jsonBody, string? accessToken)
        {
            Method = method;
            Url = url;
            JsonBody = jsonBody;
            AccessToken = accessToken;
        }

        /// <summary>The HTTP method.</summary>
        public string Method { get; }

        /// <summary>The absolute URL, query included.</summary>
        public string Url { get; }

        /// <summary>The JSON body, or null when the request has none.</summary>
        public string? JsonBody { get; }

        /// <summary>The token for the <c>Authorization: Bearer</c> header, or null when the request is sent without one.</summary>
        public string? AccessToken { get; }
    }
}
