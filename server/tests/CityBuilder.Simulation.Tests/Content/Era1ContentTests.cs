using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The server's Era I content, <c>server/content/era1-content.json</c> (content version <c>era1-1</c>, M1-02), against
/// <c>core-instructions/28-era1-content.md</c>: it loads and passes every content rule, every recipe input has a producer and every good a
/// use, each chain step runs at the ratio <c>28</c> states (rates read from the content, never written here), every level 3 waits for the
/// research <c>28</c> names, and the last research, the Town Charter, completes the era's tree.
/// </summary>
public sealed class Era1ContentTests
{
    /// <summary>The content under test, read and checked once (the test fails with every error if it is not valid).</summary>
    private static readonly GameContent _content = ContentAssert.Valid(ContentFile.ReadServer("era1-content.json"));

    /// <summary>The file loads as a valid content with the version <c>era1-1</c>, one era, 8 goods and the currency, 13 building types and 16 nodes.</summary>
    [Fact]
    public void Load_Era1Content_IsValidWithTheCountsOf28()
    {
        Assert.Equal("era1-1", _content.ContentVersion);
        Assert.Single(_content.Eras);
        Assert.Equal(8, _content.Items.Count);
        Assert.Equal(13, _content.Buildings.Count);
        Assert.Equal(16, _content.Research.Count);
    }

    /// <summary>Every recipe input is the output of some other building type's recipe.</summary>
    [Fact]
    public void Recipes_EveryInput_HasAProducer()
    {
        HashSet<ItemId> made = Recipes().SelectMany(recipe => recipe.Recipe.Outputs).Select(output => output.Item).ToHashSet();

        Assert.Empty(Recipes().SelectMany(recipe => recipe.Recipe.Inputs).Select(input => input.Item).Where(item => !made.Contains(item)).Distinct());
    }

    /// <summary>Every good (every item but the currency) is a recipe input or in a cost of a building, an upgrade or a node.</summary>
    [Fact]
    public void Items_EveryGood_HasAUse()
    {
        HashSet<ItemId> used = Recipes().SelectMany(recipe => recipe.Recipe.Inputs)
            .Concat(_content.Buildings.SelectMany(building => building.Cost.Concat(building.Levels.SelectMany(level => level.UpgradeCost))))
            .Concat(_content.Research.SelectMany(node => node.Cost))
            .Select(amount => amount.Item)
            .ToHashSet();

        Assert.Empty(_content.Items.Where(item => !item.IsCurrency && !used.Contains(item.Id)).Select(item => item.Id));
    }

    /// <summary>
    /// The chain steps of <c>28</c> → Recipes at level 1 with every worker: one Woodcutter's Hut feeds one Sawmill, one Quarry one Stonemason,
    /// one Farm one Windmill, one Windmill one Bakery (1 : 1), and two Sawmills one Trading Post.
    /// </summary>
    [Theory]
    [InlineData("woodcutter", "sawmill", "logs", 1, 1)]
    [InlineData("quarry", "stonemason", "stone", 1, 1)]
    [InlineData("farm", "windmill", "grain", 1, 1)]
    [InlineData("windmill", "bakery", "flour", 1, 1)]
    [InlineData("sawmill", "trading_post", "planks", 2, 1)]
    public void Recipes_ChainStepAtLevel1_RunsAtTheRatioOf28(string producer, string consumer, string item, int producers, int consumers)
    {
        long made = PerHourAtLevel1(producer, item, output: true);
        long eaten = PerHourAtLevel1(consumer, item, output: false);

        Assert.Equal(eaten * consumers, made * producers);
    }

    /// <summary>
    /// Every type's base max level and the node that raises it to level 3, as <c>28</c> → Buildings and levels says; the Town Hall has its
    /// level 3 from the start and the Scriptorium has one level.
    /// </summary>
    [Theory]
    [InlineData("house", 2, "timber_framing")]
    [InlineData("woodcutter", 2, "timber_framing")]
    [InlineData("sawmill", 2, "timber_framing")]
    [InlineData("farm", 2, "timber_framing")]
    [InlineData("quarry", 2, "masonry")]
    [InlineData("stonemason", 2, "masonry")]
    [InlineData("windmill", 2, "masonry")]
    [InlineData("bakery", 2, "masonry")]
    [InlineData("trading_post", 2, "masonry")]
    [InlineData("courier_station", 2, "masonry")]
    [InlineData("warehouse", 2, "masonry")]
    [InlineData("town_hall", 3, null)]
    [InlineData("scriptorium", 1, null)]
    public void Buildings_LevelCap_IsRaisedByTheNodeOf28(string type, int baseMaxLevel, string? raisedBy)
    {
        Assert.True(_content.TryGetBuilding(new BuildingTypeId(type), out BuildingDefinition? building));
        Assert.Equal(baseMaxLevel, building.BaseMaxLevel);

        IEnumerable<string> raisers = _content.Research
            .Where(node => node.Effects.OfType<MaxLevelBonusEffect>().Any(bonus => bonus.Building == building.Id))
            .Select(node => node.Id.Value);
        Assert.Equal(raisedBy is null ? [] : [raisedBy], raisers);
    }

    /// <summary>
    /// The Town Charter requires every other node of the era (directly), no node requires it, and it is the one node that grants the charter:
    /// completing it completes the era's tree.
    /// </summary>
    [Fact]
    public void Research_TownCharter_CompletesTheEra1Tree()
    {
        ResearchId charter = new("town_charter");
        Assert.True(_content.TryGetResearch(charter, out ResearchDefinition? node));

        Assert.Equal(_content.Research.Where(other => other.Id != charter).Select(other => other.Id).Order(), node.Prerequisites.Order());
        Assert.DoesNotContain(_content.Research, other => other.Prerequisites.Contains(charter));
        Assert.Equal([charter], _content.Research.Where(other => other.Effects.OfType<GrantCharterEffect>().Any()).Select(other => other.Id));
    }

    /// <summary>
    /// The landmark is built in stages through the existing project rules (<c>04</c> §16): ordering the Town Hall's level 2 pays its 900 coins
    /// at once and leaves an upgrade waiting for the stage's goods, bread included; the building keeps its level until they are in.
    /// </summary>
    [Fact]
    public void UpgradeBuilding_TownHallAtLevel1_WaitsForTheSecondStagesGoods()
    {
        CityState city = City(purse: 1000, completedResearch: ["masonry"], buildings: [Idle(1, "town_hall", 3, 3)]);

        RulesAssert.Ok(CityRules.UpgradeBuilding(city, _content, 1));

        Assert.Equal(100, city.Purse);
        Assert.True(city.TryGetBuilding(1, out Building? townHall));
        Assert.Equal(1, townHall!.Level);
        Assert.Equal(
            [("bread", 40L), ("cut_stone", 60L), ("planks", 80L)],
            townHall.Needs.Select(need => (need.Key.Value, need.Value)).OrderBy(need => need.Item1, StringComparer.Ordinal));
    }

    /// <summary>Every recipe of the content, with its building type.</summary>
    private static IEnumerable<(BuildingTypeId Type, RecipeDefinition Recipe)> Recipes() =>
        _content.Buildings.SelectMany(building => building.Levels
            .Where(level => level.Recipe is not null)
            .Select(level => (building.Id, level.Recipe!)));

    /// <summary>
    /// How many of <paramref name="item"/> the type's level 1 makes (or eats) per hour with every worker and no research: quantity × max
    /// workers × 3600 / labor, which is exact for the chains of <c>28</c>.
    /// </summary>
    private static long PerHourAtLevel1(string type, string item, bool output)
    {
        Assert.True(_content.TryGetBuilding(new BuildingTypeId(type), out BuildingDefinition? building));
        BuildingLevelDefinition level = building.GetLevel(1);
        RecipeDefinition recipe = Assert.IsType<RecipeDefinition>(level.Recipe);
        long quantity = Assert.Single(output ? recipe.Outputs : recipe.Inputs, amount => amount.Item == new ItemId(item)).Quantity;
        Assert.Equal(0, quantity * level.MaxWorkers * 3600 % recipe.Labor);
        return quantity * level.MaxWorkers * 3600 / recipe.Labor;
    }
}
