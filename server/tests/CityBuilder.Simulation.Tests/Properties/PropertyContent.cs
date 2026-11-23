using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Tests.Content;
using CityBuilder.Simulation.Tests.Golden;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The contents every property runs over, by name (a test case's name must be plain text). All are frozen, so a seed keeps its history:
/// <c>golden-v2</c> is the game's content <c>mvp-2</c> of CR-0008 (realistic numbers, the 8 h cap until CR-0008-T1 part H, goods in
/// building stores, coins only from the locked Trading Post), and <c>fixture</c> is the rule tests' <see cref="CityFixture.TestContent"/> (a
/// 1 h cap, two eras, a locked chain building, a currency output from the start, upgrades that refund inputs), which reaches chains and
/// upgrades in many more seeds; <c>fixture-m1</c> is that fixture with its guild open from the start (M1-02: a raised level cap and a third
/// builder) and a builder queue of two places (CR-0013). The seeds' histories changed with CR-0008 (the content and the model did) and, for <c>fixture</c>, with M1-02's nodes.
/// </summary>
internal static class PropertyContent
{
    /// <summary>The seeds every property checks: 0 to 499 (the testing strategy asks for at least 500 per property).</summary>
    public const int Seeds = 500;

    /// <summary>The names of the contents, as test data.</summary>
    public static TheoryData<string> Names { get; } = ["golden-v2", "fixture", "fixture-m1"];

    /// <summary>
    /// The fixture with its guild node open from the start (no prerequisite), so most seeds complete it and play with the raised hut level
    /// and the third builder slot (M1-02), and with a builder queue of two places, so orders made with every builder busy wait (CR-0013). The plain fixture keeps the guild behind tallying: a node open from the start changes which
    /// research its seeds pick, and its labor reduction cases depend on that.
    /// </summary>
    private static readonly GameContent _fixtureM1 = ContentAssert.Valid(ContentFixture.ValidWith(data =>
    {
        ContentFixture.Research(data, "guild").Prerequisites = null;
        data.Rules!.BuilderQueueLength = 2;
        CityFixture.Extend(data);
    }));

    /// <summary>The content with this name.</summary>
    public static GameContent Named(string name) => name switch
    {
        "golden-v2" => GoldenContent.V2,
        "fixture" => CityFixture.TestContent,
        "fixture-m1" => _fixtureM1,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a property content."),
    };
}
