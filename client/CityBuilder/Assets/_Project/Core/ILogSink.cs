#nullable enable

using System;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// Where <see cref="ClientLog"/> sends the lines that pass its level. The game uses <see cref="UnityLogSink"/>; a test uses a sink
    /// that keeps the lines, so it can check what was written without reading the Unity console.
    /// </summary>
    public interface ILogSink
    {
        /// <summary>Writes one line.</summary>
        /// <param name="level">The level of the line; never <see cref="LogLevel.Off"/>.</param>
        /// <param name="message">The text of the line. It holds no token and no body (<c>07</c> Logging policy).</param>
        /// <param name="exception">The exception the line is about, or null.</param>
        void Write(LogLevel level, string message, Exception? exception);
    }
}
