using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The content of M1-02's research effects (<c>core-instructions/25-eras-and-research.md</c> → Effect types, <c>04</c> §14): a building
/// type's base max level (<c>baseMaxLevel</c>), and the effects <c>maxLevelBonus</c>, <c>builderSlotBonus</c> and <c>grantCharter</c>: how
/// they are read, which values they may carry, the rules on their numbers and targets, and that a level above the base max level is
/// reachable only through research (rule 16). The fixture's hut has three levels and a base max level of 2; the guild node raises it and
/// adds a builder slot, and the charter node grants the charter.
/// </summary>
public sealed class ContentEra1EffectTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    /// <summary>A building type's base max level and the three effects become their definitions, with their values and in file order.</summary>
    [Fact]
    public void Create_NewEffectsAndBaseMaxLevel_KeepTheirValues()
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.True(content.TryGetBuilding(new BuildingTypeId("hut"), out BuildingDefinition? hut));
        Assert.Equal(2, hut.BaseMaxLevel);
        Assert.Equal(3, hut.MaxLevel);
        Assert.True(content.TryGetResearch(new ResearchId("guild"), out ResearchDefinition? guild));
        Assert.Collection(
            guild.Effects,
            effect =>
            {
                MaxLevelBonusEffect bonus = Assert.IsType<MaxLevelBonusEffect>(effect);
                Assert.Equal(new BuildingTypeId("hut"), bonus.Building);
                Assert.Equal(1, bonus.Amount);
            },
            effect => Assert.Equal(1, Assert.IsType<BuilderSlotBonusEffect>(effect).Amount));
        Assert.True(content.TryGetResearch(new ResearchId("charter"), out ResearchDefinition? charter));
        Assert.IsType<GrantCharterEffect>(Assert.Single(charter.Effects));
    }

    /// <summary>Without <c>baseMaxLevel</c>, every level is open from the start: the base max level is the number of levels.</summary>
    [Fact]
    public void Create_WithoutBaseMaxLevel_BaseMaxLevelIsTheLastLevel()
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.True(content.TryGetBuilding(new BuildingTypeId("lumber_camp"), out BuildingDefinition? camp));
        Assert.Equal(2, camp.BaseMaxLevel);
        Assert.Equal(camp.MaxLevel, camp.BaseMaxLevel);
    }

    /// <summary>Values an effect type doesn't have, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _unexpectedValues = new()
    {
        ["research[guild].effects[#0].permille"] = data => Research(data, "guild").Effects![0]!.Permille = 10,
        ["research[guild].effects[#0].item"] = data => Research(data, "guild").Effects![0]!.Item = "wood",
        ["research[guild].effects[#1].building"] = data => Research(data, "guild").Effects![1]!.Building = "hut",
        ["research[charter].effects[#0].amount"] = data => Research(data, "charter").Effects![0]!.Amount = 1,
        ["research[charter].effects[#0].building"] = data => Research(data, "charter").Effects![0]!.Building = "hut",
    };

    /// <summary>Required values left out, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _missingValues = new()
    {
        ["research[guild].effects[#0].building"] = data => Research(data, "guild").Effects![0]!.Building = null,
        ["research[guild].effects[#0].amount"] = data => Research(data, "guild").Effects![0]!.Amount = null,
        ["research[guild].effects[#1].amount"] = data => Research(data, "guild").Effects![1]!.Amount = null,
    };

    /// <summary>Numbers that must be greater than zero, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _notPositive = new()
    {
        ["research[guild].effects[#0].amount"] = data => Research(data, "guild").Effects![0]!.Amount = 0,
        ["research[guild].effects[#1].amount"] = data => Research(data, "guild").Effects![1]!.Amount = 0,
        ["buildings[hut].baseMaxLevel"] = data => Building(data, "hut").BaseMaxLevel = 0,
    };

    /// <summary>The error paths of <see cref="_unexpectedValues"/>.</summary>
    public static TheoryData<string> UnexpectedValuePaths => new(_unexpectedValues.Keys);

    /// <summary>The error paths of <see cref="_missingValues"/>.</summary>
    public static TheoryData<string> MissingValuePaths => new(_missingValues.Keys);

    /// <summary>The error paths of <see cref="_notPositive"/>.</summary>
    public static TheoryData<string> NotPositivePaths => new(_notPositive.Keys);

    /// <summary>A value of another effect type gives exactly one <see cref="ContentErrorCode.UnexpectedValue"/>.</summary>
    [Theory]
    [MemberData(nameof(UnexpectedValuePaths))]
    public void Create_NewEffectWithAValueOfAnotherType_ReportsUnexpectedValue(string path) =>
        ContentAssert.SingleError(ValidWith(_unexpectedValues[path]), ContentErrorCode.UnexpectedValue, path);

    /// <summary>A missing building or amount gives exactly one <see cref="ContentErrorCode.MissingValue"/>.</summary>
    [Theory]
    [MemberData(nameof(MissingValuePaths))]
    public void Create_NewEffectWithoutARequiredValue_ReportsMissingValue(string path) =>
        ContentAssert.SingleError(ValidWith(_missingValues[path]), ContentErrorCode.MissingValue, path);

    /// <summary>A bonus of 0 or a base max level of 0 gives exactly one <see cref="ContentErrorCode.NotPositive"/>.</summary>
    [Theory]
    [MemberData(nameof(NotPositivePaths))]
    public void Create_NewEffectOrBaseMaxLevelNotPositive_ReportsNotPositive(string path) =>
        ContentAssert.SingleError(ValidWith(_notPositive[path]), ContentErrorCode.NotPositive, path);

    /// <summary>A base max level above the type's last level gives one <see cref="ContentErrorCode.LevelCapMismatch"/>.</summary>
    [Fact]
    public void Create_BaseMaxLevelAboveTheLastLevel_ReportsLevelCapMismatch() =>
        ContentAssert.SingleError(
            ValidWith(data => Building(data, "hut").BaseMaxLevel = 4), ContentErrorCode.LevelCapMismatch, "buildings[hut].baseMaxLevel");

    /// <summary>Bonuses that would raise a type's cap past its last level give one <see cref="ContentErrorCode.LevelCapMismatch"/> for the type.</summary>
    [Fact]
    public void Create_MaxLevelBonusesPastTheLastLevel_ReportsLevelCapMismatch() =>
        ContentAssert.SingleError(
            ValidWith(data => Research(data, "guild").Effects![0]!.Amount = 2), ContentErrorCode.LevelCapMismatch, "buildings[hut]");

    /// <summary>A max level bonus on a type with no level above its base max level is a bad target (<see cref="ContentErrorCode.InvalidEffectTarget"/>).</summary>
    [Fact]
    public void Create_MaxLevelBonusOnATypeWithoutALockedLevel_ReportsInvalidEffectTarget() =>
        ContentAssert.Errors(
            ValidWith(data => Research(data, "guild").Effects![0]!.Building = "lumber_camp"),
            (ContentErrorCode.InvalidEffectTarget, "research[guild].effects[#0].building"));

    /// <summary>A max level bonus on a building type that doesn't exist gives one <see cref="ContentErrorCode.UnknownReference"/>.</summary>
    [Fact]
    public void Create_MaxLevelBonusOnAnUnknownType_ReportsUnknownReference() =>
        ContentAssert.SingleError(
            ValidWith(data => Research(data, "guild").Effects![0]!.Building = "castle"),
            ContentErrorCode.UnknownReference,
            "research[guild].effects[#0].building");

    /// <summary>A level above the base max level that no research opens can never be reached (rule 16, <see cref="ContentErrorCode.BuildingUnreachable"/>).</summary>
    [Fact]
    public void Create_LockedLevelThatNoResearchOpens_ReportsBuildingUnreachable() =>
        ContentAssert.SingleError(
            ValidWith(data => Research(data, "guild").Effects!.RemoveAt(0)), ContentErrorCode.BuildingUnreachable, "buildings[hut].levels[3]");

    /// <summary>An era-1 level opened only by an era-2 node is not reached in its era (rule 16, OD-10: nothing of a later era early).</summary>
    [Fact]
    public void Create_EraOneLevelOpenedInEraTwo_ReportsBuildingUnreachable() =>
        ContentAssert.SingleError(
            ValidWith(data =>
            {
                Research(data, "guild").Era = 2;
                Research(data, "charter").Era = 2;
            }),
            ContentErrorCode.BuildingUnreachable,
            "buildings[hut].levels[3]");
}
