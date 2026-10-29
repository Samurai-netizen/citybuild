using System.Diagnostics;

namespace CityBuilder.Api.Http;

/// <summary>
/// The duration the log lines of an endpoint carry (<c>{DurationMs}</c>): real time from the start of the endpoint's work, read from the
/// high-resolution timestamp, so a Development fast clock (<c>Simulation:DevTimeScale</c>) never changes it.
/// </summary>
internal static class RequestDuration
{
    /// <summary>The milliseconds since a <see cref="Stopwatch.GetTimestamp"/>, to one decimal.</summary>
    /// <param name="startedAt">The timestamp the work started at.</param>
    /// <returns>The real time since then, in milliseconds.</returns>
    public static double MillisecondsSince(long startedAt) => Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 1);
}
