namespace CityBuilder.Api.Time;

/// <summary>
/// The server clock's settings (core-instructions/04-simulation-model.md, section 1): the configuration section <c>Simulation</c>.
/// Validated on start (<see cref="ServerClock.AddServerClock"/>): a value out of range, or a scale other than 1 outside Development, stops it.
/// </summary>
public sealed class SimulationOptions
{
    /// <summary>The configuration section's name.</summary>
    public const string SectionName = "Simulation";

    /// <summary>The lowest allowed <see cref="DevTimeScale"/>: real time.</summary>
    public const int MinDevTimeScale = 1;

    /// <summary>
    /// The highest allowed <see cref="DevTimeScale"/>: a thousand seconds per second (the 8-hour production cap in under 30 seconds). Higher
    /// scales would also carry the scaled clock past the year 9999, which no <see cref="DateTimeOffset"/> holds, within a few years of
    /// <see cref="ScaledTimeProvider.Origin"/>.
    /// </summary>
    public const int MaxDevTimeScale = 1000;

    /// <summary>
    /// <c>Simulation:DevTimeScale</c>: how many seconds of server time pass per real second on a Development server (60 = one hour per
    /// minute), for playing a long scenario by hand. Default 1 (real time); between <see cref="MinDevTimeScale"/> and
    /// <see cref="MaxDevTimeScale"/>, and exactly 1 outside Development, or the start stops. The engine never sees it: the server clock does.
    /// </summary>
    public int DevTimeScale { get; set; } = MinDevTimeScale;
}
