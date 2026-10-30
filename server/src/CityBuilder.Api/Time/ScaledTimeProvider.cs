namespace CityBuilder.Api.Time;

/// <summary>
/// The Development server's fast clock (core-instructions/04-simulation-model.md, section 1; <c>Simulation:DevTimeScale</c>): a
/// <see cref="TimeProvider"/> whose <see cref="GetUtcNow"/> runs <see cref="Scale"/> times as fast as the real clock it wraps, counted from
/// the fixed <see cref="Origin"/>: <c>now = Origin + (real now − Origin) × Scale</c>. Because the origin is fixed, the clock goes on where it
/// was after a restart with the same scale; a different scale moves it (forward, or back behind every stored city), which is why the dev
/// database is reset after a change (docs/getting-started.md → Configuration). Only the wall clock is scaled: timestamps, elapsed times and
/// timers stay real time, so durations in the logs are real ones. Used only in Development (<see cref="ServerClock"/>).
/// </summary>
public sealed class ScaledTimeProvider : TimeProvider
{
    /// <summary>
    /// The instant both clocks agree on: 2026-01-01T00:00:00Z. Fixed, so the scaled clock is the same function of the real one in every run.
    /// </summary>
    public static readonly DateTimeOffset Origin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The real clock it scales.</summary>
    private readonly TimeProvider _real;

    /// <summary>Wraps a real clock.</summary>
    /// <param name="real">The real clock: <see cref="TimeProvider.System"/>, or a fake one in tests.</param>
    /// <param name="scale">Server seconds per real second; at least 1.</param>
    /// <exception cref="ArgumentNullException"><paramref name="real"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> is below 1.</exception>
    public ScaledTimeProvider(TimeProvider real, int scale)
    {
        _real = real ?? throw new ArgumentNullException(nameof(real));
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        Scale = scale;
    }

    /// <summary>Server seconds per real second: the value <c>GET /content</c> reports as <c>rules.timeScale</c>.</summary>
    public int Scale { get; }

    /// <summary>Server time is UTC, like the real server clock's.</summary>
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>The scaled now: the real time since <see cref="Origin"/>, times <see cref="Scale"/>, after <see cref="Origin"/>.</summary>
    /// <returns>The server time, in UTC.</returns>
    /// <exception cref="OverflowException">The scaled time doesn't fit in ticks.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The scaled time is past <see cref="DateTimeOffset.MaxValue"/>.</exception>
    public override DateTimeOffset GetUtcNow()
    {
        long realTicksSinceOrigin = checked(_real.GetUtcNow().UtcTicks - Origin.UtcTicks);
        return new DateTimeOffset(checked(Origin.UtcTicks + (realTicksSinceOrigin * Scale)), TimeSpan.Zero);
    }
}
