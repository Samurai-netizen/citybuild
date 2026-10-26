using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The value rules: quantities, durations, caps and slots are greater than zero, costs and stock are at least zero, permille stays within
/// 1000 and M0 has one research slot. Each case breaks one value of the valid fixture; the case names are the expected error paths.
/// </summary>
public sealed class ContentValueRuleTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    /// <summary>Values that must be greater than 0, each set to 0 or below, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _notPositive = new()
    {
        ["rules.gridWidth"] = data => RulesOf(data).GridWidth = 0,
        ["rules.gridHeight"] = data => RulesOf(data).GridHeight = -3,
        ["rules.builderSlots"] = data => RulesOf(data).BuilderSlots = 0,
        ["rules.researchSlots"] = data => RulesOf(data).ResearchSlots = 0,
        ["rules.maxOfflineProductionSeconds"] = data => RulesOf(data).MaxOfflineProductionSeconds = 0,
        ["rules.minCycleSeconds"] = data => RulesOf(data).MinCycleSeconds = 0,
        ["rules.baseStorageCaps.wood"] = data => RulesOf(data).BaseStorageCaps!["wood"] = 0,
        ["buildings[hut].buildSeconds"] = data => Building(data, "hut").BuildSeconds = 0,
        ["buildings[hut].levels[2].upgradeSeconds"] = data => Level(data, "hut", 2).UpgradeSeconds = 0,
        ["buildings[workshop].levels[1].recipe.inputs.wood"] = data => Level(data, "workshop", 1).Recipe!.Inputs!["wood"] = 0,
        ["buildings[workshop].levels[1].recipe.outputs.boards"] = data => Level(data, "workshop", 1).Recipe!.Outputs!["boards"] = -1,
        ["buildings[lumber_camp].levels[2].recipe.labor"] = data => Level(data, "lumber_camp", 2).Recipe!.Labor = 0,
        ["research[carpentry].durationSeconds"] = data => Research(data, "carpentry").DurationSeconds = 0,
        ["research[better_saws].effects[#0].permille"] = data => Research(data, "better_saws").Effects![0]!.Permille = 0,
        ["research[better_saws].effects[#1].permille"] = data => Research(data, "better_saws").Effects![1]!.Permille = -100,
        ["research[warehouses].effects[#1].amount"] = data => Research(data, "warehouses").Effects![1]!.Amount = 0,
    };

    /// <summary>Costs, stock and the reduction limit set below 0, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _negative = new()
    {
        ["rules.maxLaborReductionPermille"] = data => RulesOf(data).MaxLaborReductionPermille = -1,
        ["startingCity.inventory.wood"] = data => data.StartingCity!.Inventory!["wood"] = -5,
        ["buildings[workshop].cost.wood"] = data => Building(data, "workshop").Cost!["wood"] = -10,
        ["buildings[hut].levels[2].upgradeCost.gold"] = data => Level(data, "hut", 2).UpgradeCost!["gold"] = -1,
        ["research[better_saws].cost.boards"] = data => Research(data, "better_saws").Cost!["boards"] = -1,
    };

    /// <summary>Two research slots and permille above 1000, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _tooLarge = new()
    {
        ["rules.researchSlots"] = data => RulesOf(data).ResearchSlots = 2,
        ["rules.maxLaborReductionPermille"] = data => RulesOf(data).MaxLaborReductionPermille = 1001,
        ["research[better_saws].effects[#0].permille"] = data => Research(data, "better_saws").Effects![0]!.Permille = 1001,
    };

    /// <summary>The error paths of <see cref="_notPositive"/>.</summary>
    public static TheoryData<string> NotPositivePaths => new(_notPositive.Keys);

    /// <summary>The error paths of <see cref="_negative"/>.</summary>
    public static TheoryData<string> NegativePaths => new(_negative.Keys);

    /// <summary>The error paths of <see cref="_tooLarge"/>.</summary>
    public static TheoryData<string> TooLargePaths => new(_tooLarge.Keys);

    /// <summary>A quantity, duration, cap or slot count of 0 or less gives one <see cref="ContentErrorCode.NotPositive"/>.</summary>
    [Theory]
    [MemberData(nameof(NotPositivePaths))]
    public void Create_ValueThatMustBePositiveIsZeroOrLess_ReportsNotPositive(string path) =>
        ContentAssert.SingleError(ValidWith(_notPositive[path]), ContentErrorCode.NotPositive, path);

    /// <summary>A cost, stock or limit below 0 gives one <see cref="ContentErrorCode.Negative"/>.</summary>
    [Theory]
    [MemberData(nameof(NegativePaths))]
    public void Create_CostStockOrLimitBelowZero_ReportsNegative(string path) =>
        ContentAssert.SingleError(ValidWith(_negative[path]), ContentErrorCode.Negative, path);

    /// <summary>More than one research slot, or permille above 1000, gives one <see cref="ContentErrorCode.ValueTooLarge"/>.</summary>
    [Theory]
    [MemberData(nameof(TooLargePaths))]
    public void Create_SlotsOrPermilleAboveTheirLimit_ReportsValueTooLarge(string path) =>
        ContentAssert.SingleError(ValidWith(_tooLarge[path]), ContentErrorCode.ValueTooLarge, path);

    /// <summary>Zero costs, zero stock and a limit of 1000 ‰ are allowed.</summary>
    [Fact]
    public void Create_ZeroCostsAndStock_AreAccepted()
    {
        GameContentData data = Valid();
        Building(data, "hut").Cost = [];
        Building(data, "workshop").Cost!["wood"] = 0;
        Research(data, "carpentry").Cost!["gold"] = 0;
        data.StartingCity!.Inventory!["wood"] = 0;
        RulesOf(data).MaxLaborReductionPermille = 1000;

        ContentAssert.Valid(data);
    }

    /// <summary>The rules run only on content of the right shape, so a shape error hides the rule errors.</summary>
    [Fact]
    public void Create_ShapeErrorAndRuleError_ReportsOnlyTheShapeError()
    {
        // The rules run only on content of the right shape (decision 6 of the M0-07a build log).
        GameContentData data = Valid();
        Building(data, "hut").BuildSeconds = 0;
        Item(data, "wood").Name = null;

        ContentAssert.Errors(data, (ContentErrorCode.MissingValue, "items[wood].name"));
    }

    /// <summary>Several rule errors are all reported, in the order the rules run.</summary>
    [Fact]
    public void Create_SeveralRuleErrors_ReportsAllOfThem()
    {
        GameContentData data = Valid();
        Research(data, "carpentry").DurationSeconds = 0;
        Building(data, "hut").BuildSeconds = 0;
        RulesOf(data).GridWidth = 0;

        ContentAssert.Errors(
            data,
            (ContentErrorCode.NotPositive, "rules.gridWidth"),
            (ContentErrorCode.NotPositive, "buildings[hut].buildSeconds"),
            (ContentErrorCode.NotPositive, "research[carpentry].durationSeconds"));
    }
}
