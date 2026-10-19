using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Runs a property over the seeds: for each seed it generates the city's history and hands it to the check, with a second random source for
/// the check's own choices (split points, lengths). The first seed whose check fails, or throws, fails the test with the property, the
/// content, the seed, what went wrong and the numbered operation list, so the case can be replayed by hand.
/// </summary>
internal static class PropertyCheck
{
    /// <summary>
    /// Checks <paramref name="property"/> for seeds 0 to <paramref name="seeds"/> − 1. The check returns null when the property holds and a
    /// description of the difference when it doesn't.
    /// </summary>
    public static void ForEachSeed(
        string property, string contentName, Func<CityHistory, SeededRandom, string?> check, int seeds = PropertyContent.Seeds)
    {
        GameContent content = PropertyContent.Named(contentName);
        for (int seed = 0; seed < seeds; seed++)
        {
            string heading = $"Property \"{property}\" failed on content {contentName}, seed {seed}";
            CityHistory? history = null;
            string? failure;
            try
            {
                history = CityGenerator.Generate(content, seed);

                // The bitwise complement keeps the check's numbers apart from the generator's, which start from the seed itself.
                failure = check(history, new SeededRandom(~seed));
            }
            catch (Exception exception)
            {
                // Not swallowed: the test fails with the exception's full text, plus the seed and operations needed to reproduce it.
                failure = $"it threw {exception}";
            }

            if (failure is not null)
            {
                Assert.Fail($"{heading}: {failure}\n{history?.Describe() ?? "(the history could not be generated)"}");
            }
        }
    }
}
