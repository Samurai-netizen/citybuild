using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// Roads and bridges on the Era I map (<c>04</c> sections 8 and 17, M1-03): a road stands on open grass or fertile soil, never on forest or
/// rock; a road placed on water is a bridge, once <c>bridge_building</c> is done; a bridge costs the bridge's coins, has one level and is
/// removed like a road. Tiles are those of <c>29</c>: the river is x = 12.
/// </summary>
public sealed class RoadTerrainTests
{
    /// <summary>The server's Era I content, with the map.</summary>
    private static readonly GameContent _content = ContentAssert.Valid(ContentFile.ReadServer("era1-content.json"));

    /// <summary>A road on forest or rock is refused, in or out of the open land.</summary>
    [Theory]
    [InlineData(8, 9)]
    [InlineData(9, 3)]
    [InlineData(0, 0)]
    public void PlaceRoads_OnForestOrRock_IsTerrainBlocked(int x, int y) =>
        RulesAssert.Rejected(MutationErrorCode.TerrainBlocked, Rich("bridge_building"), city => Place(city, (5, 5), (x, y)));

    /// <summary>A road on grass under mist is refused.</summary>
    [Fact]
    public void PlaceRoads_OnLandUnderMist_IsLandClosed() =>
        RulesAssert.Rejected(MutationErrorCode.LandClosed, Rich(), city => Place(city, (6, 0)));

    /// <summary>A road on water before <c>bridge_building</c> is refused: a bridge is locked.</summary>
    [Fact]
    public void PlaceRoads_OnWaterWithoutTheResearch_IsBridgeLocked() =>
        RulesAssert.Rejected(MutationErrorCode.BridgeLocked, Rich(), city => Place(city, (11, 5), (12, 5)));

    /// <summary>
    /// After <c>bridge_building</c> a road on water is a bridge of level 1 and costs the bridge's 60 coins; a road beside it on grass costs
    /// the dirt path's 5 coins.
    /// </summary>
    [Fact]
    public void PlaceRoads_OnWaterAfterTheResearch_PlacesABridge()
    {
        CityState city = City(purse: 100, completedResearch: ["bridge_building"]);

        RulesAssert.Moved(Place(city, (11, 5), (12, 5)), spent: "coins=65", refunded: "");

        Assert.Equal([new RoadTile(11, 5, 1), new RoadTile(12, 5, 1, isBridge: true)], city.Roads);
        Assert.Equal(35, city.Purse);
    }

    /// <summary>A bridge is paid in full or not placed: 64 coins don't pay for a bridge and a path.</summary>
    [Fact]
    public void PlaceRoads_BridgeAboveThePurse_IsInsufficientResource() =>
        RulesAssert.Rejected(
            MutationErrorCode.InsufficientResource,
            City(purse: 64, completedResearch: ["bridge_building"]),
            city => Place(city, (11, 5), (12, 5)));

    /// <summary>A road on the fertile soil of an opened area is placed.</summary>
    [Fact]
    public void PlaceRoads_OnFertileSoilOfOpenLand_IsPlaced() => RulesAssert.Ok(Place(Rich("open_fields"), (13, 3)));

    /// <summary>A bridge has one level: an upgrade is refused.</summary>
    [Fact]
    public void UpgradeRoads_Bridge_IsMaxLevel() =>
        RulesAssert.Rejected(
            MutationErrorCode.MaxLevel,
            City(purse: 1000, completedResearch: ["bridge_building"], roads: [new RoadTile(12, 5, 1, isBridge: true)]),
            city => RoadRules.UpgradeRoads(city, _content, [(12, 5)]));

    /// <summary>A bridge is removed like a road, for nothing back; the tile is water again.</summary>
    [Fact]
    public void RemoveRoads_Bridge_RemovesIt()
    {
        CityState city = City(purse: 0, completedResearch: ["bridge_building"], roads: [new RoadTile(12, 5, 1, isBridge: true)]);

        RulesAssert.Ok(RoadRules.RemoveRoads(city, _content, [(12, 5)]));

        Assert.Empty(city.Roads);
    }

    /// <summary>A bridge and a road of the same tile and level are not equal.</summary>
    [Fact]
    public void RoadTile_BridgeAndRoad_AreNotEqual() => Assert.NotEqual(new RoadTile(12, 5, 1), new RoadTile(12, 5, 1, isBridge: true));

    /// <summary>A city with coins for anything and the research named done.</summary>
    private static CityState Rich(params string[] research) => City(purse: 100_000, completedResearch: research);

    /// <summary>Places roads on the tiles, in order.</summary>
    private static MutationResult Place(CityState city, params (int X, int Y)[] tiles) => RoadRules.PlaceRoads(city, _content, tiles);
}
