using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// M1-03 acceptance: the town the Chronicle of <c>29</c> asks for can be laid out on the Era I map. Every building of chapters 1–5 is placed
/// by the placement rule, in chapter order, each on a city holding the ones before it (built), with the research of its chapter done; the
/// eight road tiles and the bridge of chapters 2 and 3 are placed by the road rule; then every link of the three chains is walkable.
/// </summary>
public sealed class Era1TownLayoutTests
{
    /// <summary>The server's Era I content, with the map.</summary>
    private static readonly GameContent _content = ContentAssert.Valid(ContentFile.ReadServer("era1-content.json"));

    /// <summary>The research done by the end of chapter 5, which also opens areas B and C.</summary>
    private static readonly string[] _research =
        ["trade_charter", "stone_cutting", "sturdy_boots", "land_survey", "bridge_building", "open_fields", "timber_framing", "milling", "masonry"];

    /// <summary>The town in the Chronicle's order: type and tile (area A, the farms on the east bank's fertile soil).</summary>
    private static readonly (string Type, int X, int Y)[] _town =
    [
        ("house", 5, 5), ("woodcutter", 7, 9), ("scriptorium", 4, 4), ("sawmill", 6, 6), ("trading_post", 8, 6),
        ("courier_station", 5, 7), ("warehouse", 9, 7),
        ("quarry", 8, 3), ("stonemason", 7, 4),
        ("farm", 13, 3), ("farm", 14, 4), ("windmill", 10, 10),
        ("bakery", 9, 10), ("town_hall", 3, 10),
    ];

    /// <summary>The chains' links as indices into <see cref="_town"/>: logs, planks, stone, cut stone, grain, flour, bread.</summary>
    private static readonly (int Source, int Target)[] _links = [(1, 3), (3, 4), (7, 8), (8, 6), (9, 11), (10, 11), (11, 12), (12, 6)];

    /// <summary>Each building of the town is allowed on its tile, after the ones before it.</summary>
    [Fact]
    public void PlaceBuilding_TheChroniclesTown_FitsTheMap()
    {
        for (int index = 0; index < _town.Length; index++)
        {
            (string type, int x, int y) = _town[index];
            CityState city = City(purse: 100_000, completedResearch: _research, buildings: [.. Built(index)]);

            RulesAssert.Ok(CityRules.PlaceBuilding(city, _content, new BuildingTypeId(type), x, y));
        }
    }

    /// <summary>Eight road tiles and a bridge are placed, and every link of the chains is walkable over the bridge.</summary>
    [Fact]
    public void PlaceRoads_EightTilesAndABridge_JoinEveryChain()
    {
        CityState city = City(purse: 100_000, completedResearch: _research, buildings: [.. Built(_town.Length)]);

        RulesAssert.Ok(RoadRules.PlaceRoads(city, _content, [(6, 5), (7, 5), (8, 5), (9, 5), (10, 5), (11, 5), (13, 5), (14, 5)]));
        RulesAssert.Ok(RoadRules.PlaceRoads(city, _content, [(12, 5)]));

        var paths = new PathFinder(_content);
        paths.Load(city);
        Assert.All(_links, link =>
        {
            Building source = city.Buildings[link.Source];
            Building target = city.Buildings[link.Target];
            Assert.True(paths.TryMeasure(source.X, source.Y, target.X, target.Y, out _), $"{source.TypeId.Value} → {target.TypeId.Value}");
        });
    }

    /// <summary>The first <paramref name="count"/> buildings of the town, built and idle, with ids from 1.</summary>
    private static IEnumerable<Building> Built(int count) =>
        _town.Take(count).Select((building, index) => Idle(index + 1, building.Type, building.X, building.Y));
}
