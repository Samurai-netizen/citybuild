using CityBuilder.Application.Metrics;

namespace CityBuilder.Application.Tests.Metrics;

/// <summary>
/// <see cref="SessionPolicy"/> (CR-0009): a request more than the gap after the last-seen second starts a session; exactly the gap does not;
/// a gap of 0 makes every later second a new session; a negative gap is refused.
/// </summary>
public sealed class SessionPolicyTests
{
    /// <summary>The boundary of a 1 800 s gap: 1 801 s starts a session, 1 800 s and less don't, nor does a clock that went back.</summary>
    /// <param name="secondsAway">Seconds from the last-seen second to the request.</param>
    /// <param name="starts">Whether the request starts a session.</param>
    [Theory]
    [InlineData(1801, true)]
    [InlineData(86_400, true)]
    [InlineData(1800, false)]
    [InlineData(1, false)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    public void StartsSession_AroundTheGap_IsMoreThanTheGap(long secondsAway, bool starts)
    {
        const long lastSeen = 1_700_000_000;

        Assert.Equal(starts, new SessionPolicy(1800).StartsSession(lastSeen, lastSeen + secondsAway));
    }

    /// <summary>A gap of 0: the same second is the same session, the next one a new one.</summary>
    [Fact]
    public void StartsSession_ZeroGap_EveryLaterSecond()
    {
        var policy = new SessionPolicy(0);

        Assert.Equal((false, true), (policy.StartsSession(100, 100), policy.StartsSession(100, 101)));
    }

    /// <summary>The policy keeps its gap; a negative one is refused.</summary>
    [Fact]
    public void Constructor_Gap_IsKeptAndNeverNegative()
    {
        Assert.Equal(1800, new SessionPolicy(1800).GapSeconds);
        Assert.Throws<ArgumentOutOfRangeException>("gapSeconds", () => new SessionPolicy(-1));
    }
}
