using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The building rules: levels are 1..max, houses provide workers and have no recipe, producers have worker slots and a recipe with outputs,
/// and no level has fewer workers than the one below it (an upgrade keeps the assigned workers). The case names are the error paths.
/// </summary>
public sealed class ContentBuildingRuleTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    /// <summary>Ways to break the hut's level numbering: a gap, no level 1, level 1 twice, no levels at all.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _levelsNotContiguous = new()
    {
        ["gap"] = data => Level(data, "hut", 2).Level = 3,
        ["no level 1"] = data => Building(data, "hut").Levels!.RemoveAt(0),
        ["level 1 twice"] = data =>
        {
            BuildingLevelData second = Level(data, "hut", 2);
            (second.Level, second.UpgradeCost, second.UpgradeSeconds) = (1, null, null);
        },
        ["no levels"] = data => Building(data, "hut").Levels = [],
    };

    /// <summary>House levels without workers provided, or with worker slots or a recipe, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _invalidHouseLevels = new()
    {
        ["buildings[hut].levels[1].workersProvided"] = data => Level(data, "hut", 1).WorkersProvided = null,
        ["buildings[hut].levels[2].workersProvided"] = data => Level(data, "hut", 2).WorkersProvided = -5,
        ["buildings[hut].levels[1].maxWorkers"] = data => Level(data, "hut", 1).MaxWorkers = 2,
        ["buildings[hut].levels[2].recipe"] = data =>
            Level(data, "hut", 2).Recipe = new RecipeData { Outputs = new() { ["wood"] = 1 }, Labor = 30 },
    };

    /// <summary>Producer levels without slots or recipe, with workers provided, or with a recipe that makes nothing, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _invalidProducerLevels = new()
    {
        ["buildings[lumber_camp].levels[1].maxWorkers"] = data => Level(data, "lumber_camp", 1).MaxWorkers = 0,
        ["buildings[workshop].levels[1].maxWorkers"] = data => Level(data, "workshop", 1).MaxWorkers = null,
        ["buildings[lumber_camp].levels[2].workersProvided"] = data => Level(data, "lumber_camp", 2).WorkersProvided = 2,
        ["buildings[lumber_camp].levels[2].recipe"] = data => Level(data, "lumber_camp", 2).Recipe = null,
        ["buildings[workshop].levels[1].recipe.outputs"] = data => Level(data, "workshop", 1).Recipe!.Outputs = [],
    };

    /// <summary>Levels with fewer workers than the level below, for a producer and for a house, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _workersDecrease = new()
    {
        ["buildings[lumber_camp].levels[2].maxWorkers"] = data => Level(data, "lumber_camp", 2).MaxWorkers = 1,
        ["buildings[hut].levels[2].workersProvided"] = data => Level(data, "hut", 2).WorkersProvided = 2,
    };

    /// <summary>The case names of <see cref="_levelsNotContiguous"/>.</summary>
    public static TheoryData<string> LevelsNotContiguousCases => new(_levelsNotContiguous.Keys);

    /// <summary>The error paths of <see cref="_invalidHouseLevels"/>.</summary>
    public static TheoryData<string> InvalidHouseLevelPaths => new(_invalidHouseLevels.Keys);

    /// <summary>The error paths of <see cref="_invalidProducerLevels"/>.</summary>
    public static TheoryData<string> InvalidProducerLevelPaths => new(_invalidProducerLevels.Keys);

    /// <summary>The error paths of <see cref="_workersDecrease"/>.</summary>
    public static TheoryData<string> WorkersDecreasePaths => new(_workersDecrease.Keys);

    /// <summary>Levels that aren't exactly 1..max give one <see cref="ContentErrorCode.LevelsNotContiguous"/> for the building's levels.</summary>
    [Theory]
    [MemberData(nameof(LevelsNotContiguousCases))]
    public void Create_LevelNumbersNotOneToMax_ReportsLevelsNotContiguous(string levelCase) =>
        ContentAssert.SingleError(ValidWith(_levelsNotContiguous[levelCase]), ContentErrorCode.LevelsNotContiguous, "buildings[hut].levels");

    /// <summary>Each wrong house level gives one <see cref="ContentErrorCode.InvalidHouseLevel"/> at the wrong value.</summary>
    [Theory]
    [MemberData(nameof(InvalidHouseLevelPaths))]
    public void Create_HouseLevelWithoutWorkersOrWithSlotsOrRecipe_ReportsInvalidHouseLevel(string path) =>
        ContentAssert.SingleError(ValidWith(_invalidHouseLevels[path]), ContentErrorCode.InvalidHouseLevel, path);

    /// <summary>Each wrong producer level gives one <see cref="ContentErrorCode.InvalidProducerLevel"/> at the wrong value.</summary>
    [Theory]
    [MemberData(nameof(InvalidProducerLevelPaths))]
    public void Create_ProducerLevelWithoutSlotsOrRecipeOrWithWorkers_ReportsInvalidProducerLevel(string path) =>
        ContentAssert.SingleError(ValidWith(_invalidProducerLevels[path]), ContentErrorCode.InvalidProducerLevel, path);

    /// <summary>A level with fewer workers than the one below gives <see cref="ContentErrorCode.WorkersDecrease"/>, since an upgrade keeps the assigned workers.</summary>
    [Theory]
    [MemberData(nameof(WorkersDecreasePaths))]
    public void Create_LevelWithFewerWorkersThanTheLevelBelow_ReportsWorkersDecrease(string path) =>
        ContentAssert.SingleError(ValidWith(_workersDecrease[path]), ContentErrorCode.WorkersDecrease, path);

    /// <summary>Equal worker counts on consecutive levels are fine: only a drop is an error.</summary>
    [Fact]
    public void Create_SameWorkersOnEveryLevel_IsAccepted()
    {
        GameContentData data = Valid();
        Level(data, "lumber_camp", 2).MaxWorkers = 2;
        Level(data, "hut", 2).WorkersProvided = 3;

        ContentAssert.Valid(data);
    }

    /// <summary>A raw producer, such as a woodcutter, may make something from nothing but labor.</summary>
    [Fact]
    public void Create_ProducerWithoutInputs_IsAccepted()
    {
        // A raw producer, such as a woodcutter, makes something from nothing but labor.
        GameContentData data = Valid();
        Level(data, "workshop", 1).Recipe!.Inputs = null;

        ContentAssert.Valid(data);
    }

    /// <summary>Several numbering mistakes in one building are one LevelsNotContiguous error (plus the drop they cause), not one per level.</summary>
    [Fact]
    public void Create_LevelsOfOneBuildingInDisorderAndGaps_ReportsOneErrorForTheBuilding()
    {
        GameContentData data = Valid();
        Level(data, "hut", 1).Level = 5;
        Level(data, "hut", 5).UpgradeCost = new() { ["gold"] = 1 };
        Level(data, "hut", 5).UpgradeSeconds = 1;

        ContentAssert.Errors(
            data,
            (ContentErrorCode.LevelsNotContiguous, "buildings[hut].levels"),
            (ContentErrorCode.WorkersDecrease, "buildings[hut].levels[5].workersProvided"));
    }
}
