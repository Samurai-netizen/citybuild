using CityBuilder.Api.Time;
using Microsoft.Extensions.Time.Testing;

namespace CityBuilder.IntegrationTests.Time;

/// <summary>
/// The arithmetic of the Development server's fast clock (<see cref="ScaledTimeProvider"/>, core-instructions/04-simulation-model.md
/// section 1): <c>now = Origin + (real now − Origin) × Scale</c>, driven by a fake real clock. No server, no Docker.
/// </summary>
public sealed class ScaledTimeProviderTests
{
    /// <summary>At the origin both clocks show the origin, whatever the scale.</summary>
    /// <param name="scale">The scale.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(1000)]
    public void GetUtcNow_RealClockAtTheOrigin_IsTheOrigin(int scale)
    {
        var clock = new ScaledTimeProvider(new FakeTimeProvider(ScaledTimeProvider.Origin), scale);

        Assert.Equal(ScaledTimeProvider.Origin, clock.GetUtcNow());
    }

    /// <summary>Real time since the origin is multiplied by the scale: ten real minutes at 60 are ten hours.</summary>
    [Fact]
    public void GetUtcNow_TenRealMinutesAfterTheOriginAtSixty_IsTenHoursAfterIt()
    {
        var clock = new ScaledTimeProvider(new FakeTimeProvider(ScaledTimeProvider.Origin.AddMinutes(10)), 60);

        Assert.Equal(ScaledTimeProvider.Origin.AddHours(10), clock.GetUtcNow());
    }

    /// <summary>Each real second the real clock moves, the scaled clock moves <c>Scale</c> seconds, fractions of a second included.</summary>
    /// <param name="scale">The scale.</param>
    /// <param name="realMilliseconds">How far the real clock moves.</param>
    /// <param name="scaledMilliseconds">How far the scaled clock must move.</param>
    [Theory]
    [InlineData(60, 1_000, 60_000)]
    [InlineData(60, 500, 30_000)]
    [InlineData(7, 1, 7)]
    [InlineData(1000, 3_600_000, 3_600_000_000)]
    public void GetUtcNow_RealClockMoves_MovesScaleTimesAsFar(int scale, long realMilliseconds, long scaledMilliseconds)
    {
        var real = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        var clock = new ScaledTimeProvider(real, scale);
        DateTimeOffset before = clock.GetUtcNow();

        real.Advance(TimeSpan.FromMilliseconds(realMilliseconds));

        Assert.Equal(TimeSpan.FromMilliseconds(scaledMilliseconds), clock.GetUtcNow() - before);
    }

    /// <summary>A scale of 1 is the real clock.</summary>
    [Fact]
    public void GetUtcNow_ScaleOne_IsTheRealTime()
    {
        var real = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 34, 56, 789, TimeSpan.Zero));

        Assert.Equal(real.GetUtcNow(), new ScaledTimeProvider(real, 1).GetUtcNow());
    }

    /// <summary>
    /// The clock is a function of the real time alone: a new instance (a restarted server) with the same scale shows the same time, so the
    /// cities' clocks go on where they were.
    /// </summary>
    [Fact]
    public void GetUtcNow_NewInstanceSameScale_ShowsTheSameTime()
    {
        var real = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        DateTimeOffset beforeRestart = new ScaledTimeProvider(real, 60).GetUtcNow();

        Assert.Equal(beforeRestart, new ScaledTimeProvider(real, 60).GetUtcNow());
    }

    /// <summary>
    /// Another scale shows another time for the same real time (why the dev database is reset after a change): 300 real days after the
    /// origin are 300 days at 1 and 18 000 days at 60.
    /// </summary>
    [Fact]
    public void GetUtcNow_AnotherScale_ShowsAnotherTime()
    {
        var real = new FakeTimeProvider(ScaledTimeProvider.Origin.AddDays(300));

        Assert.Equal(ScaledTimeProvider.Origin.AddDays(300), new ScaledTimeProvider(real, 1).GetUtcNow());
        Assert.Equal(ScaledTimeProvider.Origin.AddDays(18_000), new ScaledTimeProvider(real, 60).GetUtcNow());
    }

    /// <summary>The result is UTC, as the real server clock's.</summary>
    [Fact]
    public void GetUtcNow_Always_HasNoOffset()
    {
        var clock = new ScaledTimeProvider(new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero)), 60);

        Assert.Equal(TimeSpan.Zero, clock.GetUtcNow().Offset);
        Assert.Equal(TimeZoneInfo.Utc, clock.LocalTimeZone);
    }

    /// <summary>A scale below 1 is refused.</summary>
    /// <param name="scale">The scale.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void Constructor_ScaleBelowOne_Throws(int scale) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScaledTimeProvider(TimeProvider.System, scale));

    /// <summary>A real clock is required.</summary>
    [Fact]
    public void Constructor_NoRealClock_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ScaledTimeProvider(null!, 60));
}
