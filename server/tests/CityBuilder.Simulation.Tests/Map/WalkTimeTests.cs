using CityBuilder.Simulation.Map;

namespace CityBuilder.Simulation.Tests.Map;

/// <summary>
/// The walk time formula of <c>core-instructions/04-simulation-model.md</c>, section 17: the sum of <c>leave + enter</c> over the steps
/// (double milliseconds), the walk bonus applied, rounded up to whole seconds once, and never below one second.
/// </summary>
public sealed class WalkTimeTests
{
    /// <summary>Exact seconds stay exact; one double millisecond more rounds up to the next second.</summary>
    [Theory]
    [InlineData(20_000L, 0, 10L)]
    [InlineData(20_001L, 0, 11L)]
    [InlineData(15_000L, 0, 8L)]
    [InlineData(1L, 0, 1L)]
    public void Seconds_WithoutBonus_RoundsUpOnce(long doubleMillis, int bonus, long expected) =>
        Assert.Equal(expected, WalkTime.Seconds(doubleMillis, bonus));

    /// <summary>A walk bonus shortens the whole walk before the rounding: 200 ‰ off 10 000 double milliseconds is 8 000, 4 s.</summary>
    [Theory]
    [InlineData(10_000L, 200, 4L)]
    [InlineData(30_000L, 200, 12L)]
    [InlineData(10_000L, 1000, 1L)]
    public void Seconds_WithBonus_ShortensBeforeRounding(long doubleMillis, int bonus, long expected) =>
        Assert.Equal(expected, WalkTime.Seconds(doubleMillis, bonus));

    /// <summary>A walk of no step (a building to itself) still takes one second, so an arrival is always after its departure.</summary>
    [Fact]
    public void Seconds_NoStep_IsOneSecond() => Assert.Equal(1L, WalkTime.Seconds(0, 0));

    /// <summary>The shortest walk is one step between two adjacent buildings, both counted as ground: 5 000 ms ground is 5 s, 4 s at 200 ‰.</summary>
    [Theory]
    [InlineData(5000L, 0, 5L)]
    [InlineData(5000L, 200, 4L)]
    public void ShortestWalkSeconds_OneGroundStep_IsTheGroundTime(long ground, int bonus, long expected) =>
        Assert.Equal(expected, WalkTime.ShortestWalkSeconds(ground, bonus));

    /// <summary>A negative sum or a bonus outside 0 to 1000 ‰ is a programmer error.</summary>
    [Fact]
    public void Seconds_BadArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WalkTime.Seconds(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WalkTime.Seconds(10, 1001));
    }
}
