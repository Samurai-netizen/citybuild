using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The cross-reference rules: ids are unique, every reference names something that exists, eras are numbered 1..n, storage caps match the
/// capped items, effects target the right kind of thing, and every locked building type is unlocked by exactly one research node. The
/// case names are the expected error paths.
/// </summary>
public sealed class ContentReferenceRuleTests
{
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
    };

    private static readonly Dictionary<string, Action<GameContentData>> _erasNotContiguous = new()
    {
        ["gap"] = data =>
        {
            Era(data, 2).Number = 3;
            Research(data, "warehouses").Era = 3;
        },
        ["no eras"] = data => data.Eras = [],
    };

    private static readonly Dictionary<string, Action<GameContentData>> _unknownReferences = new()
    {
        ["startingCity.era"] = data => data.StartingCity!.Era = 3,
        ["items[wood].era"] = data => Item(data, "wood").Era = 7,
        ["buildings[hut].era"] = data => Building(data, "hut").Era = 0,
        ["research[warehouses].era"] = data => Research(data, "warehouses").Era = 3,
        ["rules.baseStorageCaps.stone"] = data => RulesOf(data).BaseStorageCaps!["stone"] = 10,
        ["startingCity.inventory.stone"] = data => data.StartingCity!.Inventory!["stone"] = 1,
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

    private static readonly Dictionary<string, Action<GameContentData>> _storageCapMismatches = new()
    {
        ["rules.baseStorageCaps.boards"] = data => RulesOf(data).BaseStorageCaps!.Remove("boards"),
        ["rules.baseStorageCaps.gold"] = data => RulesOf(data).BaseStorageCaps!["gold"] = 10,
    };

    private static readonly Dictionary<string, Action<GameContentData>> _invalidEffectTargets = new()
    {
        ["research[better_saws].effects[#0].building"] = data => Research(data, "better_saws").Effects![0]!.Building = "hut",
        ["research[warehouses].effects[#0].item"] = data => Research(data, "warehouses").Effects![0]!.Item = "gold",
    };

    private static readonly Dictionary<string, Action<GameContentData>> _unlockMismatches = new()
    {
        ["buildings[workshop]"] = data => Research(data, "carpentry").Effects = [],
        ["research[better_saws].effects[#2]"] = data =>
            Research(data, "better_saws").Effects!.Add(new ResearchEffectData { Type = "unlockBuilding", Building = "workshop" }),
        ["research[carpentry].effects[#1]"] = data =>
            Research(data, "carpentry").Effects!.Add(new ResearchEffectData { Type = "unlockBuilding", Building = "hut" }),
        ["research[carpentry].effects[#0]"] = data => Building(data, "workshop").AvailableAtStart = true,
    };

    public static TheoryData<string> DuplicatePaths => new(_duplicates.Keys);

    public static TheoryData<string> ErasNotContiguousCases => new(_erasNotContiguous.Keys);

    public static TheoryData<string> UnknownReferencePaths => new(_unknownReferences.Keys);

    public static TheoryData<string> StorageCapMismatchPaths => new(_storageCapMismatches.Keys);

    public static TheoryData<string> InvalidEffectTargetPaths => new(_invalidEffectTargets.Keys);

    public static TheoryData<string> UnlockMismatchPaths => new(_unlockMismatches.Keys);

    [Theory]
    [MemberData(nameof(DuplicatePaths))]
    public void Create_IdOrEraNumberUsedTwice_ReportsDuplicateIdAtTheSecondUse(string path) =>
        ContentAssert.SingleError(ValidWith(_duplicates[path]), ContentErrorCode.DuplicateId, path);

    [Theory]
    [MemberData(nameof(ErasNotContiguousCases))]
    public void Create_EraNumbersNotOneToN_ReportsErasNotContiguous(string eraCase) =>
        ContentAssert.SingleError(ValidWith(_erasNotContiguous[eraCase]), ContentErrorCode.ErasNotContiguous, "eras");

    [Theory]
    [MemberData(nameof(UnknownReferencePaths))]
    public void Create_ReferenceToSomethingNotDefined_ReportsUnknownReference(string path) =>
        ContentAssert.SingleError(ValidWith(_unknownReferences[path]), ContentErrorCode.UnknownReference, path);

    [Theory]
    [MemberData(nameof(StorageCapMismatchPaths))]
    public void Create_BaseCapsNotMatchingTheCappedItems_ReportsStorageCapMismatch(string path) =>
        ContentAssert.SingleError(ValidWith(_storageCapMismatches[path]), ContentErrorCode.StorageCapMismatch, path);

    [Theory]
    [MemberData(nameof(InvalidEffectTargetPaths))]
    public void Create_EffectOnTheWrongKindOfTarget_ReportsInvalidEffectTarget(string path) =>
        ContentAssert.SingleError(ValidWith(_invalidEffectTargets[path]), ContentErrorCode.InvalidEffectTarget, path);

    [Theory]
    [MemberData(nameof(UnlockMismatchPaths))]
    public void Create_LockedTypeNotUnlockedExactlyOnce_ReportsUnlockMismatch(string path) =>
        ContentAssert.SingleError(ValidWith(_unlockMismatches[path]), ContentErrorCode.UnlockMismatch, path);

    [Fact]
    public void Create_SameIdForAnItemAndABuildingType_IsAccepted()
    {
        // Ids are unique per kind of definition: an item and a building type are different things even with the same text.
        GameContentData data = Valid();
        data.Items!.Add(new ItemData { Id = "hut", Name = "Hut Token", Era = 1, Capped = false, Tradable = false });

        ContentAssert.Valid(data);
    }

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

    private static ItemData Copy(ItemData item) =>
        new() { Id = item.Id, Name = item.Name, Era = item.Era, Capped = item.Capped, Tradable = item.Tradable };

    // A second definition with the same id and the same (valid) values: only the duplicate itself is wrong.
    private static BuildingData Copy(BuildingData building) =>
        new()
        {
            Id = building.Id,
            Name = building.Name,
            Kind = building.Kind,
            Era = building.Era,
            AvailableAtStart = building.AvailableAtStart,
            Cost = building.Cost,
            BuildSeconds = building.BuildSeconds,
            Levels = building.Levels,
        };

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
