#nullable enable

using System;
using UnityEngine;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// The game's sink: writes to the Unity console and, on a device, to the platform log, through <see cref="Debug"/>. It is the only
    /// place in the runtime code that calls <c>UnityEngine.Debug</c> to log (a test checks that), so the level and the policy of
    /// <see cref="ClientLog"/> cannot be skipped.
    /// </summary>
    public sealed class UnityLogSink : ILogSink
    {
        /// <summary>Marks a Debug line, because Unity has no level below "log" and an Info line would look the same.</summary>
        private const string DebugPrefix = "[debug] ";

        /// <summary>Writes the line as a Unity log, warning or error; an exception goes with its stack trace.</summary>
        public void Write(LogLevel level, string message, Exception? exception)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log(DebugPrefix + message);
                    break;
                case LogLevel.Info:
                    Debug.Log(message);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                default:
                    // LogException prints the message and the stack trace, so an exception is not also written as an error line.
                    if (exception != null)
                    {
                        Debug.LogException(exception);
                    }
                    else
                    {
                        Debug.LogError(message);
                    }

                    break;
            }
        }
    }
}
