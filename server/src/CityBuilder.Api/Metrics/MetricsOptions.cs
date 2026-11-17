namespace CityBuilder.Api.Metrics;

/// <summary>
/// The player metrics' settings (CR-0009): the configuration section <c>Metrics</c>. Validated on start
/// (<see cref="PlayerMetrics.AddPlayerMetrics"/>): a value out of range stops it. Nothing in the game reads them (<c>02</c> rule 23).
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>The configuration section's name.</summary>
    public const string SectionName = "Metrics";

    /// <summary>The lowest allowed <see cref="SessionGapSeconds"/>: a minute.</summary>
    public const long MinSessionGapSeconds = 60;

    /// <summary>The highest allowed <see cref="SessionGapSeconds"/>: a day.</summary>
    public const long MaxSessionGapSeconds = 86_400;

    /// <summary>
    /// <c>Metrics:SessionGapSeconds</c>: how long, in real seconds, a player may be away before their next request that settles the city
    /// starts a new play session (the <c>session.start</c> event). Default 1800 (30 minutes); <see cref="MinSessionGapSeconds"/> to
    /// <see cref="MaxSessionGapSeconds"/>. The server uses it times the clock's scale (<c>Simulation:DevTimeScale</c>), so it stays real
    /// time on a fast clock.
    /// </summary>
    public long SessionGapSeconds { get; set; } = 1800;
}
