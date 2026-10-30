namespace CityBuilder.Api.Time;

/// <summary>The log messages about the server clock (source-generated, decision 029 in core-instructions/12-decisions.md).</summary>
internal static partial class ClockLog
{
    /// <summary>
    /// Logs at startup that the Development clock runs fast (event id 7000; ids 7000–7999 belong to clock messages), as a warning: every
    /// city on this server ages that many times as fast, and a database settled at another scale has to be reset. The source generator
    /// writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the class that built the clock.</param>
    /// <param name="timeScale">Server seconds per real second.</param>
    /// <param name="serverTimeUtc">The server time at startup in ISO 8601 UTC with a literal <c>Z</c>, as the log timestamps (a <c>+</c> would
    /// be escaped by the JSON writer, and a <see cref="DateTimeOffset"/> value would be written in the machine's culture).</param>
    [LoggerMessage(EventId = 7000, Level = LogLevel.Warning,
        Message = "Development clock runs {TimeScale} times as fast as real time; server time is {ServerTimeUtc}")]
    public static partial void ScaledClock(ILogger logger, int timeScale, string serverTimeUtc);
}
