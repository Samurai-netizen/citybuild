using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// A small, valid content in code, new on every call, so a test can break exactly one value. Its numbers are made up for the tests and
/// are not the game's (those live in <c>server/content/*.json</c>, <c>core-instructions/24-economy-design.md</c> → Ownership of numbers).
/// It uses every feature: two eras, the currency and two goods, each building kind (a house, two producers (one locked), a warehouse, a
/// courier station, the science building), stores per level, each effect type, walking rules, two road levels and two amount presets.
/// </summary>
internal static class ContentFixture
{
    /// <summary>
    /// The valid test content, built new on every call: two eras; gold (the currency), wood and boards; a hut (3 then 5 workers), a lumber
    /// camp (1 wood from 40 labor, 2 then 3 workers, a store of 100 then 150 wood), a workshop locked behind carpentry (2 wood → 1 boards, 90
    /// labor, a store of 20 wood and 80 boards; it costs 40 gold and 10 wood), a depot (warehouse: 100 wood and 100 boards, then 200 each), a
    /// post (courier station: 2 then 3 couriers carrying 5 then 8, stamina 600 then 1 200 s) and a school (the science building, one per
    /// city); research carpentry → better_saws (−250 ‰ camp, −100 ‰ workshop) → warehouses (era 2, +50 wood, +40 boards), tallying (percent
    /// amounts, after carpentry), boots (walks 100 ‰ faster); walking 4 000 ms per ground tile, crossings at 250 ‰, a path (2 000 ms, 2 gold)
    /// and a paved path (1 000 ms, 6 gold); presets all (100 %) and half (50 %); trips carry 6; routes of up to 6 stops with 3 actions each;
    /// a new city starts with 100 gold.
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
            MaxOfflineProductionSeconds = 3600,
            MinCycleSeconds = 5,
            MaxLaborReductionPermille = 400,
            GroundTileMillis = 4000,
            CrossingSpeedPermille = 250,
            MinWalkSeconds = 2,
            MaxWalkBonusPermille = 300,
            TripCarryLimit = 6,
            MaxRouteStops = 6,
            MaxStopActions = 3,
        },
        Roads =
        [
            new RoadLevelData { Level = 1, Name = "Path", TileMillis = 2000, Cost = new() { ["gold"] = 2 } },
            new RoadLevelData { Level = 2, Name = "Paved path", TileMillis = 1000, Cost = new() { ["gold"] = 6 } },
        ],
        AmountPresets =
        [
            new AmountPresetData { Id = "all", Name = "All", Percent = 100 },
            new AmountPresetData { Id = "half", Name = "Half", Percent = 50 },
        ],
        StartingCity = new StartingCityData { Era = 1, Purse = 100 },
        Eras =
        [
            new EraData { Number = 1, Id = "first_era", Name = "First Era" },
            new EraData { Number = 2, Id = "second_era", Name = "Second Era" },
        ],
        Items =
        [
            new ItemData { Id = "gold", Name = "Gold", Era = 1, Currency = true, Tradable = false },
            new ItemData { Id = "wood", Name = "Wood", Era = 1, Currency = false, Tradable = true },
            new ItemData { Id = "boards", Name = "Boards", Era = 1, Currency = false, Tradable = true },
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
                    new BuildingLevelData
                    {
                        Level = 1, MaxWorkers = 2, Recipe = Recipe(inputs: null, outputs: new() { ["wood"] = 1 }, labor: 40),
                        Storage = new() { ["wood"] = 100 },
                    },
                    new BuildingLevelData
                    {
                        Level = 2, MaxWorkers = 3, Recipe = Recipe(inputs: null, outputs: new() { ["wood"] = 1 }, labor: 40),
                        Storage = new() { ["wood"] = 150 }, UpgradeCost = new() { ["gold"] = 30 }, UpgradeSeconds = 12,
                    },
                ],
            },
            new BuildingData
            {
                Id = "workshop", Name = "Workshop", Kind = "producer", Era = 1, AvailableAtStart = false,
                Cost = new() { ["gold"] = 40, ["wood"] = 10 }, BuildSeconds = 20,
                Levels =
                [
                    new BuildingLevelData
                    {
                        Level = 1, MaxWorkers = 2, Recipe = Recipe(new() { ["wood"] = 2 }, new() { ["boards"] = 1 }, 90),
                        Storage = new() { ["wood"] = 20, ["boards"] = 80 },
                    },
                ],
            },
            new BuildingData
            {
                Id = "depot", Name = "Depot", Kind = "warehouse", Era = 1, AvailableAtStart = true,
                Cost = new() { ["gold"] = 15 }, BuildSeconds = 6,
                Levels =
                [
                    new BuildingLevelData { Level = 1, Storage = new() { ["wood"] = 100, ["boards"] = 100 } },
                    new BuildingLevelData
                    {
                        Level = 2, Storage = new() { ["wood"] = 200, ["boards"] = 200 }, UpgradeCost = new() { ["gold"] = 25 }, UpgradeSeconds = 9,
                    },
                ],
            },
            new BuildingData
            {
                Id = "post", Name = "Post", Kind = "courierStation", Era = 1, AvailableAtStart = true,
                Cost = new() { ["gold"] = 25 }, BuildSeconds = 6,
                Levels =
                [
                    new BuildingLevelData { Level = 1, MaxWorkers = 2, CarryLimit = 5, StaminaSeconds = 600 },
                    new BuildingLevelData
                    {
                        Level = 2, MaxWorkers = 3, CarryLimit = 8, StaminaSeconds = 1200, UpgradeCost = new() { ["gold"] = 35, ["boards"] = 4 },
                        UpgradeSeconds = 10,
                    },
                ],
            },
            new BuildingData
            {
                Id = "school", Name = "School", Kind = "science", Era = 1, AvailableAtStart = true, MaxPerCity = 1,
                Cost = new() { ["gold"] = 30 }, BuildSeconds = 5,
                Levels = [new BuildingLevelData { Level = 1 }],
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
            new ResearchData
            {
                Id = "tallying", Name = "Tallying", Era = 1, Cost = new() { ["gold"] = 15 }, DurationSeconds = 50, Prerequisites = ["carpentry"],
                Effects = [new ResearchEffectData { Type = "unlockAmountMode", Mode = "percent" }],
            },
            new ResearchData
            {
                Id = "boots", Name = "Boots", Era = 1, Cost = new() { ["gold"] = 20, ["wood"] = 3 }, DurationSeconds = 40,
                Effects = [new ResearchEffectData { Type = "walkSpeedBonus", Permille = 100 }],
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

    /// <summary>The road level with this number in <paramref name="data"/>.</summary>
    public static RoadLevelData Road(GameContentData data, int level) => data.Roads!.Single(road => road!.Level == level)!;

    /// <summary>The fixture's amount preset with the id.</summary>
    public static AmountPresetData Preset(GameContentData data, string id) => data.AmountPresets!.Single(preset => preset!.Id == id)!;

    /// <summary>A recipe's data from its inputs (null for a raw producer), outputs and labor.</summary>
    private static RecipeData Recipe(Dictionary<string, long?>? inputs, Dictionary<string, long?> outputs, long labor) =>
        new() { Inputs = inputs, Outputs = outputs, Labor = labor };
}
