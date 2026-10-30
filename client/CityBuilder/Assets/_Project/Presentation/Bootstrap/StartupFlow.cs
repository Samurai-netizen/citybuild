#nullable enable

using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;

namespace CityBuilder.Client.Presentation.Bootstrap
{
    /// <summary>
    /// What the client does when it starts (M0-38): log in, load the content, fetch the city. A plain class over the Net services so a test
    /// runs it against a fake wire; <see cref="GameRoot"/> runs it and shows the outcome. It calls the services, never the views.
    /// <para>
    /// A kept token is tried first (development builds). If the server refuses it, because it expired or the server was reset, the token is
    /// dropped and the flow logs in once more; a refusal after a fresh login is a real failure and is reported as one.
    /// </para>
    /// </summary>
    public sealed class StartupFlow
    {
        /// <summary>Logs in and holds the token.</summary>
        private readonly AuthService _auth;

        /// <summary>Loads and validates the content.</summary>
        private readonly ContentService _content;

        /// <summary>Fetches the city.</summary>
        private readonly ApiClient _api;

        /// <summary>The dev player to be.</summary>
        private readonly string _displayName;

        /// <summary>Creates the flow.</summary>
        /// <param name="auth">The auth service over <paramref name="api"/>.</param>
        /// <param name="content">The content service over <paramref name="api"/>.</param>
        /// <param name="api">The client; the city is fetched with it.</param>
        /// <param name="displayName">The dev player's name (<c>ApiClientSettings.DevDisplayName</c>).</param>
        public StartupFlow(AuthService auth, ContentService content, ApiClient api, string displayName)
        {
            _auth = auth;
            _content = content;
            _api = api;
            _displayName = displayName;
        }

        /// <summary>Runs login, content and city in that order and stops at the first step that fails.</summary>
        /// <param name="cancellationToken">Cancels the requests; the call then throws (the app is closing).</param>
        /// <returns>How it ended; never throws for a server or network problem.</returns>
        /// <exception cref="System.OperationCanceledException">The token was cancelled.</exception>
        public async Task<StartupOutcome> RunAsync(CancellationToken cancellationToken)
        {
            bool restored = _auth.TryRestore(_displayName);
            if (!restored)
            {
                StartupOutcome? loginFailure = await LoginAsync(cancellationToken);
                if (loginFailure != null)
                {
                    return loginFailure;
                }
            }

            ContentLoadResult content = await _content.LoadAsync(cancellationToken);
            if (restored && IsTokenRefused(content.ApiError))
            {
                // The kept token is no good (expired, or the server was reset): one fresh login, then the same request again.
                _auth.Forget();
                StartupOutcome? loginFailure = await LoginAsync(cancellationToken);
                if (loginFailure != null)
                {
                    return loginFailure;
                }

                content = await _content.LoadAsync(cancellationToken);
            }

            if (content.IsInvalidContent)
            {
                return StartupOutcome.InvalidContent(content.ContentErrors);
            }

            if (!content.IsSuccess)
            {
                return StartupOutcome.Failed(content.ApiError!);
            }

            ApiResult<CitySnapshotDto> city = await _api.GetCityAsync(cancellationToken);
            if (!city.IsSuccess)
            {
                return StartupOutcome.Failed(city.Error!);
            }

            return StartupOutcome.Connected(content.Content!, city.Value!);
        }

        /// <summary>Logs in; returns the failed outcome, or null when logged in.</summary>
        private async Task<StartupOutcome?> LoginAsync(CancellationToken cancellationToken)
        {
            AuthState state = await _auth.LoginAsync(_displayName, cancellationToken);
            return state == AuthState.LoggedIn ? null : StartupOutcome.Failed(_auth.LastError!);
        }

        /// <summary>Whether the server refused the token (401, <c>AUTHENTICATION_REQUIRED</c>).</summary>
        private static bool IsTokenRefused(ApiError? error)
        {
            return error != null && error.Code == ApiErrorCodes.AuthenticationRequired;
        }
    }
}
