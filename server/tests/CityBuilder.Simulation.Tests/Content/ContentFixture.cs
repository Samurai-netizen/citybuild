using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// A small, valid content in code, new on every call, so a test can break exactly one value. Its numbers are made up for the tests and
/// are not the game's (those live in <c>server/content/*.json</c>, <c>core-instructions/24-economy-design.md</c> → Ownership of numbers).
/// It uses every feature: two eras, a capped and an uncapped item, a house and two producers (one locked), and each effect type.
/// </summary>
internal static class ContentFixture
{
    /// <summary>
    /// The valid test content, built new on every call: two eras, gold (uncapped), wood (cap 100) and boards (cap 80), a hut (3 then 5
    /// workers), a lumber camp (1 wood from 40 labor, 2 then 3 workers) and a workshop locked behind carpentry (2 wood → 1 boards, 90 labor),
    /// and research carpentry → better_saws (−250 ‰ camp, −100 ‰ workshop) → warehouses (era 2, +50 wood, +40 boards).
    /// </summary>
    public static GameContentData Valid() => new()
    {
        ContentVersion = "test-1",
        Rules = new GameRulesData
        {
            GridWidth = 6,
            GridHeight = 5,
            BuilderSlots = 2,
            ResearchSlots = 1,
            BaseStorageCaps = new() { ["wood"] = 100, ["boards"] = 80 },
            MaxOfflineProductionSeconds = 3600,
            MinCycleSeconds = 5,
            MaxLaborReductionPermille = 400,
        },
        StartingCity = new StartingCityData { Era = 1, Inventory = new() { ["gold"] = 100, ["wood"] = 5 } },
        Eras =
        [
            new EraData { Number = 1, Id = "first_era", Name = "First Era" },
            new EraData { Number = 2, Id = "second_era", Name = "Second Era" },
        ],
        Items =
        [
            new ItemData { Id = "gold", Name = "Gold", Era = 1, Capped = false, Tradable = false },
            new ItemData { Id = "wood", Name = "Wood", Era = 1, Capped = true, Tradable = true },
            new ItemData { Id = "boards", Name = "Boards", Era = 1, Capped = true, Tradable = true },
        ],
        Buildings =
        [
            new BuildingData
            {
                Id = "hut", Name = "Hut", Kind = "house", Era = 1, AvailableAtStart = true,
                Cost = new() { ["gold"] = 10 }, BuildSeconds = 5,
                Levels =
                [
                    new BuildingLevelData { Level = 1, WorkersProvided = 3 },
                    new BuildingLevelData { Level = 2, WorkersProvided = 5, UpgradeCost = new() { ["gold"] = 20, ["wood"] = 5 }, UpgradeSeconds = 8 },
                ],
            },
            new BuildingData
            {
                Id = "lumber_camp", Name = "Lumber Camp", Kind = "producer", Era = 1, AvailableAtStart = true,
                Cost = new() { ["gold"] = 20 }, BuildSeconds = 10,
                Levels =
                [
                    new BuildingLevelData { Level = 1, MaxWorkers = 2, Recipe = Recipe(inputs: null, outputs: new() { ["wood"] = 1 }, labor: 40) },
                    new BuildingLevelData
                    {
                        Level = 2, MaxWorkers = 3, Recipe = Recipe(inputs: null, outputs: new() { ["wood"] = 1 }, labor: 40),
                        UpgradeCost = new() { ["gold"] = 30 }, UpgradeSeconds = 12,
                    },
                ],
            },
            new BuildingData
            {
                Id = "workshop", Name = "Workshop", Kind = "producer", Era = 1, AvailableAtStart = false,
                Cost = new() { ["gold"] = 40, ["wood"] = 10 }, BuildSeconds = 20,
                Levels =
                [
                    new BuildingLevelData { Level = 1, MaxWorkers = 2, Recipe = Recipe(new() { ["wood"] = 2 }, new() { ["boards"] = 1 }, 90) },
                ],
            },
        ],
        Research =
        [
            new ResearchData
            {
                Id = "carpentry", Name = "Carpentry", Era = 1, Cost = new() { ["gold"] = 50 }, DurationSeconds = 100,
                Effects = [new ResearchEffectData { Type = "unlockBuilding", Building = "workshop" }],
            },
            new ResearchData
            {
                Id = "better_saws", Name = "Better Saws", Era = 1, Cost = new() { ["gold"] = 60, ["boards"] = 5 }, DurationSeconds = 200,
                Prerequisites = ["carpentry"],
                Effects =
                [
                    new ResearchEffectData { Type = "laborReduction", Building = "lumber_camp", Permille = 250 },
                    new ResearchEffectData { Type = "laborReduction", Building = "workshop", Permille = 100 },
                ],
            },
            new ResearchData
            {
                Id = "warehouses", Name = "Warehouses", Era = 2, Cost = new() { ["gold"] = 80 }, DurationSeconds = 300,
                Prerequisites = ["better_saws"],
                Effects =
                [
                    new ResearchEffectData { Type = "storageCapBonus", Item = "wood", Amount = 50 },
                    new ResearchEffectData { Type = "storageCapBonus", Item = "boards", Amount = 40 },
                ],
            },
        ],
    };

    /// <summary>The valid fixture with one change applied: the way each rule test breaks exactly one value.</summary>
    public static GameContentData ValidWith(Action<GameContentData> change)
    {
        GameContentData data = Valid();
        change(data);
        return data;
    }

    // The fixture is built fresh each time, so every test may change any of it; these shortcuts reach into it without null checks.
    /// <summary>The fixture's global rules.</summary>
    public static GameRulesData RulesOf(GameContentData data) => data.Rules!;

    /// <summary>The fixture's building type with the id.</summary>
    public static BuildingData Building(GameContentData data, string id) => data.Buildings!.Single(building => building!.Id == id)!;

    /// <summary>A level of one of the fixture's building types, by its number.</summary>
    public static BuildingLevelData Level(GameContentData data, string building, int level) =>
        Building(data, building).Levels!.Single(candidate => candidate!.Level == level)!;

    /// <summary>The fixture's research node with the id.</summary>
    public static ResearchData Research(GameContentData data, string id) => data.Research!.Single(node => node!.Id == id)!;

    /// <summary>The fixture's item with the id.</summary>
    public static ItemData Item(GameContentData data, string id) => data.Items!.Single(item => item!.Id == id)!;

    /// <summary>The fixture's era with the number.</summary>
    public static EraData Era(GameContentData data, int number) => data.Eras!.Single(era => era!.Number == number)!;

    /// <summary>A recipe's data from its inputs (null for a raw producer), outputs and labor.</summary>
    private static RecipeData Recipe(Dictionary<string, long?>? inputs, Dictionary<string, long?> outputs, long labor) =>
        new() { Inputs = inputs, Outputs = outputs, Labor = labor };
}
