using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The walking and road rules of content (<c>core-instructions/04-simulation-model.md</c>, section 14, rules 11 and 12): the ground time,
/// the crossing speed, the shortest walk and the walk bonus limit; road levels numbered 1, 2, … and each faster than what it improves. Each
/// case breaks one value of the valid fixture (ground 4 000 ms, crossings at 250 ‰, a 2 000 ms path and a 1 000 ms paved path).
/// </summary>
public sealed class ContentWalkRuleTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    /// <summary>The fixture's walking rules and road levels pass, and the content keeps them, road levels by number.</summary>
    [Fact]
    public void Create_ValidWalkingRulesAndRoads_KeepsThemInOrder()
    {
        GameContentData data = Valid();
        data.Roads!.Reverse();

        GameContent content = ContentAssert.Valid(data);

        Assert.Equal((4000L, 250, 2L, 300), (content.Rules.GroundTileMillis, content.Rules.CrossingSpeedPermille, content.Rules.MinWalkSeconds, content.Rules.MaxWalkBonusPermille));
        Assert.Equal([1, 2], content.RoadLevels.Select(level => level.Level));
        Assert.Equal(2, content.MaxRoadLevel);
        RoadLevelDefinition paved = content.GetRoadLevel(2);
        Assert.Equal(("Paved path", 1000L), (paved.Name, paved.TileMillis));
        Assert.Equal([("gold", 6L)], paved.Cost.Select(amount => (amount.Item.Value, amount.Quantity)));
    }

    /// <summary>A road level outside 1 to the last level is a programmer error, not a rule refusal.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void GetRoadLevel_OutsideTheLevels_Throws(int level)
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.Throws<ArgumentOutOfRangeException>(() => content.GetRoadLevel(level));
    }

    /// <summary>Each walking rule and the road list are required.</summary>
    [Theory]
    [InlineData("rules.groundTileMillis")]
    [InlineData("rules.crossingSpeedPermille")]
    [InlineData("rules.minWalkSeconds")]
    [InlineData("rules.maxWalkBonusPermille")]
    [InlineData("roads")]
    [InlineData("roads[1].tileMillis")]
    [InlineData("roads[1].cost")]
    [InlineData("roads[1].name")]
    public void Create_WalkingValueMissing_ReportsMissingValue(string path)
    {
        GameContentData data = Valid();
        switch (path)
        {
            case "rules.groundTileMillis": RulesOf(data).GroundTileMillis = null; break;
            case "rules.crossingSpeedPermille": RulesOf(data).CrossingSpeedPermille = null; break;
            case "rules.minWalkSeconds": RulesOf(data).MinWalkSeconds = null; break;
            case "rules.maxWalkBonusPermille": RulesOf(data).MaxWalkBonusPermille = null; break;
            case "roads": data.Roads = null; break;
            case "roads[1].tileMillis": Road(data, 1).TileMillis = null; break;
            case "roads[1].cost": Road(data, 1).Cost = null; break;
            default: Road(data, 1).Name = " "; break;
        }

        ContentAssert.SingleError(data, ContentErrorCode.MissingValue, path);
    }

    /// <summary>The ground time, the crossing speed, the shortest walk and a road's tile time must be greater than 0.</summary>
    [Theory]
    [InlineData("rules.crossingSpeedPermille")]
    [InlineData("rules.minWalkSeconds")]
    [InlineData("roads[2].tileMillis")]
    public void Create_ValueNotPositive_ReportsNotPositive(string path)
    {
        GameContentData data = Valid();
        switch (path)
        {
            case "rules.crossingSpeedPermille": RulesOf(data).CrossingSpeedPermille = 0; break;
            case "rules.minWalkSeconds": RulesOf(data).MinWalkSeconds = 0; break;
            default: Road(data, 2).TileMillis = 0; break;
        }

        ContentAssert.SingleError(data, ContentErrorCode.NotPositive, path);
    }

    /// <summary>A ground time of 0 is reported once; the road levels and the shortest walk are not checked against it.</summary>
    [Fact]
    public void Create_GroundTimeZero_ReportsNotPositiveAndRoadsNotFaster()
    {
        GameContentData data = Valid();
        RulesOf(data).GroundTileMillis = 0;

        ContentAssert.Errors(
            data,
            (ContentErrorCode.NotPositive, "rules.groundTileMillis"),
            (ContentErrorCode.RoadNotFaster, "roads[1].tileMillis"));
    }

    /// <summary>The crossing speed is at most 1000 ‰ (a crossing is never faster than ground), the walk bonus limit at most 900 ‰.</summary>
    [Theory]
    [InlineData("rules.crossingSpeedPermille", 1001)]
    [InlineData("rules.maxWalkBonusPermille", 901)]
    public void Create_PermilleAboveItsLimit_ReportsValueTooLarge(string path, int value)
    {
        GameContentData data = Valid();
        if (path == "rules.crossingSpeedPermille")
        {
            RulesOf(data).CrossingSpeedPermille = value;
        }
        else
        {
            RulesOf(data).MaxWalkBonusPermille = value;
        }

        ContentAssert.SingleError(data, ContentErrorCode.ValueTooLarge, path);
    }

    /// <summary>The limits themselves are allowed: crossings at the ground speed, a walk bonus limit of 900 ‰ and of 0 ‰.</summary>
    [Theory]
    [InlineData(1000, 900)]
    [InlineData(1, 0)]
    public void Create_PermilleAtItsLimits_IsValid(int crossing, int maxBonus)
    {
        GameContentData data = Valid();
        RulesOf(data).CrossingSpeedPermille = crossing;
        RulesOf(data).MaxWalkBonusPermille = maxBonus;

        ContentAssert.Valid(data);
    }

    /// <summary>A negative walk bonus limit and a negative road cost are reported.</summary>
    [Theory]
    [InlineData("rules.maxWalkBonusPermille")]
    [InlineData("roads[1].cost.gold")]
    public void Create_ValueBelowZero_ReportsNegative(string path)
    {
        GameContentData data = Valid();
        if (path == "rules.maxWalkBonusPermille")
        {
            RulesOf(data).MaxWalkBonusPermille = -1;
        }
        else
        {
            Road(data, 1).Cost!["gold"] = -2;
        }

        ContentAssert.SingleError(data, ContentErrorCode.Negative, path);
    }

    /// <summary>
    /// The shortest walk, one step between adjacent buildings (both ground: 4 000 + 4 000 double milliseconds, 4 s), must take at least
    /// <c>minWalkSeconds</c>: 4 is allowed, 5 is not.
    /// </summary>
    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void Create_ShortestWalkAgainstMinWalkSeconds_IsCheckedAtTheLimit(long minWalkSeconds, bool valid)
    {
        GameContentData data = Valid();
        RulesOf(data).MinWalkSeconds = minWalkSeconds;

        if (valid)
        {
            ContentAssert.Valid(data);
        }
        else
        {
            ContentAssert.SingleError(data, ContentErrorCode.WalkTooShort, "rules.groundTileMillis");
        }
    }

    /// <summary>A road level must be faster than the ground (level 1) and than the level before it; equal is not faster.</summary>
    [Theory]
    [InlineData(1, 4000L)]
    [InlineData(1, 9000L)]
    [InlineData(2, 2000L)]
    [InlineData(2, 2500L)]
    public void Create_RoadLevelNotFaster_ReportsRoadNotFaster(int level, long tileMillis)
    {
        GameContentData data = Valid();
        Road(data, level).TileMillis = tileMillis;

        ContentAssert.SingleError(data, ContentErrorCode.RoadNotFaster, $"roads[{level}].tileMillis");
    }

    /// <summary>Road levels must be numbered 1, 2, … each once: a gap, a repeat and an empty list are each one error on <c>roads</c>.</summary>
    [Theory]
    [InlineData("gap")]
    [InlineData("repeat")]
    [InlineData("empty")]
    public void Create_RoadLevelsNotContiguous_ReportsLevelsNotContiguous(string fault)
    {
        GameContentData data = Valid();
        switch (fault)
        {
            case "gap": Road(data, 2).Level = 3; break;
            case "repeat": data.Roads![1]!.Level = 1; break;
            default: data.Roads!.Clear(); break;
        }

        ContentAssert.SingleError(data, ContentErrorCode.LevelsNotContiguous, "roads");
    }

    /// <summary>A road cost names only items the content defines.</summary>
    [Fact]
    public void Create_RoadCostOfAnUnknownItem_ReportsUnknownReference()
    {
        GameContentData data = Valid();
        Road(data, 2).Cost!["stone"] = 1;

        ContentAssert.SingleError(data, ContentErrorCode.UnknownReference, "roads[2].cost.stone");
    }
}
