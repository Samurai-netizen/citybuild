namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>The seed runner of the properties: a failing or throwing seed fails the test and prints everything needed to replay it.</summary>
public sealed class PropertyCheckTests
{
    /// <summary>A check that holds for every seed passes, and runs on seeds 0, 1 and 2 in that order.</summary>
    [Fact]
    public void ForEachSeed_CheckHoldsForEverySeed_Passes()
    {
        var seen = new List<int>();

        PropertyCheck.ForEachSeed("always", "fixture", (history, _) =>
        {
            seen.Add(history.Seed);
            return null;
        }, seeds: 3);

        Assert.Equal([0, 1, 2], seen);
    }

    /// <summary>A seed whose check fails fails the run with the property, content, seed, difference, and the history's operations.</summary>
    [Fact]
    public void ForEachSeed_CheckFailsForOneSeed_PrintsPropertyContentSeedDifferenceAndOperations()
    {
        Exception failure = Assert.ThrowsAny<Exception>(() =>
            PropertyCheck.ForEachSeed("sample", "fixture", (history, _) => history.Seed == 2 ? "clock: expected 1, actual 2" : null, seeds: 5));

        string expectedOperations = CityGenerator.Generate(PropertyContent.Named("fixture"), 2).Describe();
        Assert.Equal($"Property \"sample\" failed on content fixture, seed 2: clock: expected 1, actual 2\n{expectedOperations}", failure.Message);
    }

    /// <summary>A check that throws fails the run with the exception and the history.</summary>
    [Fact]
    public void ForEachSeed_CheckThrows_PrintsTheExceptionAndTheOperations()
    {
        Exception failure = Assert.ThrowsAny<Exception>(() =>
            PropertyCheck.ForEachSeed("throwing", "fixture", (_, _) => throw new InvalidOperationException("engine broke"), seeds: 1));

        Assert.StartsWith("Property \"throwing\" failed on content fixture, seed 0: it threw System.InvalidOperationException: engine broke", failure.Message);
        Assert.Contains("seed 0, ", failure.Message);
    }
}
