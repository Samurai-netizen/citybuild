#nullable enable

using System;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// The client's one way to log (M0-57), for code that has no logger handed to it: <see cref="Default"/> and a method per level that
    /// forwards to it. Nothing here can be changed after the game starts (no settable level or sink, so it is not mutable state): the
    /// level follows the build, <see cref="LogLevel.Debug"/> in the Editor and development builds and <see cref="LogLevel.Warning"/> in a
    /// release build (<see cref="DefaultLevelFor"/>), and the sink is the Unity console. A class whose logging a test checks takes a
    /// <see cref="ClientLogger"/> in its constructor instead (<see cref="ApiClient"/>, <c>CommandFeedback</c>).
    /// </summary>
    public static class ClientLog
    {
        /// <summary>The logger of the game: the build's level over the Unity console.</summary>
        public static readonly ClientLogger Default = new ClientLogger(DefaultLevelFor(UnityEngine.Debug.isDebugBuild), new UnityLogSink());

        /// <summary>The level a build starts with: Debug when it may show developer detail (Editor, development build), Warning otherwise.</summary>
        /// <param name="developmentBuild"><c>Debug.isDebugBuild</c>: true in the Editor and in a development build.</param>
        public static LogLevel DefaultLevelFor(bool developmentBuild)
        {
            return developmentBuild ? LogLevel.Debug : LogLevel.Warning;
        }

        /// <summary>True when <see cref="Default"/> writes a line of this level (<see cref="ClientLogger.IsEnabled"/>).</summary>
        public static bool IsEnabled(LogLevel level)
        {
            return Default.IsEnabled(level);
        }

        /// <summary>Logs developer detail with <see cref="Default"/> (<see cref="ClientLogger.Debug"/>).</summary>
        public static void Debug(string message)
        {
            Default.Debug(message);
        }

        /// <summary>Logs a normal event with <see cref="Default"/> (<see cref="ClientLogger.Info"/>).</summary>
        public static void Info(string message)
        {
            Default.Info(message);
        }

        /// <summary>Logs a problem the game goes on after with <see cref="Default"/> (<see cref="ClientLogger.Warning"/>).</summary>
        public static void Warning(string message)
        {
            Default.Warning(message);
        }

        /// <summary>Logs a fault in the client with <see cref="Default"/> (<see cref="ClientLogger.Error"/>).</summary>
        public static void Error(string message)
        {
            Default.Error(message);
        }

        /// <summary>Logs an exception as an error with <see cref="Default"/> (<see cref="ClientLogger.Exception"/>).</summary>
        public static void Exception(Exception exception)
        {
            Default.Exception(exception);
        }
    }
}
