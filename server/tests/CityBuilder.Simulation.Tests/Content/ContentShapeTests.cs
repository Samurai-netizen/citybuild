using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The shape phase: each case breaks one value of the valid fixture and expects exactly one error at that value's path. The case names
/// are those paths.
/// </summary>
public sealed class ContentShapeTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    private static readonly Dictionary<string, Action<GameContentData>> _missingValues = new()
    {
        ["contentVersion"] = data => data.ContentVersion = null,
        ["rules"] = data => data.Rules = null,
        ["startingCity"] = data => data.StartingCity = null,
        ["eras"] = data => data.Eras = null,
        ["items"] = data => data.Items = null,
        ["buildings"] = data => data.Buildings = null,
        ["research"] = data => data.Research = null,
        ["rules.gridWidth"] = data => RulesOf(data).GridWidth = null,
        ["rules.researchSlots"] = data => RulesOf(data).ResearchSlots = null,
        ["rules.baseStorageCaps"] = data => RulesOf(data).BaseStorageCaps = null,
        ["rules.maxOfflineProductionSeconds"] = data => RulesOf(data).MaxOfflineProductionSeconds = null,
        ["rules.minCycleSeconds"] = data => RulesOf(data).MinCycleSeconds = null,
        ["rules.maxLaborReductionPermille"] = data => RulesOf(data).MaxLaborReductionPermille = null,
        ["startingCity.era"] = data => data.StartingCity!.Era = null,
        ["startingCity.inventory"] = data => data.StartingCity!.Inventory = null,
        ["startingCity.inventory.gold"] = data => data.StartingCity!.Inventory!["gold"] = null,
        ["eras[first_era].number"] = data => Era(data, 1).Number = null,
        ["eras[#1].id"] = data => Era(data, 2).Id = null,
        ["eras[first_era].name"] = data => Era(data, 1).Name = "   ",
        ["items[#1]"] = data => data.Items![1] = null,
        ["items[#2].id"] = data => Item(data, "boards").Id = null,
        ["items[wood].name"] = data => Item(data, "wood").Name = null,
        ["items[wood].era"] = data => Item(data, "wood").Era = null,
        ["items[wood].capped"] = data => Item(data, "wood").Capped = null,
        ["items[wood].tradable"] = data => Item(data, "wood").Tradable = null,
        ["buildings[hut].kind"] = data => Building(data, "hut").Kind = null,
        ["buildings[hut].era"] = data => Building(data, "hut").Era = null,
        ["buildings[hut].availableAtStart"] = data => Building(data, "hut").AvailableAtStart = null,
        ["buildings[hut].cost"] = data => Building(data, "hut").Cost = null,
        ["buildings[hut].buildSeconds"] = data => Building(data, "hut").BuildSeconds = null,
        ["buildings[hut].levels"] = data => Building(data, "hut").Levels = null,
        ["buildings[hut].levels[#1]"] = data => Building(data, "hut").Levels![1] = null,
        ["buildings[hut].levels[#1].level"] = data => Level(data, "hut", 2).Level = null,
        ["buildings[hut].levels[2].upgradeCost"] = data => Level(data, "hut", 2).UpgradeCost = null,
        ["buildings[hut].levels[2].upgradeSeconds"] = data => Level(data, "hut", 2).UpgradeSeconds = null,
        ["buildings[lumber_camp].levels[1].recipe.outputs"] = data => Level(data, "lumber_camp", 1).Recipe!.Outputs = null,
        ["buildings[lumber_camp].levels[1].recipe.labor"] = data => Level(data, "lumber_camp", 1).Recipe!.Labor = null,
        ["buildings[workshop].levels[1].recipe.inputs.wood"] = data => Level(data, "workshop", 1).Recipe!.Inputs!["wood"] = null,
        ["research[carpentry].name"] = data => Research(data, "carpentry").Name = string.Empty,
        ["research[carpentry].era"] = data => Research(data, "carpentry").Era = null,
        ["research[carpentry].cost"] = data => Research(data, "carpentry").Cost = null,
        ["research[carpentry].durationSeconds"] = data => Research(data, "carpentry").DurationSeconds = null,
        ["research[carpentry].effects[#0]"] = data => Research(data, "carpentry").Effects![0] = null,
        ["research[carpentry].effects[#0].type"] = data => Research(data, "carpentry").Effects![0]!.Type = null,
        ["research[carpentry].effects[#0].building"] = data => Research(data, "carpentry").Effects![0]!.Building = null,
        ["research[better_saws].prerequisites[#0]"] = data => Research(data, "better_saws").Prerequisites![0] = null,
        ["research[better_saws].effects[#1].permille"] = data => Research(data, "better_saws").Effects![1]!.Permille = null,
        ["research[warehouses].effects[#0].item"] = data => Research(data, "warehouses").Effects![0]!.Item = null,
        ["research[warehouses].effects[#1].amount"] = data => Research(data, "warehouses").Effects![1]!.Amount = null,
    };

    private static readonly Dictionary<string, Action<GameContentData>> _invalidIds = new()
    {
        ["items[Wood].id"] = data => Item(data, "wood").Id = "Wood",
        ["items[1wood].id"] = data => Item(data, "wood").Id = "1wood",
        ["items[_wood].id"] = data => Item(data, "wood").Id = "_wood",
        ["items[wo od].id"] = data => Item(data, "wood").Id = "wo od",
        ["items[wood-2].id"] = data => Item(data, "wood").Id = "wood-2",
        ["items[w\u00F6od].id"] = data => Item(data, "wood").Id = "w\u00F6od",
        ["items[#1].id"] = data => Item(data, "wood").Id = string.Empty,
        ["eras[First].id"] = data => Era(data, 1).Id = "First",
        ["buildings[Hut].id"] = data => Building(data, "hut").Id = "Hut",
        ["research[Carpentry].id"] = data => Research(data, "carpentry").Id = "Carpentry",
        ["buildings[hut].cost.Gold"] = data => Building(data, "hut").Cost = new() { ["Gold"] = 10 },
        ["rules.baseStorageCaps. wood"] = data => RulesOf(data).BaseStorageCaps = new() { [" wood"] = 100, ["boards"] = 80 },
        ["research[better_saws].prerequisites[#0]"] = data => Research(data, "better_saws").Prerequisites = ["Carpentry"],
        ["research[carpentry].effects[#0].building"] = data => Research(data, "carpentry").Effects![0]!.Building = "work shop",
        ["research[warehouses].effects[#0].item"] = data => Research(data, "warehouses").Effects![0]!.Item = "WOOD",
    };

    private static readonly Dictionary<string, Action<GameContentData>> _invalidValues = new()
    {
        ["buildings[hut].kind"] = data => Building(data, "hut").Kind = "farm",
        ["buildings[lumber_camp].kind"] = data => Building(data, "lumber_camp").Kind = "Producer",
        ["research[carpentry].effects[#0].type"] = data => Research(data, "carpentry").Effects![0]!.Type = "unlock",
    };

    private static readonly Dictionary<string, Action<GameContentData>> _unexpectedValues = new()
    {
        ["buildings[hut].levels[1].upgradeCost"] = data => Level(data, "hut", 1).UpgradeCost = new() { ["gold"] = 1 },
        ["buildings[hut].levels[1].upgradeSeconds"] = data => Level(data, "hut", 1).UpgradeSeconds = 3,
        ["research[carpentry].effects[#0].permille"] = data => Research(data, "carpentry").Effects![0]!.Permille = 10,
        ["research[carpentry].effects[#0].amount"] = data => Research(data, "carpentry").Effects![0]!.Amount = 10,
        ["research[better_saws].effects[#0].item"] = data => Research(data, "better_saws").Effects![0]!.Item = "wood",
        ["research[warehouses].effects[#0].building"] = data => Research(data, "warehouses").Effects![0]!.Building = "hut",
        ["research[warehouses].effects[#0].permille"] = data => Research(data, "warehouses").Effects![0]!.Permille = 10,
    };

    private static readonly Dictionary<string, Action<GameContentData>> _valuesTooLarge = new()
    {
        ["rules.maxOfflineProductionSeconds"] = data => RulesOf(data).MaxOfflineProductionSeconds = GameContentFactory.MaxQuantity + 1,
        ["rules.baseStorageCaps.wood"] = data => RulesOf(data).BaseStorageCaps!["wood"] = long.MaxValue,
        ["startingCity.inventory.gold"] = data => data.StartingCity!.Inventory!["gold"] = GameContentFactory.MaxQuantity + 1,
        ["buildings[hut].buildSeconds"] = data => Building(data, "hut").BuildSeconds = GameContentFactory.MaxQuantity + 1,
        ["buildings[hut].levels[2].upgradeSeconds"] = data => Level(data, "hut", 2).UpgradeSeconds = GameContentFactory.MaxQuantity + 1,
        ["buildings[lumber_camp].levels[1].recipe.labor"] = data => Level(data, "lumber_camp", 1).Recipe!.Labor = GameContentFactory.MaxQuantity + 1,
        ["research[carpentry].durationSeconds"] = data => Research(data, "carpentry").DurationSeconds = GameContentFactory.MaxQuantity + 1,
        ["research[warehouses].effects[#0].amount"] = data => Research(data, "warehouses").Effects![0]!.Amount = GameContentFactory.MaxQuantity + 1,
    };

    public static TheoryData<string> MissingValuePaths => new(_missingValues.Keys);

    public static TheoryData<string> InvalidIdPaths => new(_invalidIds.Keys);

    public static TheoryData<string> InvalidValuePaths => new(_invalidValues.Keys);

    public static TheoryData<string> UnexpectedValuePaths => new(_unexpectedValues.Keys);

    public static TheoryData<string> ValueTooLargePaths => new(_valuesTooLarge.Keys);

    [Theory]
    [MemberData(nameof(MissingValuePaths))]
    public void Create_RequiredValueMissing_ReportsMissingValueAtItsPath(string path) =>
        ContentAssert.SingleError(ValidWith(_missingValues[path]), ContentErrorCode.MissingValue, path);

    [Theory]
    [MemberData(nameof(InvalidIdPaths))]
    public void Create_IdTextWithoutTheIdFormat_ReportsInvalidIdAtItsPath(string path) =>
        ContentAssert.SingleError(ValidWith(_invalidIds[path]), ContentErrorCode.InvalidId, path);

    [Theory]
    [MemberData(nameof(InvalidValuePaths))]
    public void Create_TextOutsideItsFixedSet_ReportsInvalidValueAtItsPath(string path) =>
        ContentAssert.SingleError(ValidWith(_invalidValues[path]), ContentErrorCode.InvalidValue, path);

    [Theory]
    [MemberData(nameof(UnexpectedValuePaths))]
    public void Create_ValueWhereNoneIsAllowed_ReportsUnexpectedValueAtItsPath(string path) =>
        ContentAssert.SingleError(ValidWith(_unexpectedValues[path]), ContentErrorCode.UnexpectedValue, path);

    [Theory]
    [MemberData(nameof(ValueTooLargePaths))]
    public void Create_NumberAboveMaxQuantity_ReportsValueTooLargeAtItsPath(string path) =>
        ContentAssert.SingleError(ValidWith(_valuesTooLarge[path]), ContentErrorCode.ValueTooLarge, path);

    [Fact]
    public void Create_NumbersExactlyAtMaxQuantity_AreAccepted()
    {
        GameContentData data = Valid();
        RulesOf(data).MaxOfflineProductionSeconds = GameContentFactory.MaxQuantity;
        data.StartingCity!.Inventory!["gold"] = GameContentFactory.MaxQuantity;

        ContentAssert.Valid(data);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("a_1")]
    [InlineData("z9")]
    [InlineData("abcdefghijklmnopqrstuvwxyz_0123456789_abcdefghijklmnopqrstuvwxyz")] // 64 characters, the longest allowed
    public void Create_IdTextWithTheIdFormat_IsAccepted(string text)
    {
        GameContentData data = Valid();
        Item(data, "gold").Id = text;
        RenameItemKeys(data, "gold", text);

        ContentAssert.Valid(data);
    }

    [Fact]
    public void Create_IdTextOneLongerThanTheLimit_ReportsInvalidId()
    {
        string tooLong = new('a', 65);
        GameContentData data = Valid();
        Era(data, 2).Id = tooLong;

        ContentAssert.SingleError(data, ContentErrorCode.InvalidId, $"eras[{tooLong}].id");
    }

    [Fact]
    public void Create_SeveralShapeErrors_ReportsAllOfThemInFileOrder()
    {
        GameContentData data = Valid();
        Research(data, "warehouses").DurationSeconds = null;
        Item(data, "wood").Capped = null;
        RulesOf(data).GridHeight = null;

        ContentAssert.Errors(
            data,
            (ContentErrorCode.MissingValue, "rules.gridHeight"),
            (ContentErrorCode.MissingValue, "items[wood].capped"),
            (ContentErrorCode.MissingValue, "research[warehouses].durationSeconds"));
    }

    [Fact]
    public void Create_SeveralBadKeysInOneMap_ReportsThemInOrdinalKeyOrder()
    {
        // A dictionary's own order is an implementation detail; errors must not depend on it.
        GameContentData data = Valid();
        Building(data, "hut").Cost = new() { ["Zinc"] = 1, ["Amber"] = 2 };

        ContentAssert.Errors(
            data,
            (ContentErrorCode.InvalidId, "buildings[hut].cost.Amber"),
            (ContentErrorCode.InvalidId, "buildings[hut].cost.Zinc"));
    }

    [Fact]
    public void ToString_OfAnError_GivesCodePathAndMessage()
    {
        GameContentData data = Valid();
        RulesOf(data).GridWidth = null;

        ContentError error = Assert.Single(GameContentFactory.Create(data).Errors);

        Assert.Equal("MissingValue at rules.gridWidth: " + error.Message, error.ToString());
    }

    // Keeps the content valid when an item gets another id: every map key and effect that names it follows.
    private static void RenameItemKeys(GameContentData data, string from, string to)
    {
        IEnumerable<Dictionary<string, long?>?> maps = new[] { data.Rules!.BaseStorageCaps, data.StartingCity!.Inventory }
            .Concat(data.Buildings!.Select(building => building!.Cost))
            .Concat(data.Buildings!.SelectMany(building => building!.Levels!).Select(level => level!.UpgradeCost))
            .Concat(data.Buildings!.SelectMany(building => building!.Levels!).SelectMany(level => new[] { level!.Recipe?.Inputs, level.Recipe?.Outputs }))
            .Concat(data.Research!.Select(node => node!.Cost));
        foreach (Dictionary<string, long?> map in maps.OfType<Dictionary<string, long?>>())
        {
            if (map.Remove(from, out long? amount))
            {
                map[to] = amount;
            }
        }
    }
}
