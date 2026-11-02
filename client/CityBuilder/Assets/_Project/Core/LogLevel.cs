#nullable enable

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// How serious a log line is, and the threshold <see cref="ClientLog.Level"/> sets: a line is written when its level is at or above the
    /// threshold. The order of the members is the order of importance, so comparing them is the filter.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Detail for developers, for example one line per network request. Written in the Editor and development builds only.</summary>
        Debug = 0,

        /// <summary>A normal event worth knowing about in a log, such as the start of the game.</summary>
        Info = 1,

        /// <summary>Something went wrong but the game goes on: a refused command, a failed request.</summary>
        Warning = 2,

        /// <summary>A fault in the client itself: a missing asset, a thrown exception.</summary>
        Error = 3,

        /// <summary>As a threshold, writes nothing. Not a level for a line.</summary>
        Off = 4,
    }
}
