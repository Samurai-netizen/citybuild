#nullable enable

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// How the client stands with the server, as the player should see it (M0-41). It is the answer to "can I trust what is on screen to be
    /// current?": <see cref="Connected"/> says yes, every other value says what is wrong. The rules that move between the values are in
    /// <c>ConnectionMonitor</c> (Net); this assembly only names them.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>The client is starting: login, content and the first city are on their way.</summary>
        Connecting = 0,

        /// <summary>The last request got a usable answer.</summary>
        Connected = 1,

        /// <summary>No answer arrived: no connection, or the server did not answer in time. Trying again may work.</summary>
        Offline = 2,

        /// <summary>The server does not accept the client's login, and the one new login the client tries per poll did not fix it.</summary>
        Unauthorized = 3,

        /// <summary>The server answered with a fault of its own (5xx) or with something the client does not expect from it.</summary>
        ServerError = 4,

        /// <summary>The server's content or answers are not the ones this client was built for: it needs a matching build.</summary>
        IncompatibleVersion = 5,
    }
}
