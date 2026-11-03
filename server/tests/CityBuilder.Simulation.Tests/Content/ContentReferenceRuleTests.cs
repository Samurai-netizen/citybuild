using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The cross-reference rules: ids are unique, every reference names something that exists, eras are numbered 1..n, exactly one currency used
/// only where goods aren't needed (content rules 4 and 11), stores that hold their recipe's goods and never shrink (rule 9), exactly one
/// science building (rule 10), effects target the right kind of thing, and every locked building type is unlocked by exactly one research
/// node. The case names are the expected error paths.
/// </summary>
public sealed class ContentReferenceRuleTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    /// <summary>Ids and era numbers used twice, and a prerequisite listed twice, by the expected error path (the second use).</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _duplicates = new()
    {
        ["eras[first_era]"] = data => Era(data, 2).Id = "first_era",
        ["eras[second_era].number"] = data =>
        {
            Era(data, 2).Number = 1;
            Research(data, "warehouses").Era = 1;
        },
        ["items[wood]"] = data => data.Items!.Add(Copy(Item(data, "wood"))),
        ["buildings[hut]"] = data => data.Buildings!.Add(Copy(Building(data, "hut"))),
        ["research[carpentry]"] = data => data.Research!.Add(Copy(Research(data, "carpentry"))),
        ["research[better_saws].prerequisites[carpentry]"] = data => Research(data, "better_saws").Prerequisites = ["carpentry", "carpentry"],
        ["amountPresets[all]"] = data => data.AmountPresets!.Add(new AmountPresetData { Id = "all", Name = "Everything", Percent = 100 }),
    };

    /// <summary>Era numberings with a gap, and no eras at all.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _erasNotContiguous = new()
    {
        ["gap"] = data =>
        {
            Era(data, 2).Number = 3;
            Research(data, "warehouses").Era = 3;
        },
        ["no eras"] = data => data.Eras = [],
    };

    /// <summary>Every kind of reference to something the content doesn't define, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _unknownReferences = new()
    {
        ["startingCity.era"] = data => data.StartingCity!.Era = 3,
        ["items[wood].era"] = data => Item(data, "wood").Era = 7,
        ["buildings[hut].era"] = data => Building(data, "hut").Era = 0,
        ["research[warehouses].era"] = data => Research(data, "warehouses").Era = 3,
        ["buildings[depot].levels[2].storage.stone"] = data => Level(data, "depot", 2).Storage!["stone"] = 10,
        ["buildings[hut].cost.stone"] = data => Building(data, "hut").Cost!["stone"] = 1,
        ["buildings[hut].levels[2].upgradeCost.stone"] = data => Level(data, "hut", 2).UpgradeCost!["stone"] = 1,
        ["buildings[workshop].levels[1].recipe.inputs.stone"] = data => Level(data, "workshop", 1).Recipe!.Inputs!["stone"] = 1,
        ["buildings[lumber_camp].levels[2].recipe.outputs.stone"] = data => Level(data, "lumber_camp", 2).Recipe!.Outputs!["stone"] = 1,
        ["research[carpentry].cost.stone"] = data => Research(data, "carpentry").Cost!["stone"] = 1,
        ["research[carpentry].effects[#1].building"] = data =>
            Research(data, "carpentry").Effects!.Add(new ResearchEffectData { Type = "unlockBuilding", Building = "tower" }),
        ["research[better_saws].effects[#0].building"] = data => Research(data, "better_saws").Effects![0]!.Building = "tower",
        ["research[warehouses].effects[#0].item"] = data => Research(data, "warehouses").Effects![0]!.Item = "stone",
        ["research[better_saws].prerequisites[alchemy]"] = data => Research(data, "better_saws").Prerequisites!.Add("alchemy"),
    };

    /// <summary>Each break of the store rules (9 and 4): a store with the currency, a producer's store without a recipe good or below a cycle's quantity, a shrinking store.</summary>
    private static readonly Dictionary<string, (ContentErrorCode Code, Action<GameContentData> Change)> _storeRules = new()
    {
        ["buildings[depot].levels[2].storage.gold"] = (ContentErrorCode.StoreMismatch, data => Level(data, "depot", 2).Storage!["gold"] = 10),
        ["buildings[workshop].levels[1].storage"] = (ContentErrorCode.StoreMismatch, data => Level(data, "workshop", 1).Storage!.Remove("boards")),
        ["buildings[workshop].levels[1].storage.wood"] = (ContentErrorCode.StoreMismatch, data => Level(data, "workshop", 1).Storage!["wood"] = 1),
        ["buildings[depot].levels[2].storage.boards"] = (ContentErrorCode.StoreShrinks, data => Level(data, "depot", 2).Storage!.Remove("boards")),
        ["buildings[depot].levels[2].storage.wood"] = (ContentErrorCode.StoreShrinks, data => Level(data, "depot", 2).Storage!["wood"] = 50),
    };

    /// <summary>
    /// Each break of the currency rules (4 and 11) and the science building rule (10): two currencies, the currency as a recipe input, a road
    /// cost in goods, no science building, two, and one whose <c>maxPerCity</c> isn't 1.
    /// </summary>
    private static readonly Dictionary<string, (ContentErrorCode Code, Action<GameContentData> Change)> _currencyAndScience = new()
    {
        ["items"] = (ContentErrorCode.CurrencyCount, data => data.Items!.Add(new ItemData { Id = "silver", Name = "Silver", Era = 1, Currency = true, Tradable = false })),
        ["buildings[workshop].levels[1].recipe.inputs.gold"] = (ContentErrorCode.InvalidCurrencyUse, data => Level(data, "workshop", 1).Recipe!.Inputs!["gold"] = 1),
        ["roads[1].cost.wood"] = (ContentErrorCode.InvalidCurrencyUse, data => Road(data, 1).Cost!["wood"] = 1),
        ["buildings"] = (ContentErrorCode.ScienceBuildingMismatch, data => data.Buildings!.Remove(Building(data, "school"))),
        ["buildings[school].maxPerCity"] = (ContentErrorCode.ScienceBuildingMismatch, data => Building(data, "school").MaxPerCity = null),
    };

    /// <summary>A labor reduction on a house and on a warehouse, and a storage bonus on the currency.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _invalidEffectTargets = new()
    {
        ["research[better_saws].effects[#0].building"] = data => Research(data, "better_saws").Effects![0]!.Building = "hut",
        ["research[better_saws].effects[#1].building"] = data => Research(data, "better_saws").Effects![1]!.Building = "depot",
        ["research[warehouses].effects[#0].item"] = data => Research(data, "warehouses").Effects![0]!.Item = "gold",
    };

    /// <summary>A locked type that no node unlocks, one unlocked twice, and an unlock of a type available at the start.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _unlockMismatches = new()
    {
        ["buildings[workshop]"] = data => Research(data, "carpentry").Effects = [],
        ["research[better_saws].effects[#2]"] = data =>
            Research(data, "better_saws").Effects!.Add(new ResearchEffectData { Type = "unlockBuilding", Building = "workshop" }),
        ["research[carpentry].effects[#1]"] = data =>
            Research(data, "carpentry").Effects!.Add(new ResearchEffectData { Type = "unlockBuilding", Building = "hut" }),
        ["research[carpentry].effects[#0]"] = data => Building(data, "workshop").AvailableAtStart = true,
    };

    /// <summary>The error paths of <see cref="_duplicates"/>.</summary>
    public static TheoryData<string> DuplicatePaths => new(_duplicates.Keys);

    /// <summary>The case names of <see cref="_erasNotContiguous"/>.</summary>
    public static TheoryData<string> ErasNotContiguousCases => new(_erasNotContiguous.Keys);

    /// <summary>The error paths of <see cref="_unknownReferences"/>.</summary>
    public static TheoryData<string> UnknownReferencePaths => new(_unknownReferences.Keys);

    /// <summary>The error paths of <see cref="_storeRules"/>.</summary>
    public static TheoryData<string> StoreRulePaths => new(_storeRules.Keys);

    /// <summary>The error paths of <see cref="_currencyAndScience"/>.</summary>
    public static TheoryData<string> CurrencyAndSciencePaths => new(_currencyAndScience.Keys);

    /// <summary>The error paths of <see cref="_invalidEffectTargets"/>.</summary>
    public static TheoryData<string> InvalidEffectTargetPaths => new(_invalidEffectTargets.Keys);

    /// <summary>The error paths of <see cref="_unlockMismatches"/>.</summary>
    public static TheoryData<string> UnlockMismatchPaths => new(_unlockMismatches.Keys);

    /// <summary>A repeated id or era number gives one <see cref="ContentErrorCode.DuplicateId"/> at its second use.</summary>
    [Theory]
    [MemberData(nameof(DuplicatePaths))]
    public void Create_IdOrEraNumberUsedTwice_ReportsDuplicateIdAtTheSecondUse(string path) =>
        ContentAssert.SingleError(ValidWith(_duplicates[path]), ContentErrorCode.DuplicateId, path);

    /// <summary>Era numbers that aren't 1..n give one <see cref="ContentErrorCode.ErasNotContiguous"/> for the eras.</summary>
    [Theory]
    [MemberData(nameof(ErasNotContiguousCases))]
    public void Create_EraNumbersNotOneToN_ReportsErasNotContiguous(string eraCase) =>
        ContentAssert.SingleError(ValidWith(_erasNotContiguous[eraCase]), ContentErrorCode.ErasNotContiguous, "eras");

    /// <summary>Each reference to an undefined era, item, building type or research node gives one <see cref="ContentErrorCode.UnknownReference"/>.</summary>
    [Theory]
    [MemberData(nameof(UnknownReferencePaths))]
    public void Create_ReferenceToSomethingNotDefined_ReportsUnknownReference(string path) =>
        ContentAssert.SingleError(ValidWith(_unknownReferences[path]), ContentErrorCode.UnknownReference, path);

    /// <summary>A store that breaks rule 9 or holds the currency gives one <see cref="ContentErrorCode.StoreMismatch"/> or <see cref="ContentErrorCode.StoreShrinks"/>.</summary>
    /// <param name="path">The expected error path.</param>
    [Theory]
    [MemberData(nameof(StoreRulePaths))]
    public void Create_StoreBreakingItsRules_ReportsTheStoreError(string path) =>
        ContentAssert.SingleError(ValidWith(_storeRules[path].Change), _storeRules[path].Code, path);

    /// <summary>A break of the currency or science building rules gives one error with the rule's code.</summary>
    /// <param name="path">The expected error path.</param>
    [Theory]
    [MemberData(nameof(CurrencyAndSciencePaths))]
    public void Create_CurrencyOrScienceBuildingRuleBroken_ReportsItsCode(string path) =>
        ContentAssert.SingleError(ValidWith(_currencyAndScience[path].Change), _currencyAndScience[path].Code, path);

    /// <summary>Two science building types are one <see cref="ContentErrorCode.ScienceBuildingMismatch"/> for the content: research happens at one.</summary>
    [Fact]
    public void Create_TwoScienceBuildingTypes_ReportsScienceBuildingMismatch()
    {
        GameContentData data = ValidWith(data =>
        {
            BuildingData academy = Copy(Building(data, "school"));
            academy.Id = "academy";
            data.Buildings!.Add(academy);
        });

        ContentAssert.SingleError(data, ContentErrorCode.ScienceBuildingMismatch, "buildings");
    }

    /// <summary>A producer whose recipe makes only the currency needs no store: the coins go into the purse (rule 9).</summary>
    [Fact]
    public void Create_ProducerOfTheCurrencyOnlyWithoutAStore_IsAccepted()
    {
        GameContentData data = ValidWith(data => data.Buildings!.Add(new BuildingData
        {
            Id = "mint", Name = "Mint", Kind = "producer", Era = 1, AvailableAtStart = true, Cost = new() { ["gold"] = 1 }, BuildSeconds = 1,
            Levels = [new BuildingLevelData { Level = 1, MaxWorkers = 1, Recipe = new RecipeData { Outputs = new() { ["gold"] = 1 }, Labor = 10 } }],
        }));

        ContentAssert.Valid(data);
    }

    /// <summary>An effect on the wrong kind of target gives one <see cref="ContentErrorCode.InvalidEffectTarget"/>.</summary>
    [Theory]
    [MemberData(nameof(InvalidEffectTargetPaths))]
    public void Create_EffectOnTheWrongKindOfTarget_ReportsInvalidEffectTarget(string path) =>
        ContentAssert.SingleError(ValidWith(_invalidEffectTargets[path]), ContentErrorCode.InvalidEffectTarget, path);

    /// <summary>A locked type that isn't unlocked by exactly one node gives one <see cref="ContentErrorCode.UnlockMismatch"/>.</summary>
    [Theory]
    [MemberData(nameof(UnlockMismatchPaths))]
    public void Create_LockedTypeNotUnlockedExactlyOnce_ReportsUnlockMismatch(string path) =>
        ContentAssert.SingleError(ValidWith(_unlockMismatches[path]), ContentErrorCode.UnlockMismatch, path);

    /// <summary>An era copy repeats both id and number; it is one mistake, so it gives one error, at the id.</summary>
    [Fact]
    public void Create_ExactCopyOfAnEra_ReportsOneDuplicateIdForTheCopy()
    {
        // The copy repeats both the id and the number; it is one mistake, so it gives one error.
        GameContentData data = ValidWith(data => data.Eras!.Add(new EraData { Number = 1, Id = "first_era", Name = "First Era" }));

        ContentAssert.SingleError(data, ContentErrorCode.DuplicateId, "eras[first_era]");
    }

    /// <summary>Ids are unique per kind of definition: an item and a building type may share a text.</summary>
    [Fact]
    public void Create_SameIdForAnItemAndABuildingType_IsAccepted()
    {
        // Ids are unique per kind of definition: an item and a building type are different things even with the same text.
        GameContentData data = Valid();
        data.Items!.Add(new ItemData { Id = "hut", Name = "Hut Token", Era = 1, Currency = false, Tradable = false });

        ContentAssert.Valid(data);
    }

    /// <summary>Two unknown items in one cost are both reported, in ordinal key order, whatever order the map holds them in.</summary>
    [Fact]
    public void Create_TwoUnknownItemsInOneCost_ReportsBothInOrdinalOrder()
    {
        GameContentData data = Valid();
        Building(data, "hut").Cost = new() { ["zinc"] = 1, ["amber"] = 2 };

        ContentAssert.Errors(
            data,
            (ContentErrorCode.UnknownReference, "buildings[hut].cost.amber"),
            (ContentErrorCode.UnknownReference, "buildings[hut].cost.zinc"));
    }

    /// <summary>A second item definition with the same id and values: only the duplicate itself is wrong.</summary>
    private static ItemData Copy(ItemData item) =>
        new() { Id = item.Id, Name = item.Name, Era = item.Era, Currency = item.Currency, Tradable = item.Tradable };

    /// <summary>A second building type definition with the same id and the same (valid) values: only the duplicate itself is wrong.</summary>
    private static BuildingData Copy(BuildingData building) =>
        new()
        {
            Id = building.Id,
            Name = building.Name,
            Kind = building.Kind,
            Era = building.Era,
            AvailableAtStart = building.AvailableAtStart,
            MaxPerCity = building.MaxPerCity,
            Cost = building.Cost,
            BuildSeconds = building.BuildSeconds,
            Levels = building.Levels,
        };

    /// <summary>A second research node definition with the same id and values: only the duplicate itself is wrong.</summary>
    private static ResearchData Copy(ResearchData node) =>
        new()
        {
            Id = node.Id,
            Name = node.Name,
            Era = node.Era,
            Cost = node.Cost,
            DurationSeconds = node.DurationSeconds,
            Prerequisites = node.Prerequisites,
            Effects = node.Effects,
        };
}
