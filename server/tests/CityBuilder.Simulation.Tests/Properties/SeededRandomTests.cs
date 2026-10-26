namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The property tests' random source: SplitMix64 with its published values, so a printed seed reproduces on any machine, and numbers that
/// stay within their range and reach both ends.
/// </summary>
public sealed class SeededRandomTests
{
    /// <summary>Over the full range, seed 0 gives the first outputs of the reference SplitMix64.</summary>
    [Fact]
    public void Between_FullRangeFromSeedZero_GivesTheSplitMix64ReferenceValues()
    {
        var random = new SeededRandom(0);

        // The first outputs of the reference SplitMix64 started at state 0.
        Assert.Equal(unchecked((long)0xE220A8397B1DCDAF), random.Between(long.MinValue, long.MaxValue));
        Assert.Equal(unchecked((long)0x6E789E6AA1B965F4), random.Between(long.MinValue, long.MaxValue));
        Assert.Equal(unchecked((long)0x06C45D188009454F), random.Between(long.MinValue, long.MaxValue));
    }

    /// <summary>Draws stay within the range, both ends included, and reach both ends.</summary>
    [Fact]
    public void Between_ManyDraws_StayInRangeAndReachBothEnds()
    {
        var random = new SeededRandom(42);

        long[] draws = [.. Enumerable.Range(0, 1000).Select(_ => random.Between(-3, 3))];

        Assert.All(draws, draw => Assert.InRange(draw, -3, 3));
        Assert.Contains(-3L, draws);
        Assert.Contains(3L, draws);
    }

    /// <summary>Two generators with the same seed give the same numbers.</summary>
    [Fact]
    public void Between_SameSeed_GivesTheSameSequence()
    {
        var first = new SeededRandom(9);
        var second = new SeededRandom(9);

        Assert.Equal(
            Enumerable.Range(0, 20).Select(_ => first.Between(0, 1_000_000)),
            Enumerable.Range(0, 20).Select(_ => second.Between(0, 1_000_000)));
    }

    /// <summary>A range whose maximum is below its minimum throws.</summary>
    [Fact]
    public void Between_EmptyRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRandom(1).Between(5, 4));
    }
}
