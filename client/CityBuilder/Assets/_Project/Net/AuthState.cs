#nullable enable

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Where <see cref="AuthService"/> stands. A login in progress is its own state so a screen can say "Connecting"; a failed login keeps the
    /// reason in <see cref="AuthService.LastError"/> instead of being an exception (<c>14</c> Correctness).
    /// </summary>
    public enum AuthState
    {
        /// <summary>No token: the app just started, the token was dropped, or a login was cancelled.</summary>
        LoggedOut = 0,

        /// <summary>A dev login request is on its way.</summary>
        LoggingIn = 1,

        /// <summary>The API client holds a token. The server has not necessarily accepted it yet: a restored token is only a candidate.</summary>
        LoggedIn = 2,

        /// <summary>The last dev login got no token; <see cref="AuthService.LastError"/> says why.</summary>
        Failed = 3,
    }
}
