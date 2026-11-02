#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Core;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>A log sink that keeps every line it is given, so a test can check what <see cref="ClientLog"/> wrote (M0-57).</summary>
    public sealed class RecordingLogSink : ILogSink
    {
        /// <summary>The lines written, in order.</summary>
        public List<(LogLevel Level, string Message, Exception? Exception)> Lines { get; } = new List<(LogLevel, string, Exception?)>();

        /// <summary>Keeps the line.</summary>
        public void Write(LogLevel level, string message, Exception? exception)
        {
            Lines.Add((level, message, exception));
        }
    }
}
