using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.EngineRules;

/// <summary>
/// The effects of M1-02 in the engine (<c>04</c> sections 5, 6 and 8; <c>25</c> → Effect types): <c>maxLevelBonus</c> raises one type's
/// level cap, <c>builderSlotBonus</c> adds builder slots, <c>grantCharter</c> marks the city chartered. All three are derived from the
/// completed research, so each counts from its node's completion second. Fixture numbers (<see cref="State.CityFixture.TestContent"/>): a
/// hut's base max level is 2 and its level 3 costs 40 gold and 10 wood; two builder slots; the guild node (60 s) adds hut max level +1 and
/// one builder slot; the charter node (30 s) grants the charter.
/// </summary>
public sealed class LevelCapAndBuilderSlotRuleTests
{
    /// <summary>The hut type, whose level 3 needs the guild.</summary>
    private static readonly BuildingTypeId _hut = new("hut");

    /// <summary>Without the guild a hut's max level is its base max level (2); with it, one more.</summary>
    [Fact]
    public void MaxLevel_WithAndWithoutTheBonusNode_IsBaseThenBasePlusBonus()
    {
        Assert.Equal(2, CityQueries.MaxLevel(City(), TestContent, _hut));
        Assert.Equal(3, CityQueries.MaxLevel(City(completedResearch: ["guild"]), TestContent, _hut));
        Assert.Equal(2, CityQueries.MaxLevel(City(completedResearch: ["guild"]), TestContent, new BuildingTypeId("lumber_camp")));
    }

    /// <summary>The builder slots are the content's plus every completed <c>builderSlotBonus</c>.</summary>
    [Fact]
    public void BuilderSlots_WithAndWithoutTheBonusNode_IsRulesThenRulesPlusBonus()
    {
        Assert.Equal(2, CityQueries.BuilderSlots(City(), TestContent));
        Assert.Equal(3, CityQueries.BuilderSlots(City(completedResearch: ["guild"]), TestContent));
    }

    /// <summary>A city is chartered once a node with <c>grantCharter</c> is completed, and not before.</summary>
    [Fact]
    public void IsChartered_WithAndWithoutTheCharterNode_IsFalseThenTrue()
    {
        Assert.False(CityQueries.IsChartered(City(completedResearch: ["guild"]), TestContent));
        Assert.True(CityQueries.IsChartered(City(completedResearch: ["guild", "charter"]), TestContent));
    }

    /// <summary>An upgrade past the base max level is rejected as <see cref="MutationErrorCode.MaxLevel"/> before the guild.</summary>
    [Fact]
    public void UpgradeBuilding_PastTheBaseMaxLevelWithoutTheNode_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.MaxLevel, City(purse: 100, buildings: [Idle(1, "hut", 0, 0, level: 2)]), Upgrade(1));

    /// <summary>After the guild the same upgrade is accepted: it pays the coins and waits for the wood (<c>04</c> §16).</summary>
    [Fact]
    public void UpgradeBuilding_PastTheBaseMaxLevelWithTheNode_PaysAndWaitsForTheGoods()
    {
        CityState city = City(purse: 100, buildings: [Idle(1, "hut", 0, 0, level: 2)], completedResearch: ["guild"]);

        RulesAssert.Moved(Upgrade(1)(city), spent: "gold=40", refunded: "");

        Assert.Equal((2, true), (city.Buildings[0].Level, city.Buildings[0].HasUpgradeWaiting));
    }

    /// <summary>At the last level the bonus allows, an upgrade is rejected again.</summary>
    [Fact]
    public void UpgradeBuilding_AtTheRaisedMaxLevel_IsRejected() =>
        RulesAssert.Rejected(
            MutationErrorCode.MaxLevel, City(purse: 100, buildings: [Idle(1, "hut", 0, 0, level: 3)], completedResearch: ["guild"]), Upgrade(1));

    /// <summary>With two builders busy and the guild done, a third construction is accepted.</summary>
    [Fact]
    public void PlaceBuilding_TwoBuildersBusyAfterTheNode_UsesTheThirdSlot()
    {
        CityState city = City(
            purse: 100, buildings: [Constructing(1, "hut", 0, 0, 900, 2000), Constructing(2, "hut", 1, 0, 900, 2000)], completedResearch: ["guild"]);

        RulesAssert.Ok(CityRules.PlaceBuilding(city, TestContent, _hut, 2, 0));

        Assert.Equal(3, CityQueries.BuildersBusy(city));
    }

    /// <summary>
    /// Mid-advance, the slot is there from the guild's completion second: a placement one second before it is rejected as
    /// <see cref="MutationErrorCode.NoFreeBuilder"/>, and at that second it is accepted.
    /// </summary>
    [Fact]
    public void PlaceBuilding_AtTheGuildsCompletionSecond_IsAcceptedAndOneSecondBeforeRejected()
    {
        CityState city = City(
            clock: 1000,
            purse: 100,
            buildings: [Constructing(1, "hut", 0, 0, 900, 5000), Constructing(2, "hut", 1, 0, 900, 5000)],
            activeResearch: new ActiveResearch(new ResearchId("guild"), 1000, 1060));

        Simulator.Advance(city, TestContent, 59);
        RulesAssert.Rejected(MutationErrorCode.NoFreeBuilder, city, city => CityRules.PlaceBuilding(city, TestContent, _hut, 2, 0));
        SettlementReport report = Simulator.Advance(city, TestContent, 1);

        Assert.Equal(["guild"], report.CompletedResearch.Select(id => id.Value));
        RulesAssert.Ok(CityRules.PlaceBuilding(city, TestContent, _hut, 2, 0));
    }

    /// <summary>Mid-advance, the raised cap is there from the guild's completion second, not before.</summary>
    [Fact]
    public void UpgradeBuilding_AtTheGuildsCompletionSecond_IsAcceptedAndOneSecondBeforeRejected()
    {
        CityState city = City(
            clock: 1000, purse: 100, buildings: [Idle(1, "hut", 0, 0, level: 2)], activeResearch: new ActiveResearch(new ResearchId("guild"), 1000, 1060));

        Simulator.Advance(city, TestContent, 59);
        RulesAssert.Rejected(MutationErrorCode.MaxLevel, city, Upgrade(1));
        Simulator.Advance(city, TestContent, 1);

        RulesAssert.Ok(Upgrade(1)(city));
    }

    /// <summary>The charter counts from its node's completion second, inside an advance that goes on past it.</summary>
    [Fact]
    public void Advance_ThroughTheCharterNodesCompletion_CharteredFromThatSecond()
    {
        CityState city = City(clock: 1000, completedResearch: ["guild"], activeResearch: new ActiveResearch(new ResearchId("charter"), 1000, 1030));

        Simulator.Advance(city, TestContent, 29);
        Assert.False(CityQueries.IsChartered(city, TestContent));
        SettlementReport report = Simulator.Advance(city, TestContent, 100);

        Assert.True(CityQueries.IsChartered(city, TestContent));
        Assert.Equal(["charter"], report.CompletedResearch.Select(id => id.Value));
    }

    /// <summary>The upgrade of building <paramref name="buildingId"/> with the fixture content, as a mutation for the assertions.</summary>
    private static Func<CityState, MutationResult> Upgrade(int buildingId) => city => CityRules.UpgradeBuilding(city, TestContent, buildingId);
}
