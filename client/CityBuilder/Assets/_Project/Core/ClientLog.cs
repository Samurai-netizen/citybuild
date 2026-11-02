#nullable enable

using System;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// The client's one way to log (M0-57). A static entry point, because the code that logs is spread over three assemblies and most of
    /// it has no object to hand a logger to; the level and the sink can be replaced, so a test sees what was written. The level
    /// follows the build: <see cref="LogLevel.Debug"/> in the Editor and development builds, <see cref="LogLevel.Warning"/> in a
    /// release build (<see cref="DefaultLevelFor"/>). Callers never put a token or a request or response body in a message (<c>07</c>
    /// Logging policy); an <c>ApiError</c> is logged as its <c>ToString()</c>, which holds the code, the status and the trace id.
    /// Used on the main thread only.
    /// </summary>
    public static class ClientLog
    {
        /// <summary>The threshold in force; null means "not set", so <see cref="Level"/> falls back to the build's default.</summary>
        private static LogLevel? _level;

        /// <summary>The sink in force; null means the Unity console.</summary>
        private static ILogSink? _sink;

        /// <summary>The threshold: a line below it is dropped. Defaults by build type; set it for a test or a diagnosis.</summary>
        public static LogLevel Level
        {
            get => _level ?? DefaultLevelFor(UnityEngine.Debug.isDebugBuild);
            set => _level = value;
        }

        /// <summary>Where lines go. Defaults to <see cref="UnityLogSink"/>; a null value restores that default.</summary>
        public static ILogSink Sink
        {
            get => _sink ??= new UnityLogSink();
            set => _sink = value;
        }

        /// <summary>The level a build starts with: Debug when it may show developer detail (Editor, development build), Warning otherwise.</summary>
        /// <param name="developmentBuild"><c>Debug.isDebugBuild</c>: true in the Editor and in a development build.</param>
        public static LogLevel DefaultLevelFor(bool developmentBuild)
        {
            return developmentBuild ? LogLevel.Debug : LogLevel.Warning;
        }

        /// <summary>Gives back the build's default level and the Unity sink. Tests call it after they changed either.</summary>
        public static void Reset()
        {
            _level = null;
            _sink = null;
        }

        /// <summary>
        /// True when a line of this level would be written. Check it before building an expensive message, for example one line per
        /// request at Debug.
        /// </summary>
        public static bool IsEnabled(LogLevel level)
        {
            return level != LogLevel.Off && level >= Level;
        }

        /// <summary>Logs developer detail (<see cref="LogLevel.Debug"/>), such as a network request. Not written in a release build.</summary>
        public static void Debug(string message)
        {
            Write(LogLevel.Debug, message, null);
        }

        /// <summary>Logs a normal event (<see cref="LogLevel.Info"/>).</summary>
        public static void Info(string message)
        {
            Write(LogLevel.Info, message, null);
        }

        /// <summary>Logs a problem the game goes on after (<see cref="LogLevel.Warning"/>). Add the trace id of a failed request.</summary>
        public static void Warning(string message)
        {
            Write(LogLevel.Warning, message, null);
        }

        /// <summary>Logs a fault in the client (<see cref="LogLevel.Error"/>).</summary>
        public static void Error(string message)
        {
            Write(LogLevel.Error, message, null);
        }

        /// <summary>Logs an exception as an error, with its stack trace.</summary>
        public static void Exception(Exception exception)
        {
            Write(LogLevel.Error, exception.GetType().Name + ": " + exception.Message, exception);
        }

        /// <summary>Sends the line to the sink when its level passes.</summary>
        private static void Write(LogLevel level, string message, Exception? exception)
        {
            if (IsEnabled(level))
            {
                Sink.Write(level, message, exception);
            }
        }
    }
}
