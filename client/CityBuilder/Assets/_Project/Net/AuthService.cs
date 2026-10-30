#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The client's login: it gets a token from the development login, hands it to the <see cref="ApiClient"/>, and in development builds
    /// keeps it for the next run. The state machine is <see cref="AuthState"/>. It holds no snapshot and no balance, and it does not decide
    /// whether a token is still good: the server does, and a caller that sees <c>AUTHENTICATION_REQUIRED</c> calls <see cref="Forget"/> and
    /// logs in again (<c>StartupFlow</c> in the Presentation assembly does).
    /// </summary>
    public sealed class AuthService
    {
        /// <summary>The client that sends the requests and carries the token.</summary>
        private readonly ApiClient _api;

        /// <summary>Where a development build keeps the token between runs.</summary>
        private readonly IAccessTokenStore _store;

        /// <summary>Creates the service in <see cref="AuthState.LoggedOut"/>.</summary>
        /// <param name="api">The client whose token this service sets.</param>
        /// <param name="store">The token store; <see cref="AccessTokenStores.For"/> picks it for the build.</param>
        public AuthService(ApiClient api, IAccessTokenStore store)
        {
            _api = api;
            _store = store;
        }

        /// <summary>Where the service stands.</summary>
        public AuthState State { get; private set; } = AuthState.LoggedOut;

        /// <summary>Why the last login failed; null unless <see cref="State"/> is <see cref="AuthState.Failed"/>.</summary>
        public ApiError? LastError { get; private set; }

        /// <summary>
        /// Takes the kept token when it was issued for <paramref name="displayName"/>. The token is only a candidate: nothing asks the server
        /// here, so the first request may still answer <c>AUTHENTICATION_REQUIRED</c>.
        /// </summary>
        /// <param name="displayName">The dev player the app wants to be.</param>
        /// <returns>True when the service is now <see cref="AuthState.LoggedIn"/> with the kept token.</returns>
        /// <exception cref="InvalidOperationException">A login is in progress.</exception>
        public bool TryRestore(string displayName)
        {
            RequireNotLoggingIn();
            StoredLogin? stored = _store.Load();
            if (stored == null || stored.DisplayName != displayName)
            {
                return false;
            }

            _api.SetAccessToken(stored.AccessToken);
            LastError = null;
            State = AuthState.LoggedIn;
            return true;
        }

        /// <summary>
        /// Logs in with <c>POST /auth/dev-login</c>, creating the player on first use. On success the token is set on the client and kept; on
        /// failure the client has no token and <see cref="LastError"/> says why.
        /// </summary>
        /// <param name="displayName">The dev player's name (1 to 32 letters, digits, <c>_</c>, <c>-</c> or <c>.</c>; the server checks).</param>
        /// <param name="cancellationToken">Cancels the request; the service is then <see cref="AuthState.LoggedOut"/> and the call throws.</param>
        /// <returns>The state after the call: <see cref="AuthState.LoggedIn"/> or <see cref="AuthState.Failed"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="displayName"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">A login is already in progress.</exception>
        /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
        public async Task<AuthState> LoginAsync(string displayName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                throw new ArgumentException("The display name must not be empty.", nameof(displayName));
            }

            RequireNotLoggingIn();
            Drop();
            State = AuthState.LoggingIn;

            ApiResult<DevLoginResponseDto> result;
            try
            {
                result = await _api.DevLoginAsync(displayName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                State = AuthState.LoggedOut;
                throw;
            }

            if (!result.IsSuccess)
            {
                return Fail(result.Error!);
            }

            string accessToken = result.Value!.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
            {
                // The DTO requires the member, but an empty text still decodes; a login without a token is a broken answer.
                return Fail(new ApiError(ApiErrorCodes.ClientInvalidResponse, 200, "The login answer holds no access token.", null, null, null));
            }

            _api.SetAccessToken(accessToken);
            _store.Save(new StoredLogin(displayName, accessToken));
            State = AuthState.LoggedIn;
            return State;
        }

        /// <summary>Drops the token from the client and the store and goes back to <see cref="AuthState.LoggedOut"/>; used when the server refused it.</summary>
        public void Forget()
        {
            RequireNotLoggingIn();
            Drop();
        }

        /// <summary>Clears the token everywhere and the last error.</summary>
        private void Drop()
        {
            _api.SetAccessToken(null);
            _store.Clear();
            LastError = null;
            State = AuthState.LoggedOut;
        }

        /// <summary>Records a failed login and returns the new state.</summary>
        private AuthState Fail(ApiError error)
        {
            LastError = error;
            State = AuthState.Failed;
            return State;
        }

        /// <summary>Refuses a second operation while a login request is in flight: two would race for the token.</summary>
        private void RequireNotLoggingIn()
        {
            if (State == AuthState.LoggingIn)
            {
                throw new InvalidOperationException("A login is already in progress.");
            }
        }
    }
}
