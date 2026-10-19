using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Tests.Golden;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The contents every property runs over, by name (a test case's name must be plain text). Both are frozen, so a seed keeps its history:
/// <c>golden-v1</c> is the game's M0 content as of M0-11 (realistic numbers, an 8 h cap, coins only from the locked Trading Post), and
/// <c>fixture</c> is the rule tests' <see cref="CityFixture.TestContent"/> (a 1 h cap, two eras, a locked chain building, an uncapped
/// output from the start, upgrades that refund inputs), which reaches chains and upgrades in many more seeds.
/// </summary>
internal static class PropertyContent
{
    /// <summary>The seeds every property checks: 0 to 499 (the testing strategy asks for at least 500 per property).</summary>
    public const int Seeds = 500;

    /// <summary>The names of the contents, as test data.</summary>
    public static TheoryData<string> Names { get; } = ["golden-v1", "fixture"];

    /// <summary>The content with this name.</summary>
    public static GameContent Named(string name) => name switch
    {
        "golden-v1" => GoldenContent.V1,
        "fixture" => CityFixture.TestContent,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a property content."),
    };
}
