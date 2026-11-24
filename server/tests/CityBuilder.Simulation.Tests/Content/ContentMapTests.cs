using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The content of M1-03's map (<c>29</c> → Map, <c>04</c> section 17): the map's terrain and areas, the bridge, a building type's
/// <c>site</c>, and the effects <c>openLand</c> and <c>unlockBridge</c>: how they are read and the rules on them. The map fixture
/// (<see cref="MapContentFixture"/>) puts a 6 × 5 map on the rule fixture.
/// </summary>
public sealed class ContentMapTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (MapContentFixture.Valid).

    /// <summary>The map keeps its id, start area, rough time, the terrain and the area of every tile.</summary>
    [Fact]
    public void Create_Map_KeepsTerrainAndAreas()
    {
        GameContent content = ContentAssert.Valid(MapContentFixture.Valid());

        MapDefinition map = Assert.IsType<MapDefinition>(content.Map);
        Assert.Equal(("meadow", 'A', 9000L, 6, 5), (map.Id, map.StartArea, map.RoughTileMillis, map.Width, map.Height));
        Assert.Equal(
            [Terrain.Forest, Terrain.Grass, Terrain.Rock, Terrain.Soil, Terrain.Water],
            new[] { map.TerrainAt(0, 0), map.TerrainAt(1, 1), map.TerrainAt(0, 2), map.TerrainAt(0, 4), map.TerrainAt(5, 0) });
        Assert.Equal(['A', 'B', 'C'], map.AreaLetters);
        Assert.Equal(('A', 'B', 'C'), (map.AreaAt(3, 3), map.AreaAt(4, 0), map.AreaAt(0, 4)));
        Assert.Equal(Terrain.Water, content.TerrainAt(5, 0));
    }

    /// <summary>Content without a map (M0's, the golden scenarios') has every tile grass, and no bridge.</summary>
    [Fact]
    public void Create_WithoutMap_EveryTileIsGrass()
    {
        GameContent content = ContentAssert.Valid(Valid());

        Assert.Null(content.Map);
        Assert.Null(content.Bridge);
        Assert.Equal(Terrain.Grass, content.TerrainAt(5, 4));
    }

    /// <summary>The bridge, the site, and the effects <c>openLand</c> and <c>unlockBridge</c> keep their values.</summary>
    [Fact]
    public void Create_BridgeSiteAndNewEffects_KeepTheirValues()
    {
        GameContent content = ContentAssert.Valid(MapContentFixture.Valid());

        BridgeDefinition bridge = Assert.IsType<BridgeDefinition>(content.Bridge);
        Assert.Equal(("Bridge", 1500L), (bridge.Name, bridge.TileMillis));
        ItemQuantity cost = Assert.Single(bridge.Cost);
        Assert.Equal((new ItemId("gold"), 30L), (cost.Item, cost.Quantity));
        Assert.True(content.TryGetBuilding(new BuildingTypeId("lumber_camp"), out BuildingDefinition? camp));
        Assert.Equal(BuildingSite.NearForest, camp.Site);
        Assert.True(content.TryGetBuilding(new BuildingTypeId("hut"), out BuildingDefinition? hut));
        Assert.Equal(BuildingSite.Any, hut.Site);
        Assert.True(content.TryGetResearch(new ResearchId("tallying"), out ResearchDefinition? tallying));
        Assert.Equal('B', Assert.Single(tallying.Effects.OfType<OpenLandEffect>()).Area);
        Assert.True(content.TryGetResearch(new ResearchId("carpentry"), out ResearchDefinition? carpentry));
        Assert.Contains(carpentry.Effects, effect => effect is UnlockBridgeEffect);
    }

    /// <summary>Each site value of the JSON becomes its <see cref="BuildingSite"/>.</summary>
    [Theory]
    [InlineData("nearForest", BuildingSite.NearForest)]
    [InlineData("nearRock", BuildingSite.NearRock)]
    [InlineData("fertileSoil", BuildingSite.FertileSoil)]
    public void Create_Site_ReadsEachValue(string text, BuildingSite expected)
    {
        GameContent content = ContentAssert.Valid(MapContentFixture.ValidWith(data => Building(data, "hut").Site = text));

        Assert.True(content.TryGetBuilding(new BuildingTypeId("hut"), out BuildingDefinition? hut));
        Assert.Equal(expected, hut.Site);
    }

    /// <summary>Broken maps, sites and effects, each with the one error it gives and where.</summary>
    private static readonly Dictionary<string, (Action<GameContentData> Change, ContentErrorCode Code, string Path)> _broken = new()
    {
        ["rows"] = (data => data.Map!.Tiles!.RemoveAt(4), ContentErrorCode.MapMismatch, "map.tiles"),
        ["row length"] = (data => data.Map!.Tiles![2] = "R....", ContentErrorCode.MapMismatch, "map.tiles[#2]"),
        ["terrain letter"] = (data => data.Map!.Tiles![1] = ".X...W", ContentErrorCode.InvalidValue, "map.tiles[#1]"),
        ["area rows"] = (data => data.Map!.Areas!.Add("AAAABB"), ContentErrorCode.MapMismatch, "map.areas"),
        ["area length"] = (data => data.Map!.Areas![0] = "AAAAB", ContentErrorCode.MapMismatch, "map.areas[#0]"),
        ["area letter"] = (data => data.Map!.Areas![0] = "AAAAb.", ContentErrorCode.InvalidValue, "map.areas[#0]"),
        ["start area"] = (data => data.Map!.StartArea = "D", ContentErrorCode.UnknownReference, "map.startArea"),
        ["start area text"] = (data => data.Map!.StartArea = "AB", ContentErrorCode.InvalidValue, "map.startArea"),
        ["rough time"] = (data => data.Map!.RoughTileMillis = 0, ContentErrorCode.NotPositive, "map.roughTileMillis"),
        ["map id"] = (data => data.Map!.Id = null, ContentErrorCode.MissingValue, "map.id"),
        ["site"] = (data => Building(data, "hut").Site = "nearWater", ContentErrorCode.InvalidValue, "buildings[hut].site"),
        ["open unknown area"] = (data => Research(data, "tallying").Effects![1]!.Area = "D", ContentErrorCode.UnknownReference,
            "research[tallying].effects[#1].area"),
        ["open the start area"] = (data => Research(data, "tallying").Effects![1]!.Area = "A", ContentErrorCode.InvalidEffectTarget,
            "research[tallying].effects[#1].area"),
        ["open without area"] = (data => Research(data, "tallying").Effects![1]!.Area = null, ContentErrorCode.MissingValue,
            "research[tallying].effects[#1].area"),
        ["area on another effect"] = (data => Research(data, "guild").Effects![0]!.Area = "B", ContentErrorCode.UnexpectedValue,
            "research[guild].effects[#0].area"),
        ["bridge value"] = (data => Research(data, "carpentry").Effects!.Last()!.Item = "wood", ContentErrorCode.UnexpectedValue,
            "research[carpentry].effects[#1].item"),
        ["bridge time"] = (data => data.Bridge!.TileMillis = 4000, ContentErrorCode.RoadNotFaster, "bridge.tileMillis"),
        ["bridge goods"] = (data => data.Bridge!.Cost!["wood"] = 2, ContentErrorCode.InvalidCurrencyUse, "bridge.cost.wood"),
        ["bridge without unlock"] = (data => Research(data, "carpentry").Effects!.RemoveAt(1), ContentErrorCode.BuildingUnreachable, "bridge"),
        ["unlock without bridge"] = (data => data.Bridge = null, ContentErrorCode.MissingValue, "bridge"),
        ["site without map"] = (data => data.Map = null, ContentErrorCode.MissingValue, "map"),
    };

    /// <summary>The names of <see cref="_broken"/>.</summary>
    public static TheoryData<string> BrokenCases => new(_broken.Keys);

    /// <summary>Each broken case gives exactly its one error.</summary>
    [Theory]
    [MemberData(nameof(BrokenCases))]
    public void Create_BrokenMapSiteOrEffect_ReportsOneError(string name)
    {
        (Action<GameContentData> change, ContentErrorCode code, string path) = _broken[name];

        ContentAssert.SingleError(MapContentFixture.ValidWith(change), code, path);
    }

    /// <summary>A grid of another size than the map gives one <see cref="ContentErrorCode.MapMismatch"/> on each list's first row.</summary>
    [Fact]
    public void Create_GridOfAnotherSize_ReportsMapMismatch() =>
        ContentAssert.Errors(
            MapContentFixture.ValidWith(data => data.Rules!.GridWidth = 7),
            (ContentErrorCode.MapMismatch, "map.tiles[#0]"),
            (ContentErrorCode.MapMismatch, "map.areas[#0]"));
}
