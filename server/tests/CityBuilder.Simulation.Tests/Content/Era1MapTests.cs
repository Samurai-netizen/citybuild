using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The map of the server's Era I content (<c>server/content/era1-content.json</c>, M1-03) against <c>29</c> → Map: its size, its start area
/// and what each area holds; which research opens which area and unlocks the bridge; the bridge's numbers and the three raw producers'
/// sites (<c>28</c> → Roads, <c>22</c> → Terrain).
/// </summary>
public sealed class Era1MapTests
{
    /// <summary>The content under test, read and checked once.</summary>
    private static readonly GameContent _content = ContentAssert.Valid(ContentFile.ReadServer("era1-content.json"));

    /// <summary>The map of the content, which has one.</summary>
    private static MapDefinition Map => Assert.IsType<MapDefinition>(_content.Map);

    /// <summary>The content version moved on with the map, and the map is 18 × 14 with area A open at the start.</summary>
    [Fact]
    public void Map_Era1_Is18By14WithStartAreaA()
    {
        Assert.Equal("era1-2", _content.ContentVersion);
        Assert.Equal((18, 14, 'A'), (Map.Width, Map.Height, Map.StartArea));
        Assert.Equal((18, 14), (_content.Rules.GridWidth, _content.Rules.GridHeight));
        Assert.Equal(['A', 'B', 'C'], Map.AreaLetters);
        Assert.Equal(2 * _content.Rules.GroundTileMillis, Map.RoughTileMillis);
    }

    /// <summary>
    /// Area A holds what <c>29</c> counts: 11 forest and 5 rock tiles, 19 grass tiles beside a forest and 7 beside rock (the neighbour may
    /// lie under mist), no fertile soil; area C holds 37 fertile soil tiles; the river runs down x = 12.
    /// </summary>
    [Fact]
    public void Map_Areas_HoldTheTerrainOf29()
    {
        Assert.Equal(11, CountIn('A', Terrain.Forest));
        Assert.Equal(5, CountIn('A', Terrain.Rock));
        Assert.Equal(0, CountIn('A', Terrain.Soil));
        Assert.Equal(19, GrassBesideIn('A', Terrain.Forest));
        Assert.Equal(7, GrassBesideIn('A', Terrain.Rock));
        Assert.Equal(37, CountIn('C', Terrain.Soil));
        for (int y = 0; y < Map.Height; y++)
        {
            Assert.Equal(Terrain.Water, Map.TerrainAt(12, y));
        }
    }

    /// <summary><c>land_survey</c> opens area B, <c>open_fields</c> area C, and <c>bridge_building</c> unlocks the bridge.</summary>
    [Fact]
    public void Research_LandAndBridge_OpenTheAreasOf28()
    {
        Assert.Equal('B', Assert.Single(EffectsOf("land_survey").OfType<OpenLandEffect>()).Area);
        Assert.Equal('C', Assert.Single(EffectsOf("open_fields").OfType<OpenLandEffect>()).Area);
        Assert.Single(EffectsOf("bridge_building").OfType<UnlockBridgeEffect>());
    }

    /// <summary>The bridge crosses a tile in 1 500 ms and costs 60 coins (<c>28</c> → Roads).</summary>
    [Fact]
    public void Bridge_Era1_HasTheNumbersOf28()
    {
        BridgeDefinition bridge = Assert.IsType<BridgeDefinition>(_content.Bridge);
        Assert.Equal(1500, bridge.TileMillis);
        ItemQuantity cost = Assert.Single(bridge.Cost);
        Assert.Equal((new ItemId("coins"), 60L), (cost.Item, cost.Quantity));
    }

    /// <summary>The Woodcutter's Hut needs forest beside it, the Quarry rock, the Farm fertile soil; every other type any grass or soil.</summary>
    [Fact]
    public void Buildings_RawProducers_HaveTheirSites()
    {
        foreach (BuildingDefinition building in _content.Buildings)
        {
            BuildingSite expected = building.Id.Value switch
            {
                "woodcutter" => BuildingSite.NearForest,
                "quarry" => BuildingSite.NearRock,
                "farm" => BuildingSite.FertileSoil,
                _ => BuildingSite.Any,
            };
            Assert.Equal(expected, building.Site);
        }
    }

    /// <summary>The effects of a research node of the content.</summary>
    private static IReadOnlyList<ResearchEffect> EffectsOf(string id)
    {
        Assert.True(_content.TryGetResearch(new ResearchId(id), out ResearchDefinition? node));
        return node.Effects;
    }

    /// <summary>How many tiles of an area have a terrain.</summary>
    private static int CountIn(char area, Terrain terrain) =>
        Tiles().Count(tile => Map.AreaAt(tile.X, tile.Y) == area && Map.TerrainAt(tile.X, tile.Y) == terrain);

    /// <summary>How many grass tiles of an area have a tile of a terrain beside them (four directions, any area).</summary>
    private static int GrassBesideIn(char area, Terrain terrain) =>
        Tiles().Count(tile => Map.AreaAt(tile.X, tile.Y) == area && Map.TerrainAt(tile.X, tile.Y) == Terrain.Grass
            && new[] { (1, 0), (0, 1), (-1, 0), (0, -1) }.Any(step =>
            {
                int x = tile.X + step.Item1;
                int y = tile.Y + step.Item2;
                return x >= 0 && y >= 0 && x < Map.Width && y < Map.Height && Map.TerrainAt(x, y) == terrain;
            }));

    /// <summary>Every tile of the map.</summary>
    private static IEnumerable<(int X, int Y)> Tiles() =>
        Enumerable.Range(0, Map.Height).SelectMany(y => Enumerable.Range(0, Map.Width).Select(x => (x, y)));
}
