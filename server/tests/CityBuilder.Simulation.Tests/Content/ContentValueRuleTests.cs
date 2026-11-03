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
        ["rules.tripCarryLimit"] = data => RulesOf(data).TripCarryLimit = 0,
        ["rules.maxRouteStops"] = data => RulesOf(data).MaxRouteStops = 0,
        ["rules.maxStopActions"] = data => RulesOf(data).MaxStopActions = -1,
        ["buildings[depot].levels[1].storage.wood"] = data => Level(data, "depot", 1).Storage!["wood"] = 0,
        ["buildings[hut].maxPerCity"] = data => Building(data, "hut").MaxPerCity = 0,
        ["amountPresets[half].percent"] = data => Preset(data, "half").Percent = 0,
        ["research[boots].effects[#0].permille"] = data => Research(data, "boots").Effects![0]!.Permille = 0,
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

    /// <summary>Costs, the purse and the reduction limit set below 0, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _negative = new()
    {
        ["rules.maxLaborReductionPermille"] = data => RulesOf(data).MaxLaborReductionPermille = -1,
        ["startingCity.purse"] = data => data.StartingCity!.Purse = -5,
        ["buildings[workshop].cost.wood"] = data => Building(data, "workshop").Cost!["wood"] = -10,
        ["buildings[hut].levels[2].upgradeCost.gold"] = data => Level(data, "hut", 2).UpgradeCost!["gold"] = -1,
        ["research[better_saws].cost.boards"] = data => Research(data, "better_saws").Cost!["boards"] = -1,
    };

    /// <summary>Two research slots, permille above 1000, and a grid or a worker count past <see cref="GameContentFactory.MaxStoredSmallValue"/>, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _tooLarge = new()
    {
        ["rules.researchSlots"] = data => RulesOf(data).ResearchSlots = 2,
        ["rules.gridWidth"] = data => RulesOf(data).GridWidth = GameContentFactory.MaxStoredSmallValue + 1,
        ["rules.gridHeight"] = data => RulesOf(data).GridHeight = int.MaxValue,
        ["buildings[hut].levels[2].workersProvided"] = data => Level(data, "hut", 2).WorkersProvided = GameContentFactory.MaxStoredSmallValue + 1,
        ["buildings[lumber_camp].levels[2].maxWorkers"] = data =>
        {
            // The labor grows with the workers, so the only broken rule is the limit: not the shortest cycle.
            Level(data, "lumber_camp", 2).MaxWorkers = GameContentFactory.MaxStoredSmallValue + 1;
            Level(data, "lumber_camp", 2).Recipe!.Labor = GameContentFactory.MaxQuantity;
        },
        ["rules.maxLaborReductionPermille"] = data => RulesOf(data).MaxLaborReductionPermille = 1001,
        ["research[better_saws].effects[#0].permille"] = data => Research(data, "better_saws").Effects![0]!.Permille = 1001,
        ["rules.maxRouteStops"] = data => RulesOf(data).MaxRouteStops = 17,
        ["rules.maxStopActions"] = data => RulesOf(data).MaxStopActions = 9,
        ["amountPresets[all].percent"] = data => Preset(data, "all").Percent = 101,
        ["research[boots].effects[#0].permille"] = data => Research(data, "boots").Effects![0]!.Permille = 1001,
    };

    /// <summary>The error paths of <see cref="_notPositive"/>.</summary>
    public static TheoryData<string> NotPositivePaths => new(_notPositive.Keys);

    /// <summary>The error paths of <see cref="_negative"/>.</summary>
    public static TheoryData<string> NegativePaths => new(_negative.Keys);

    /// <summary>The error paths of <see cref="_tooLarge"/>.</summary>
    public static TheoryData<string> TooLargePaths => new(_tooLarge.Keys);

    /// <summary>A quantity, duration, store limit, slot count, percentage or limit of 0 or less gives one <see cref="ContentErrorCode.NotPositive"/>.</summary>
    [Theory]
    [MemberData(nameof(NotPositivePaths))]
    public void Create_ValueThatMustBePositiveIsZeroOrLess_ReportsNotPositive(string path) =>
        ContentAssert.SingleError(ValidWith(_notPositive[path]), ContentErrorCode.NotPositive, path);

    /// <summary>A cost, stock or limit below 0 gives one <see cref="ContentErrorCode.Negative"/>.</summary>
    [Theory]
    [MemberData(nameof(NegativePaths))]
    public void Create_CostStockOrLimitBelowZero_ReportsNegative(string path) =>
        ContentAssert.SingleError(ValidWith(_negative[path]), ContentErrorCode.Negative, path);

    /// <summary>More than one research slot, permille above 1000, or a grid or a worker count too big to store, gives one <see cref="ContentErrorCode.ValueTooLarge"/>.</summary>
    [Theory]
    [MemberData(nameof(TooLargePaths))]
    public void Create_SlotsOrPermilleAboveTheirLimit_ReportsValueTooLarge(string path) =>
        ContentAssert.SingleError(ValidWith(_tooLarge[path]), ContentErrorCode.ValueTooLarge, path);

    /// <summary>A grid and a worker count of exactly the limit are allowed: the limit is the largest value a <c>smallint</c> column holds.</summary>
    [Fact]
    public void Create_GridAndWorkersAtTheLimit_AreAccepted()
    {
        GameContentData data = Valid();
        RulesOf(data).GridWidth = GameContentFactory.MaxStoredSmallValue;
        RulesOf(data).GridHeight = GameContentFactory.MaxStoredSmallValue;
        Level(data, "hut", 2).WorkersProvided = GameContentFactory.MaxStoredSmallValue;

        Assert.True(GameContentFactory.Create(data).IsValid);
    }

    /// <summary>A route of one stop goes nowhere: <c>maxRouteStops</c> 1 is <see cref="ContentErrorCode.ValueTooSmall"/>; 2 and the API's 16 are allowed.</summary>
    [Fact]
    public void Create_MaxRouteStopsOfOne_ReportsValueTooSmall()
    {
        ContentAssert.SingleError(ValidWith(data => RulesOf(data).MaxRouteStops = 1), ContentErrorCode.ValueTooSmall, "rules.maxRouteStops");
        ContentAssert.Valid(ValidWith(data => RulesOf(data).MaxRouteStops = 2));
        ContentAssert.Valid(ValidWith(data => RulesOf(data).MaxRouteStops = 16));
        ContentAssert.Valid(ValidWith(data => RulesOf(data).MaxStopActions = 8));
    }

    /// <summary>No amount preset, or more than 8, is <see cref="ContentErrorCode.InvalidAmountPresets"/>; 1 and 8 are allowed (content rule 13).</summary>
    [Fact]
    public void Create_NoOrNinePresets_ReportsInvalidAmountPresets()
    {
        // A preset of the n-th percent, for lists of a given length.
        static AmountPresetData PresetOf(int percent) => new() { Id = "p" + percent, Name = "P" + percent, Percent = percent };

        ContentAssert.SingleError(ValidWith(data => data.AmountPresets = []), ContentErrorCode.InvalidAmountPresets, "amountPresets");
        ContentAssert.SingleError(
            ValidWith(data => data.AmountPresets = [.. Enumerable.Range(1, 9).Select(PresetOf)]), ContentErrorCode.InvalidAmountPresets, "amountPresets");
        ContentAssert.Valid(ValidWith(data => data.AmountPresets = [PresetOf(100)]));
        ContentAssert.Valid(ValidWith(data => data.AmountPresets = [.. Enumerable.Range(1, 8).Select(PresetOf)]));
    }

    /// <summary>Zero costs, an empty purse and a limit of 1000 ‰ are allowed.</summary>
    [Fact]
    public void Create_ZeroCostsAndStock_AreAccepted()
    {
        GameContentData data = Valid();
        Building(data, "hut").Cost = [];
        Building(data, "workshop").Cost!["wood"] = 0;
        Research(data, "carpentry").Cost!["gold"] = 0;
        data.StartingCity!.Purse = 0;
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
