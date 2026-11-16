using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The graph rules (M0-70, <c>04</c> §14 rules 14 to 17): every good that is used is made by some recipe (the currency may come from the
/// starting purse instead); every good is used somewhere; a level's recipe is never worse than the one below and upgrade costs never fall;
/// and, playing each era from the start, every building type, level and research node of the era can be reached. They run only when no
/// other rule found an error. In the fixture, wood comes from the lumber camp, boards from the workshop (locked behind carpentry), the
/// hut provides the workers, the school runs the research; boards pay for better_saws and the post's level 2.
/// </summary>
public sealed class ContentGraphRuleTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    /// <summary>A good in a cost that no recipe makes gives <see cref="ContentErrorCode.ItemNeverProduced"/>, at the item, naming the cost.</summary>
    [Fact]
    public void Create_CostWithAGoodNoRecipeMakes_ReportsItemNeverProduced()
    {
        GameContentData data = ValidWith(data =>
        {
            data.Items!.Add(new ItemData { Id = "nails", Name = "Nails", Era = 1, Currency = false, Tradable = true });
            Level(data, "post", 2).UpgradeCost!["nails"] = 2;
        });

        ContentAssert.SingleError(data, ContentErrorCode.ItemNeverProduced, "items[nails]");
        Assert.Contains("buildings[post].levels[2].upgradeCost", Assert.Single(GameContentFactory.Create(data).Errors).Message);
    }

    /// <summary>A recipe input that no recipe makes gives <see cref="ContentErrorCode.ItemNeverProduced"/>.</summary>
    [Fact]
    public void Create_RecipeInputNoRecipeMakes_ReportsItemNeverProduced()
    {
        GameContentData data = ValidWith(data =>
        {
            data.Items!.Add(new ItemData { Id = "nails", Name = "Nails", Era = 1, Currency = false, Tradable = true });
            Level(data, "workshop", 1).Recipe!.Inputs!["nails"] = 1;
            Level(data, "workshop", 1).Storage!["nails"] = 5;
        });

        ContentAssert.SingleError(data, ContentErrorCode.ItemNeverProduced, "items[nails]");
        Assert.Contains("buildings[workshop].levels[1].recipe.inputs", Assert.Single(GameContentFactory.Create(data).Errors).Message);
    }

    /// <summary>The currency in costs with no starting purse and no recipe that makes it can never be paid.</summary>
    [Fact]
    public void Create_CurrencyWithoutPurseOrRecipe_ReportsItemNeverProduced()
    {
        GameContentData data = ValidWith(data => data.StartingCity!.Purse = 0);

        ContentAssert.SingleError(data, ContentErrorCode.ItemNeverProduced, "items[gold]");
    }

    /// <summary>A good that nothing uses gives <see cref="ContentErrorCode.ItemNeverUsed"/>.</summary>
    [Fact]
    public void Create_GoodNothingUses_ReportsItemNeverUsed()
    {
        GameContentData data = ValidWith(data => data.Items!.Add(new ItemData { Id = "stone", Name = "Stone", Era = 1, Currency = false, Tradable = true }));

        ContentAssert.SingleError(data, ContentErrorCode.ItemNeverUsed, "items[stone]");
    }

    /// <summary>A good that is made and stored but neither consumed by a recipe nor spent in a cost is unused too.</summary>
    [Fact]
    public void Create_GoodOnlyProduced_ReportsItemNeverUsed()
    {
        GameContentData data = ValidWith(data =>
        {
            Research(data, "better_saws").Cost!.Remove("boards");
            Level(data, "post", 2).UpgradeCost!.Remove("boards");
        });

        ContentAssert.SingleError(data, ContentErrorCode.ItemNeverUsed, "items[boards]");
    }

    /// <summary>A level whose recipe makes a good more slowly (more labor for the same output) gives <see cref="ContentErrorCode.RecipeWorse"/>.</summary>
    [Fact]
    public void Create_LevelMakingLessPerHour_ReportsRecipeWorse()
    {
        // Level 1: 1 wood per 40 labor with 2 workers; level 2: 1 wood per 80 labor with 3 workers, 3/80 < 2/40.
        GameContentData data = ValidWith(data => Level(data, "lumber_camp", 2).Recipe!.Labor = 80);

        ContentAssert.SingleError(data, ContentErrorCode.RecipeWorse, "buildings[lumber_camp].levels[2].recipe");
    }

    /// <summary>A level that needs more of an input for each output gives <see cref="ContentErrorCode.RecipeWorse"/>.</summary>
    [Fact]
    public void Create_LevelNeedingMoreInputPerOutput_ReportsRecipeWorse()
    {
        GameContentData data = ValidWith(data => Building(data, "workshop").Levels!.Add(WorkshopLevel2(inputWood: 3, outputBoards: 1, labor: 90)));

        ContentAssert.SingleError(data, ContentErrorCode.RecipeWorse, "buildings[workshop].levels[2].recipe");
    }

    /// <summary>A level with more labor but more output per cycle, at the same input per output, is better and accepted.</summary>
    [Fact]
    public void Create_LevelMakingMorePerHour_IsAccepted()
    {
        GameContentData data = ValidWith(data => Building(data, "workshop").Levels!.Add(WorkshopLevel2(inputWood: 4, outputBoards: 2, labor: 135)));

        ContentAssert.Valid(data);
    }

    /// <summary>An upgrade that costs less of the currency than the build gives <see cref="ContentErrorCode.UpgradeCostDecrease"/>.</summary>
    [Fact]
    public void Create_UpgradeCheaperThanTheBuild_ReportsUpgradeCostDecrease()
    {
        GameContentData data = ValidWith(data => Level(data, "hut", 2).UpgradeCost!["gold"] = 5);

        ContentAssert.SingleError(data, ContentErrorCode.UpgradeCostDecrease, "buildings[hut].levels[2].upgradeCost");
    }

    /// <summary>An upgrade with fewer goods than the upgrade below gives <see cref="ContentErrorCode.UpgradeCostDecrease"/>.</summary>
    [Fact]
    public void Create_UpgradeWithFewerGoodsThanTheOneBelow_ReportsUpgradeCostDecrease()
    {
        GameContentData data = ValidWith(data => Building(data, "post").Levels!.Add(new BuildingLevelData
        {
            Level = 3, MaxWorkers = 3, CarryLimit = 8, StaminaSeconds = 1200, UpgradeCost = new() { ["gold"] = 40, ["boards"] = 2 }, UpgradeSeconds = 12,
        }));

        ContentAssert.SingleError(data, ContentErrorCode.UpgradeCostDecrease, "buildings[post].levels[3].upgradeCost");
    }

    /// <summary>
    /// A building whose cost needs its own output can never be built (<see cref="ContentErrorCode.BuildingUnreachable"/>); what needs that
    /// output can't be reached either. Each era's building types and levels come first, then its research nodes.
    /// </summary>
    [Fact]
    public void Create_BuildingCostNeedingItsOwnOutput_ReportsBuildingUnreachableAndWhatDependsOnIt()
    {
        GameContentData data = ValidWith(data => Building(data, "workshop").Cost!["boards"] = 1);

        ContentAssert.Errors(
            data,
            (ContentErrorCode.BuildingUnreachable, "buildings[workshop]"),
            (ContentErrorCode.BuildingUnreachable, "buildings[post].levels[2]"),
            (ContentErrorCode.ResearchUnreachable, "research[better_saws]"),
            (ContentErrorCode.ResearchUnreachable, "research[warehouses]"));
    }

    /// <summary>A research node that costs a good only its own unlock makes can never start (<see cref="ContentErrorCode.ResearchUnreachable"/>).</summary>
    [Fact]
    public void Create_ResearchCostNeedingWhatItUnlocks_ReportsResearchUnreachable()
    {
        GameContentData data = ValidWith(data => Research(data, "carpentry").Cost!["boards"] = 5);

        ContentAssert.Errors(
            data,
            (ContentErrorCode.BuildingUnreachable, "buildings[workshop]"),
            (ContentErrorCode.BuildingUnreachable, "buildings[post].levels[2]"),
            (ContentErrorCode.ResearchUnreachable, "research[carpentry]"),
            (ContentErrorCode.ResearchUnreachable, "research[better_saws]"),
            (ContentErrorCode.ResearchUnreachable, "research[tallying]"),
            (ContentErrorCode.ResearchUnreachable, "research[warehouses]"));
    }

    /// <summary>
    /// Producers only run with workers: a house that costs wood can't be built, so no recipe runs and nothing that needs a good is reached.
    /// A building type that can't be built reports no error for its levels.
    /// </summary>
    [Fact]
    public void Create_HouseCostingAGoodOnlyWorkersMake_ReportsEverythingThatNeedsGoods()
    {
        GameContentData data = ValidWith(data => Building(data, "hut").Cost!["wood"] = 5);

        ContentAssert.Errors(
            data,
            (ContentErrorCode.BuildingUnreachable, "buildings[hut]"),
            (ContentErrorCode.BuildingUnreachable, "buildings[workshop]"),
            (ContentErrorCode.BuildingUnreachable, "buildings[post].levels[2]"),
            (ContentErrorCode.ResearchUnreachable, "research[better_saws]"),
            (ContentErrorCode.ResearchUnreachable, "research[boots]"),
            (ContentErrorCode.ResearchUnreachable, "research[warehouses]"));
    }

    /// <summary>
    /// An era-1 building type unlocked only by an era-2 node is not reachable in its era; an era's research can't use a node of an earlier era
    /// that was never reached.
    /// </summary>
    [Fact]
    public void Create_EraOneBuildingUnlockedInEraTwo_ReportsBuildingUnreachable()
    {
        GameContentData data = ValidWith(data =>
        {
            Research(data, "carpentry").Era = 2;
            Research(data, "better_saws").Prerequisites = null;
            Research(data, "tallying").Prerequisites = null;
        });

        ContentAssert.Errors(
            data,
            (ContentErrorCode.BuildingUnreachable, "buildings[workshop]"),
            (ContentErrorCode.BuildingUnreachable, "buildings[post].levels[2]"),
            (ContentErrorCode.ResearchUnreachable, "research[better_saws]"),
            (ContentErrorCode.ResearchUnreachable, "research[warehouses]"));
    }

    /// <summary>The graph rules wait for the other rules: with a rule error, an unused good is not reported as well.</summary>
    [Fact]
    public void Create_WithAnotherRuleError_SkipsTheGraphRules()
    {
        GameContentData data = ValidWith(data =>
        {
            data.Items!.Add(new ItemData { Id = "stone", Name = "Stone", Era = 1, Currency = false, Tradable = true });
            Level(data, "hut", 2).WorkersProvided = 2;
        });

        ContentAssert.SingleError(data, ContentErrorCode.WorkersDecrease, "buildings[hut].levels[2].workersProvided");
    }

    /// <summary>A level 2 of the workshop with the given recipe; everything else stays at least level 1's.</summary>
    private static BuildingLevelData WorkshopLevel2(long inputWood, long outputBoards, long labor) => new()
    {
        Level = 2, MaxWorkers = 2, UpgradeCost = new() { ["gold"] = 50, ["wood"] = 10 }, UpgradeSeconds = 10,
        Recipe = new RecipeData { Inputs = new() { ["wood"] = inputWood }, Outputs = new() { ["boards"] = outputBoards }, Labor = labor },
        Storage = new() { ["wood"] = 20, ["boards"] = 80 },
    };
}
