using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Tests.Golden;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The contents every property runs over, by name (a test case's name must be plain text). Both are frozen, so a seed keeps its history:
/// <c>golden-v2</c> is the game's content <c>mvp-2</c> of CR-0008 (realistic numbers, the 8 h cap until CR-0008-T1 part H, goods in
/// building stores, coins only from the locked Trading Post), and <c>fixture</c> is the rule tests' <see cref="CityFixture.TestContent"/> (a
/// 1 h cap, two eras, a locked chain building, a currency output from the start, upgrades that refund inputs), which reaches chains and
/// upgrades in many more seeds. The seeds' histories changed with CR-0008 (the content and the model did).
/// </summary>
internal static class PropertyContent
{
    /// <summary>The seeds every property checks: 0 to 499 (the testing strategy asks for at least 500 per property).</summary>
    public const int Seeds = 500;

    /// <summary>The names of the contents, as test data.</summary>
    public static TheoryData<string> Names { get; } = ["golden-v2", "fixture"];

    /// <summary>The content with this name.</summary>
    public static GameContent Named(string name) => name switch
    {
        "golden-v2" => GoldenContent.V2,
        "fixture" => CityFixture.TestContent,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a property content."),
    };
}
