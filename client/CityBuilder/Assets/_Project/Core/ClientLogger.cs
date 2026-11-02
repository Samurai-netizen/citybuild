#nullable enable

using System;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// A logger with a fixed level and a fixed sink (M0-57): a line below the level is dropped, any other goes to the sink. It holds no
    /// state that changes, so two loggers never affect each other. The game uses <see cref="ClientLog.Default"/>; a class whose logging a
    /// test checks takes a <see cref="ClientLogger"/> over a recording sink in its constructor. Callers never put a token or a request or
    /// response body in a message (<c>07</c> Logging policy); an <c>ApiError</c> is logged as its <c>ToString()</c>, which holds the code, the
    /// status and the trace id. Used on the main thread only.
    /// </summary>
    public sealed class ClientLogger
    {
        /// <summary>The sink the lines go to.</summary>
        private readonly ILogSink _sink;

        /// <summary>Creates a logger.</summary>
        /// <param name="level">The threshold: a line below it is dropped; <see cref="LogLevel.Off"/> drops everything.</param>
        /// <param name="sink">Where the lines go.</param>
        /// <exception cref="ArgumentNullException"><paramref name="sink"/> is null.</exception>
        public ClientLogger(LogLevel level, ILogSink sink)
        {
            Level = level;
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>The threshold of this logger.</summary>
        public LogLevel Level { get; }

        /// <summary>
        /// True when a line of this level would be written. Check it before building an expensive message, for example one line per
        /// request at Debug.
        /// </summary>
        public bool IsEnabled(LogLevel level)
        {
            return level != LogLevel.Off && level >= Level;
        }

        /// <summary>Logs developer detail (<see cref="LogLevel.Debug"/>), such as a network request. Not written in a release build.</summary>
        public void Debug(string message)
        {
            Write(LogLevel.Debug, message, null);
        }

        /// <summary>Logs a normal event (<see cref="LogLevel.Info"/>).</summary>
        public void Info(string message)
        {
            Write(LogLevel.Info, message, null);
        }

        /// <summary>Logs a problem the game goes on after (<see cref="LogLevel.Warning"/>). Add the trace id of a failed request.</summary>
        public void Warning(string message)
        {
            Write(LogLevel.Warning, message, null);
        }

        /// <summary>Logs a fault in the client (<see cref="LogLevel.Error"/>).</summary>
        public void Error(string message)
        {
            Write(LogLevel.Error, message, null);
        }

        /// <summary>Logs an exception as an error, with its stack trace.</summary>
        public void Exception(Exception exception)
        {
            Write(LogLevel.Error, exception.GetType().Name + ": " + exception.Message, exception);
        }

        /// <summary>Sends the line to the sink when its level passes.</summary>
        private void Write(LogLevel level, string message, Exception? exception)
        {
            if (IsEnabled(level))
            {
                _sink.Write(level, message, exception);
            }
        }
    }
}
