namespace CityBuilder.BalanceSim.Tests;

/// <summary>The check-in patterns: the three forms, their start times, their folder names and the errors.</summary>
public sealed class SessionPatternTests
{
    /// <summary>A daily pattern starts at its first time (clock 0) and repeats every 24 h, times sorted.</summary>
    [Fact]
    public void Daily_StartsAtTheFirstTime_AndRepeatsEveryDay()
    {
        SessionPattern pattern = SessionPattern.Parse("20:00, 08:00,13:00 daily");

        Assert.Equal([0L, 5 * 3_600, 12 * 3_600, 24 * 3_600, 29 * 3_600, 36 * 3_600], pattern.Starts(2 * 86_400));
        Assert.Equal("daily-0800-1300-2000", pattern.Slug);
    }

    /// <summary>An interval pattern starts at 0 and repeats by its interval, in hours or minutes, with or without a space.</summary>
    [Fact]
    public void Every_RepeatsByItsInterval()
    {
        Assert.Equal([0L, 14_400, 28_800], SessionPattern.Parse("every 4h").Starts(30_000));
        Assert.Equal([0L, 14_400], SessionPattern.Parse("every 4 h").Starts(28_800));
        Assert.Equal([0L, 5_400], SessionPattern.Parse("every 90min").Starts(10_000));
        Assert.Equal("every-90min", SessionPattern.Parse("every 90 min").Slug);
    }

    /// <summary>"once a day" is one check-in per day.</summary>
    [Fact]
    public void OnceADay_IsOneCheckInPerDay()
    {
        SessionPattern pattern = SessionPattern.Parse("Once a day");

        Assert.Equal([0L, 86_400, 172_800], pattern.Starts(3 * 86_400));
        Assert.Equal("Once a day", pattern.Text);
    }

    /// <summary>Text that is none of the forms, a bad time or a bad interval is a format error.</summary>
    /// <param name="text">The bad pattern.</param>
    [Theory]
    [InlineData("twice a day")]
    [InlineData("24:00 daily")]
    [InlineData("8:5 daily")]
    [InlineData(" daily")]
    [InlineData("every 0h")]
    [InlineData("every 4")]
    [InlineData("every -1h")]
    public void Parse_RejectsBadPatterns(string text) => Assert.Throws<FormatException>(() => SessionPattern.Parse(text));
}
