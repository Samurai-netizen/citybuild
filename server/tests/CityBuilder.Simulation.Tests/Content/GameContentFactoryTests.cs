using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>What the factory builds from valid data: every value carried over, sorted lists, lookups and derived values.</summary>
public sealed class GameContentFactoryTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    /// <summary>Null data is a programmer error and throws, unlike a missing value inside the data, which is a reported error.</summary>
    [Fact]
    public void Create_NullData_Throws()
    {
        // The null is deliberate: a missing data object is a programmer error, unlike a missing value inside it.
        Assert.Throws<ArgumentNullException>(() => GameContentFactory.Create(null!));
    }

    /// <summary>Valid data gives content and no errors, with its version carried over.</summary>
    [Fact]
    public void Create_ValidData_ReturnsContentAndNoErrors()
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.Equal("test-1", content.ContentVersion);
    }

    /// <summary>Every global rule is carried over, and the base caps come sorted by item id.</summary>
    [Fact]
    public void Create_ValidData_CarriesOverTheRules()
    {
        GameRules rules = ContentAssert.Valid(Valid()).Rules;

        Assert.Equal((6, 5, 2, 1), (rules.GridWidth, rules.GridHeight, rules.BuilderSlots, rules.ResearchSlots));
        Assert.Equal((3600L, 5L, 400), (rules.MaxOfflineProductionSeconds, rules.MinCycleSeconds, rules.MaxLaborReductionPermille));
        Assert.Equal([("boards", 80L), ("wood", 100L)], rules.BaseStorageCaps.Select(cap => (cap.Item.Value, cap.Quantity)));
    }

    /// <summary>A capped item has a base cap and an uncapped one has none.</summary>
    [Fact]
    public void TryGetBaseStorageCap_CappedAndUncappedItems_FindsOnlyTheCapped()
    {
        GameRules rules = ContentAssert.Valid(Valid()).Rules;

        Assert.True(rules.TryGetBaseStorageCap(new ItemId("wood"), out long woodCap));
        Assert.Equal(100L, woodCap);
        Assert.False(rules.TryGetBaseStorageCap(new ItemId("gold"), out _));
    }

    /// <summary>The starting era and stock are carried over, the stock sorted by item id.</summary>
    [Fact]
    public void Create_ValidData_CarriesOverTheStartingCity()
    {
        StartingCityDefinition startingCity = ContentAssert.Valid(Valid()).StartingCity;

        Assert.Equal(1, startingCity.Era);
        Assert.Equal([("gold", 100L), ("wood", 5L)], startingCity.Inventory.Select(stock => (stock.Item.Value, stock.Quantity)));
    }

    /// <summary>Every era and item value is carried over.</summary>
    [Fact]
    public void Create_ValidData_CarriesOverErasAndItems()
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.Equal([(1, "first_era", "First Era"), (2, "second_era", "Second Era")], content.Eras.Select(era => (era.Number, era.Id.Value, era.Name)));
        Assert.True(content.TryGetItem(new ItemId("wood"), out ItemDefinition? wood));
        Assert.Equal(("Wood", 1, true, true), (wood.Name, wood.Era, wood.IsCapped, wood.IsTradable));
        Assert.True(content.TryGetItem(new ItemId("gold"), out ItemDefinition? gold));
        Assert.Equal((false, false), (gold.IsCapped, gold.IsTradable));
    }

    /// <summary>Items, building types and research nodes are sorted by id whatever order the file lists them in.</summary>
    [Fact]
    public void Create_ListsInAnyFileOrder_SortsItemsBuildingsAndResearchById()
    {
        GameContentData data = Valid();
        data.Items!.Reverse();
        data.Buildings!.Reverse();
        data.Research!.Reverse();

        GameContent content = ContentAssert.Valid(data);

        Assert.Equal(["boards", "gold", "wood"], content.Items.Select(item => item.Id.Value));
        Assert.Equal(["hut", "lumber_camp", "workshop"], content.Buildings.Select(building => building.Id.Value));
        Assert.Equal(["better_saws", "carpentry", "warehouses"], content.Research.Select(node => node.Id.Value));
    }

    /// <summary>Eras are sorted by number whatever order the file lists them in.</summary>
    [Fact]
    public void Create_ErasInAnyFileOrder_SortsErasByNumber()
    {
        GameContentData data = Valid();
        data.Eras!.Reverse();

        Assert.Equal([1, 2], ContentAssert.Valid(data).Eras.Select(era => era.Number));
    }

    /// <summary>Each lookup finds exactly the defined definitions: an unknown, differently cased or other-kind id finds nothing.</summary>
    [Fact]
    public void Lookups_KnownAndUnknownIds_FindExactlyTheDefinedOnes()
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.True(content.TryGetEra(2, out EraDefinition? second) && second.Id == new EraId("second_era"));
        Assert.False(content.TryGetEra(3, out _));
        Assert.True(content.TryGetBuilding(new BuildingTypeId("workshop"), out BuildingDefinition? workshop) && workshop.Name == "Workshop");
        Assert.False(content.TryGetBuilding(new BuildingTypeId("gold"), out _));
        Assert.True(content.TryGetResearch(new ResearchId("carpentry"), out ResearchDefinition? carpentry) && carpentry.Era == 1);
        Assert.False(content.TryGetResearch(new ResearchId("Carpentry"), out _));
        Assert.False(content.TryGetItem(new ItemId("hut"), out _));
    }

    /// <summary>The lines of an item map (here a cost) come sorted by item id.</summary>
    [Fact]
    public void Create_ItemMapInAnyOrder_SortsItsLinesByItemId()
    {
        GameContentData data = Valid();
        Building(data, "workshop").Cost = new() { ["wood"] = 10, ["gold"] = 40 };

        BuildingDefinition workshop = BuildingOf(ContentAssert.Valid(data), "workshop");

        Assert.Equal([("gold", 40L), ("wood", 10L)], workshop.Cost.Select(line => (line.Item.Value, line.Quantity)));
    }

    /// <summary>Every building value and its recipe are carried over.</summary>
    [Fact]
    public void Create_ValidData_CarriesOverTheBuildingValues()
    {
        BuildingDefinition workshop = BuildingOf(ContentAssert.Valid(Valid()), "workshop");

        Assert.Equal(("Workshop", BuildingKind.Producer, 1, 20L, false), (workshop.Name, workshop.Kind, workshop.Era, workshop.BuildSeconds, workshop.IsAvailableAtStart));
        RecipeDefinition recipe = Assert.IsType<RecipeDefinition>(workshop.GetLevel(1).Recipe);
        Assert.Equal([("wood", 2L)], recipe.Inputs.Select(line => (line.Item.Value, line.Quantity)));
        Assert.Equal([("boards", 1L)], recipe.Outputs.Select(line => (line.Item.Value, line.Quantity)));
        Assert.Equal(90L, recipe.Labor);
    }

    /// <summary>A locked type's <c>UnlockedByResearch</c> is the node whose effect names it; a type available at the start has none.</summary>
    [Fact]
    public void Create_LockedBuilding_IsUnlockedByTheResearchWhoseEffectNamesIt()
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.Equal(new ResearchId("carpentry"), BuildingOf(content, "workshop").UnlockedByResearch);
        Assert.Null(BuildingOf(content, "hut").UnlockedByResearch);
        Assert.True(BuildingOf(content, "hut").IsAvailableAtStart);
    }

    /// <summary>Levels are sorted by number whatever their file order, and each is found by <c>GetLevel</c>.</summary>
    [Fact]
    public void Create_LevelsInAnyFileOrder_AreSortedAndReachableByNumber()
    {
        GameContentData data = Valid();
        Building(data, "hut").Levels!.Reverse();

        BuildingDefinition hut = BuildingOf(ContentAssert.Valid(data), "hut");

        Assert.Equal(2, hut.MaxLevel);
        Assert.Equal([1, 2], hut.Levels.Select(level => level.Level));
        Assert.Equal(5, hut.GetLevel(2).WorkersProvided);
        Assert.Equal([("gold", 20L), ("wood", 5L)], hut.GetLevel(2).UpgradeCost.Select(line => (line.Item.Value, line.Quantity)));
        Assert.Equal(8L, hut.GetLevel(2).UpgradeSeconds);
    }

    /// <summary>A level number outside 1..max is a programmer error and throws.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void GetLevel_NumberOutsideOneToMaxLevel_Throws(int level)
    {
        BuildingDefinition hut = BuildingOf(ContentAssert.Valid(Valid()), "hut");

        Assert.Throws<ArgumentOutOfRangeException>(() => hut.GetLevel(level));
    }

    /// <summary>Optional values left out become 0, null or empty: a house's slots and recipe, level 1's upgrade, missing inputs and prerequisites.</summary>
    [Fact]
    public void Create_LeftOutOptionalValues_BecomeZeroNoneOrEmpty()
    {
        GameContent content = ContentAssert.Valid(Valid());
        BuildingLevelDefinition hutLevel = BuildingOf(content, "hut").GetLevel(1);
        BuildingLevelDefinition campLevel = BuildingOf(content, "lumber_camp").GetLevel(1);

        Assert.Equal((0, 3), (hutLevel.MaxWorkers, hutLevel.WorkersProvided));
        Assert.Null(hutLevel.Recipe);
        Assert.Empty(hutLevel.UpgradeCost);
        Assert.Equal(0L, hutLevel.UpgradeSeconds);
        Assert.Equal(0, campLevel.WorkersProvided);
        Assert.Empty(Assert.IsType<RecipeDefinition>(campLevel.Recipe).Inputs);
        Assert.Empty(ResearchOf(content, "carpentry").Prerequisites);
    }

    /// <summary>Every research value is carried over.</summary>
    [Fact]
    public void Create_ValidData_CarriesOverTheResearchValues()
    {
        ResearchDefinition betterSaws = ResearchOf(ContentAssert.Valid(Valid()), "better_saws");

        Assert.Equal(("Better Saws", 1, 200L), (betterSaws.Name, betterSaws.Era, betterSaws.DurationSeconds));
        Assert.Equal([("boards", 5L), ("gold", 60L)], betterSaws.Cost.Select(line => (line.Item.Value, line.Quantity)));
        Assert.Equal([new ResearchId("carpentry")], betterSaws.Prerequisites);
    }

    /// <summary>Several prerequisites come sorted by id.</summary>
    [Fact]
    public void Create_SeveralPrerequisites_AreSortedById()
    {
        GameContentData data = Valid();
        Research(data, "warehouses").Prerequisites = ["carpentry", "better_saws"];

        Assert.Equal(["better_saws", "carpentry"], ResearchOf(ContentAssert.Valid(data), "warehouses").Prerequisites.Select(id => id.Value));
    }

    /// <summary>Each effect keeps its type and values, and effects keep the file's order.</summary>
    [Fact]
    public void Create_Effects_KeepTheirTypeTheirValuesAndTheFileOrder()
    {
        GameContent content = ContentAssert.Valid(Valid());

        UnlockBuildingEffect unlock = Assert.IsType<UnlockBuildingEffect>(Assert.Single(ResearchOf(content, "carpentry").Effects));
        Assert.Equal(new BuildingTypeId("workshop"), unlock.Building);
        Assert.Collection(
            ResearchOf(content, "better_saws").Effects,
            effect => Assert.Equal(("lumber_camp", 250), LaborReduction(effect)),
            effect => Assert.Equal(("workshop", 100), LaborReduction(effect)));
        Assert.Collection(
            ResearchOf(content, "warehouses").Effects,
            effect => Assert.Equal(("wood", 50L), StorageCapBonus(effect)),
            effect => Assert.Equal(("boards", 40L), StorageCapBonus(effect)));
    }

    /// <summary>The content's lists are read-only even through a cast: content can't change after it is made.</summary>
    [Fact]
    public void ContentLists_CannotBeChangedThroughACast()
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.False(content.Items is ItemDefinition[]);
        Assert.True(((ICollection<ItemDefinition>)content.Items).IsReadOnly);
        Assert.True(((ICollection<ItemQuantity>)content.Rules.BaseStorageCaps).IsReadOnly);
        Assert.True(((ICollection<BuildingLevelDefinition>)BuildingOf(content, "hut").Levels).IsReadOnly);
    }

    /// <summary>The factory only reads the data: sorting the levels doesn't reorder the caller's list.</summary>
    [Fact]
    public void Create_ValidData_LeavesTheDataUnchanged()
    {
        GameContentData data = Valid();
        Building(data, "hut").Levels!.Reverse();

        ContentAssert.Valid(data);

        Assert.Equal([2, 1], Building(data, "hut").Levels!.Select(level => level!.Level));
    }

    /// <summary>A building type of the content by id; throws when it doesn't exist.</summary>
    private static BuildingDefinition BuildingOf(GameContent content, string id) =>
        content.TryGetBuilding(new BuildingTypeId(id), out BuildingDefinition? building) ? building : throw new InvalidOperationException(id);

    /// <summary>A research node of the content by id; throws when it doesn't exist.</summary>
    private static ResearchDefinition ResearchOf(GameContent content, string id) =>
        content.TryGetResearch(new ResearchId(id), out ResearchDefinition? node) ? node : throw new InvalidOperationException(id);

    /// <summary>An effect as (building, permille), asserting that it is a labor reduction.</summary>
    private static (string Building, int Permille) LaborReduction(ResearchEffect effect)
    {
        LaborReductionEffect reduction = Assert.IsType<LaborReductionEffect>(effect);
        return (reduction.Building.Value, reduction.Permille);
    }

    /// <summary>An effect as (item, amount), asserting that it is a storage cap bonus.</summary>
    private static (string Item, long Amount) StorageCapBonus(ResearchEffect effect)
    {
        StorageCapBonusEffect bonus = Assert.IsType<StorageCapBonusEffect>(effect);
        return (bonus.Item.Value, bonus.Amount);
    }
}
