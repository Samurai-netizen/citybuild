using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// A small, valid content in code, new on every call, so a test can break exactly one value. Its numbers are made up for the tests and
/// are not the game's (those live in <c>server/content/*.json</c>, <c>core-instructions/24-economy-design.md</c> → Ownership of numbers).
/// It uses every feature: two eras, a capped and an uncapped item, a house and two producers (one locked), and each effect type.
/// </summary>
internal static class ContentFixture
{
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

    // The fixture is built fresh each time, so every test may change any of it; these shortcuts reach into it without null checks.
    public static GameRulesData RulesOf(GameContentData data) => data.Rules!;

    public static BuildingData Building(GameContentData data, string id) => data.Buildings!.Single(building => building!.Id == id)!;

    public static BuildingLevelData Level(GameContentData data, string building, int level) =>
        Building(data, building).Levels!.Single(candidate => candidate!.Level == level)!;

    public static ResearchData Research(GameContentData data, string id) => data.Research!.Single(node => node!.Id == id)!;

    public static ItemData Item(GameContentData data, string id) => data.Items!.Single(item => item!.Id == id)!;

    public static EraData Era(GameContentData data, int number) => data.Eras!.Single(era => era!.Number == number)!;

    private static RecipeData Recipe(Dictionary<string, long?>? inputs, Dictionary<string, long?> outputs, long labor) =>
        new() { Inputs = inputs, Outputs = outputs, Labor = labor };
}
