using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;

namespace CityBuilder.Simulation.Tests.Performance;

/// <summary>
/// The Era I case of the performance budget (<c>04</c> section 12, M1-02, M1-03): the largest map of the game's content
/// (<c>server/content/era1-content.json</c>, <c>era1-2</c>: the 18 × 14 map of <c>29</c>, every area open, a bridge on every river tile)
/// with 64 buildings of all 13 types, every research node done, each building at its highest level. Each building takes the first free tile in
/// reading order where its type may stand (open grass or soil, its site met): the Scriptorium, the Town Hall and a Warehouse; 18 producers (4 woodcutters, 2 sawmills, a trading post,
/// 2 quarries, a stonemason, 4 farms, 2 windmills, 2 bakeries), each with its most workers (119); 18 courier stations with 6 couriers each
/// (108, 43 200 s of stamina); 25 houses. The houses and the Town Hall provide 237 workers, so 10 stay free. Every courier carries one link of
/// the three chains (<see cref="Links"/>), the couriers dealt over the links in turn; the processors start with a full store of their input,
/// so every chain runs from the first second. The city is restored as it is stored, at clock 1000 with every producer idle and every
/// courier at its station: the first advance starts them.
/// </summary>
internal static class Era1WorstCase
{
    /// <summary>The city clock the city is stored at.</summary>
    public const long Clock = 1000;

    /// <summary>The presence grace of the server, in seconds: the absence starts this long after the clock.</summary>
    public const long Grace = 300;

    /// <summary>The courier stations of the case.</summary>
    public const int Stations = 18;

    /// <summary>The couriers of a level-3 station.</summary>
    public const int CouriersPerStation = 6;

    /// <summary>A level-3 station's stamina, in seconds.</summary>
    public const long Stamina = 43_200;

    /// <summary>The content of the case: the game's Era I content, loaded once and checked like the server's.</summary>
    public static GameContent CaseContent { get; } = ContentAssert.Valid(ContentFile.ReadServer("era1-content.json"));

    /// <summary>The goods the three chains make: every item of the content.</summary>
    public static readonly string[] Goods = ["logs", "planks", "stone", "cut_stone", "grain", "flour", "bread", "coins"];

    /// <summary>The goods couriers carry: every good of the content but the currency.</summary>
    public static readonly string[] Carried = ["logs", "planks", "stone", "cut_stone", "grain", "flour", "bread"];

    /// <summary>The building types of the tiles, row by row (the stations and the houses follow).</summary>
    private static readonly string[] _fixedTiles =
    [
        "scriptorium", "town_hall", "warehouse",
        "woodcutter", "woodcutter", "woodcutter", "woodcutter", "sawmill", "sawmill", "trading_post",
        "quarry", "quarry", "stonemason",
        "farm", "farm", "farm", "farm", "windmill", "windmill", "bakery", "bakery",
    ];

    /// <summary>The houses of the case.</summary>
    private const int _houses = 25;

    /// <summary>
    /// The links the couriers carry, as building ids of the tile order: each woodcutter's logs to a sawmill, the sawmills' planks to the
    /// trading post, the quarries' stone to the stonemason, its cut stone to the warehouse, each farm's grain to a windmill, the windmills'
    /// flour to a bakery, the bakeries' bread to the warehouse.
    /// </summary>
    public static readonly (int Source, int Target, string Item)[] Links =
    [
        (4, 8, "logs"), (5, 9, "logs"), (6, 8, "logs"), (7, 9, "logs"),
        (8, 10, "planks"), (9, 10, "planks"),
        (11, 13, "stone"), (12, 13, "stone"),
        (13, 3, "cut_stone"),
        (14, 18, "grain"), (15, 19, "grain"), (16, 18, "grain"), (17, 19, "grain"),
        (18, 20, "flour"), (19, 21, "flour"),
        (20, 3, "bread"), (21, 3, "bread"),
    ];

    /// <summary>The input each processor starts with, a full store of it.</summary>
    private static readonly Dictionary<string, (string Item, long Quantity)> _startingInput = new()
    {
        ["sawmill"] = ("logs", 120),
        ["trading_post"] = ("planks", 120),
        ["stonemason"] = ("stone", 120),
        ["windmill"] = ("grain", 120),
        ["bakery"] = ("flour", 120),
    };

    /// <summary>The city of the class summary, a new object each time.</summary>
    public static CityState Build()
    {
        string[] types = [.. _fixedTiles, .. Enumerable.Repeat("courier_station", Stations), .. Enumerable.Repeat("house", _houses)];
        int width = CaseContent.Rules.GridWidth;
        var taken = new bool[width * CaseContent.Rules.GridHeight];
        var buildings = new List<Building>();
        var couriers = new List<Courier>();
        var routes = new List<Route>();
        int courier = 0;
        for (int index = 0; index < types.Length; index++)
        {
            int id = index + 1;
            BuildingDefinition definition = Definition(types[index]);
            BuildingLevelDefinition top = definition.Levels[^1];
            int workers = definition.Kind is BuildingKind.Producer or BuildingKind.CourierStation ? top.MaxWorkers : 0;
            KeyValuePair<ItemId, long>[]? store = _startingInput.TryGetValue(types[index], out (string Item, long Quantity) input)
                ? [new KeyValuePair<ItemId, long>(new ItemId(input.Item), input.Quantity)]
                : null;
            (int x, int y) = FirstFreeTile(definition, taken);
            taken[(y * width) + x] = true;
            buildings.Add(new Building(id, definition.Id, x, y, top.Level, workers, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, store));
            if (definition.Kind != BuildingKind.CourierStation)
            {
                continue;
            }

            for (int slot = 1; slot <= CouriersPerStation; slot++, courier++)
            {
                (int source, int target, string item) = Links[courier % Links.Length];
                couriers.Add(new Courier(id, slot, CourierState.AtStation, 0, null, null, Stamina));
                routes.Add(new Route(id, slot,
                [
                    new RouteStop(source, [new RouteAction(RouteActionKind.Take, new ItemId(item), All)]),
                    new RouteStop(target, [new RouteAction(RouteActionKind.Put, new ItemId(item), All)]),
                ]));
            }
        }

        ResearchId[] research = [.. CaseContent.Research.Select(node => node.Id)];
        return new CityState(
            Clock, 1, buildings.Count + 1, 0, buildings, research, activeResearch: null, couriers: couriers, routes: routes, roads: Bridges());
    }

    /// <summary>The first tile in reading order that is not taken and where a building of the type may stand (every area is open).</summary>
    private static (int X, int Y) FirstFreeTile(BuildingDefinition definition, bool[] taken)
    {
        int width = CaseContent.Rules.GridWidth;
        for (int index = 0; index < taken.Length; index++)
        {
            if (!taken[index] && CityQueries.SiteError(CaseContent, definition, index % width, index / width) is null)
            {
                return (index % width, index / width);
            }
        }

        throw new InvalidOperationException("The map has no tile left for " + definition.Id.Value + ".");
    }

    /// <summary>A bridge on every water tile of the map, so the east bank is joined all along the river.</summary>
    private static RoadTile[] Bridges()
    {
        MapDefinition map = CaseContent.Map!;
        return [.. Enumerable.Range(0, map.Width * map.Height)
            .Where(index => map.TerrainAt(index % map.Width, index / map.Width) == Terrain.Water)
            .Select(index => new RoadTile(index % map.Width, index / map.Width, 1, isBridge: true))];
    }

    /// <summary>A building type of the content.</summary>
    private static BuildingDefinition Definition(string type) =>
        CaseContent.TryGetBuilding(new BuildingTypeId(type), out BuildingDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(type), type, "not a building type of era1-content.json");

    /// <summary>The preset <c>all</c>.</summary>
    private static Amount All => Amount.Preset(new AmountPresetId("all"));
}
