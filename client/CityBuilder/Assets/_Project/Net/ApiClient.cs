#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The client's one way to talk to the server (<c>05</c>): one method per endpoint, each returning an <see cref="ApiResult{T}"/>.
    /// It sends the player's intent and reads the answer; it keeps no city, makes no request id and decides nothing about the game
    /// (<c>CommandSender</c> and <c>ClientCityStore</c> do, in later tasks). Views never call it directly (<c>.claude/rules/unity.md</c>).
    /// </summary>
    public sealed class ApiClient
    {
        /// <summary>The path prefix of version 1 of the API (<c>05</c> Versioning).</summary>
        private const string ApiPrefix = "/api/v1";

        /// <summary>The longest timeout accepted; a request that waits longer is a lost one, whatever the network.</summary>
        private const int MaxTimeoutSeconds = 300;

        /// <summary>The server's address without a trailing slash.</summary>
        private readonly string _baseUrl;

        /// <summary>Seconds one request may take.</summary>
        private readonly int _timeoutSeconds;

        /// <summary>The wire.</summary>
        private readonly IApiTransport _transport;

        /// <summary>Writes request bodies and reads answers.</summary>
        private readonly ApiJson _json = new ApiJson();

        /// <summary>Turns answers into results.</summary>
        private readonly ApiResponseParser _parser;

        /// <summary>The bearer token of the logged-in player; kept in memory only.</summary>
        private string? _accessToken;

        /// <summary>Creates a client.</summary>
        /// <param name="baseUrl">The server's address without a path, for example <c>http://localhost:8080</c> (<see cref="ApiClientSettings"/>).</param>
        /// <param name="timeoutSeconds">Seconds one request may take, from 1 to 300.</param>
        /// <param name="transport">The wire; <see cref="UnityWebRequestTransport"/> in the game.</param>
        /// <exception cref="ArgumentException"><paramref name="baseUrl"/> is not an absolute http or https address, or the timeout is out of range: a wrong asset, found at start.</exception>
        public ApiClient(string baseUrl, int timeoutSeconds, IApiTransport transport)
        {
            bool isUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri);
            if (!isUrl || uri == null || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("The base URL must be an absolute http or https address.", nameof(baseUrl));
            }

            if (timeoutSeconds < 1 || timeoutSeconds > MaxTimeoutSeconds)
            {
                throw new ArgumentException("The timeout must be from 1 to " + MaxTimeoutSeconds + " seconds.", nameof(timeoutSeconds));
            }

            _baseUrl = baseUrl.TrimEnd('/');
            _timeoutSeconds = timeoutSeconds;
            _transport = transport;
            _parser = new ApiResponseParser(_json);
        }

        /// <summary>Sets the token every later request except the login sends, or clears it with null.</summary>
        public void SetAccessToken(string? accessToken)
        {
            _accessToken = accessToken;
        }

        /// <summary><c>POST /auth/dev-login</c>: logs in as a development player (Development servers only) and creates it on first use.</summary>
        public Task<ApiResult<DevLoginResponseDto>> DevLoginAsync(string displayName, CancellationToken cancellationToken)
        {
            return SendAsync<DevLoginResponseDto>("POST", "/auth/dev-login", new DevLoginRequestDto(displayName), false, cancellationToken);
        }

        /// <summary><c>GET /content</c>: the game content, the same for every player.</summary>
        public Task<ApiResult<ContentResponseDto>> GetContentAsync(CancellationToken cancellationToken)
        {
            return SendAsync<ContentResponseDto>("GET", "/content", null, true, cancellationToken);
        }

        /// <summary><c>GET /city</c>: the player's city settled to now. Safe to repeat; it is not free of effects on the server (<c>05</c>).</summary>
        public Task<ApiResult<CitySnapshotDto>> GetCityAsync(CancellationToken cancellationToken)
        {
            return SendAsync<CitySnapshotDto>("GET", "/city", null, true, cancellationToken);
        }

        /// <summary><c>POST /commands/place-building</c>.</summary>
        public Task<ApiResult<CitySnapshotDto>> PlaceBuildingAsync(PlaceBuildingRequestDto request, CancellationToken cancellationToken)
        {
            return SendAsync<CitySnapshotDto>("POST", "/commands/place-building", request, true, cancellationToken);
        }

        /// <summary><c>POST /commands/upgrade-building</c>.</summary>
        public Task<ApiResult<CitySnapshotDto>> UpgradeBuildingAsync(UpgradeBuildingRequestDto request, CancellationToken cancellationToken)
        {
            return SendAsync<CitySnapshotDto>("POST", "/commands/upgrade-building", request, true, cancellationToken);
        }

        /// <summary><c>POST /commands/assign-workers</c>.</summary>
        public Task<ApiResult<CitySnapshotDto>> AssignWorkersAsync(AssignWorkersRequestDto request, CancellationToken cancellationToken)
        {
            return SendAsync<CitySnapshotDto>("POST", "/commands/assign-workers", request, true, cancellationToken);
        }

        /// <summary><c>POST /commands/start-research</c>.</summary>
        public Task<ApiResult<CitySnapshotDto>> StartResearchAsync(StartResearchRequestDto request, CancellationToken cancellationToken)
        {
            return SendAsync<CitySnapshotDto>("POST", "/commands/start-research", request, true, cancellationToken);
        }

        /// <summary>
        /// Sends one request and reads the answer. The timeout is the client's own, so it works the same on every transport: when it
        /// fires the transport's token is cancelled and the call returns <c>CLIENT_TIMEOUT</c>; when the caller's token is cancelled
        /// the call throws instead.
        /// </summary>
        private async Task<ApiResult<T>> SendAsync<T>(string method, string path, object? body, bool sendToken, CancellationToken cancellationToken)
            where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? jsonBody = body == null ? null : _json.Serialize(body);
            ApiTransportRequest request = new ApiTransportRequest(method, _baseUrl + ApiPrefix + path, jsonBody, sendToken ? _accessToken : null);

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
                try
                {
                    ApiTransportResponse response = await _transport.SendAsync(request, timeout.Token);
                    return _parser.Parse<T>(response);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    string detail = "No answer within " + _timeoutSeconds + " seconds.";
                    return ApiResult<T>.Failure(new ApiError(ApiErrorCodes.ClientTimeout, 0, detail, null, null, null));
                }
            }
        }
    }
}
