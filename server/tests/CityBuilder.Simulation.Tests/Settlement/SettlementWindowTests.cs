using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Simulation.Tests.Settlement;

/// <summary>The window a settlement covers, from the last settled server second to now (<c>04</c> section 1).</summary>
public sealed class SettlementWindowTests
{
    // A server second in 2026, so the tests use the kind of values the server passes.
    private const long _lastSettled = 1_790_000_000;

    [Fact]
    public void Compute_SameSecond_ElapsesNothingAndKeepsTheLastSettledSecond()
    {
        SettlementWindow window = SettlementWindow.Compute(_lastSettled, _lastSettled);

        Assert.Equal(0, window.ElapsedSeconds);
        Assert.Equal(_lastSettled, window.NewLastSettledUnixSec);
        Assert.False(window.IsClockBehind);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(28_800L)] // 8 hours, the M0 production cap
    [InlineData(28_801L)]
    [InlineData(2_592_000L)] // 30 days
    public void Compute_LaterSecond_ElapsesTheDifferenceAndMovesToNow(long seconds)
    {
        SettlementWindow window = SettlementWindow.Compute(_lastSettled, _lastSettled + seconds);

        Assert.Equal(seconds, window.ElapsedSeconds);
        Assert.Equal(_lastSettled + seconds, window.NewLastSettledUnixSec);
        Assert.False(window.IsClockBehind);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(3_600L)]
    public void Compute_EarlierSecond_ElapsesNothingAndKeepsTheLastSettledSecond(long secondsBack)
    {
        SettlementWindow window = SettlementWindow.Compute(_lastSettled, _lastSettled - secondsBack);

        Assert.Equal(0, window.ElapsedSeconds);
        Assert.Equal(_lastSettled, window.NewLastSettledUnixSec);
        Assert.True(window.IsClockBehind);
    }

    [Fact]
    public void Compute_SuccessiveWindowsWithTheClockGoingBack_AddUpToTheSpanFromFirstToLast()
    {
        // The clock goes back by 2 seconds and then forward again: the seconds it repeats are not counted twice.
        long[] nows = [_lastSettled + 5, _lastSettled + 3, _lastSettled + 10];
        long lastSettled = _lastSettled;
        long total = 0;

        foreach (long now in nows)
        {
            SettlementWindow window = SettlementWindow.Compute(lastSettled, now);
            total += window.ElapsedSeconds;
            lastSettled = window.NewLastSettledUnixSec;
        }

        Assert.Equal(10, total);
        Assert.Equal(_lastSettled + 10, lastSettled);
    }

    [Fact]
    public void Compute_DifferenceBeyondLong_Throws()
    {
        Assert.Throws<OverflowException>(() => SettlementWindow.Compute(long.MinValue, 1));
    }
}
