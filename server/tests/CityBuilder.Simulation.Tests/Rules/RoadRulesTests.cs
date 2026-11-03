using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>
/// <c>RoadRules</c> (<c>04</c> sections 8 and 17; CR-0008 D6): placing, upgrading and removing a list of road tiles, all or nothing, for coins
/// only and without a builder. Fixture numbers: the grid is 6 × 5; a path (level 1) costs 2 gold per tile, a paved path (level 2) 6 gold per
/// tile to upgrade to; a hut stands on (0,0) where a test puts one.
/// </summary>
public sealed class RoadRulesTests
{
    /// <summary>Placing pays level 1's cost once per tile and puts a level-1 road on every tile, sorted by tile.</summary>
    [Fact]
    public void PlaceRoads_EmptyTiles_PaysPerTileAndBuildsLevelOne()
    {
        CityState city = City(clock: 1000, purse: 10, buildings: [Idle(1, "hut", 0, 0)]);

        RulesAssert.Moved(Place(city, (2, 1), (1, 1), (3, 1)), spent: "gold=6", refunded: "");

        Assert.Equal([new RoadTile(1, 1, 1), new RoadTile(2, 1, 1), new RoadTile(3, 1, 1)], city.Roads);
        Assert.Equal(4, city.Purse);
    }

    /// <summary>No builder is needed: roads are placed even while every builder slot is busy.</summary>
    [Fact]
    public void PlaceRoads_EveryBuilderBusy_StillPlaces()
    {
        CityState city = City(purse: 10, buildings:
        [
            Constructing(1, "hut", 0, 0, startedAt: 998, endsAt: 1003),
            Constructing(2, "hut", 1, 0, startedAt: 998, endsAt: 1003),
        ]);

        RulesAssert.Ok(Place(city, (2, 2)));
    }

    /// <summary>No tile, or a tile named twice, is <see cref="MutationErrorCode.InvalidArgument"/> for every road command (the server refuses both before the lock).</summary>
    [Fact]
    public void AnyRoadCommand_NoTileOrATileTwice_IsInvalidArgument()
    {
        CityState city = City(purse: 100, roads: [new RoadTile(1, 1, 1)]);

        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, city, city => Place(city));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, city, city => Place(city, (2, 2), (2, 2)));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, city, city => Upgrade(city));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, city, city => Upgrade(city, (1, 1), (1, 1)));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, city, city => Remove(city));
        RulesAssert.Rejected(MutationErrorCode.InvalidArgument, city, city => Remove(city, (1, 1), (1, 1)));
    }

    /// <summary>A tile outside the grid is <see cref="MutationErrorCode.OutOfBounds"/>, and nothing of the list is built.</summary>
    [Theory]
    [InlineData(6, 0)]
    [InlineData(0, 5)]
    [InlineData(-1, 0)]
    public void PlaceRoads_TileOutsideTheGrid_IsRejected(int x, int y) =>
        RulesAssert.Rejected(MutationErrorCode.OutOfBounds, City(purse: 100), city => Place(city, (1, 1), (x, y)));

    /// <summary>A tile with a building (in any phase) or a road is <see cref="MutationErrorCode.TileOccupied"/>.</summary>
    [Fact]
    public void PlaceRoads_TileWithABuildingOrARoad_IsRejected()
    {
        CityState city = City(purse: 100, buildings: [Constructing(1, "hut", 0, 0, 998, 1003)], roads: [new RoadTile(2, 0, 1)]);

        RulesAssert.Rejected(MutationErrorCode.TileOccupied, city, city => Place(city, (1, 0), (0, 0)));
        RulesAssert.Rejected(MutationErrorCode.TileOccupied, city, city => Place(city, (1, 0), (2, 0)));
    }

    /// <summary>The cost of the whole list must be affordable: 3 tiles cost 6 gold, 5 is not enough.</summary>
    [Fact]
    public void PlaceRoads_NotEnoughForTheWholeList_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.InsufficientResource, City(purse: 5), city => Place(city, (1, 1), (2, 1), (3, 1)));

    /// <summary>The checks run over the whole list in the order bounds, occupancy, cost: an occupied first tile and an outside second tile is out of bounds.</summary>
    [Fact]
    public void PlaceRoads_SeveralProblems_ReportsTheFirstCheckOverTheWholeList()
    {
        CityState city = City(purse: 0, buildings: [Idle(1, "hut", 0, 0)]);

        RulesAssert.Rejected(MutationErrorCode.OutOfBounds, city, city => Place(city, (0, 0), (9, 9)));
        RulesAssert.Rejected(MutationErrorCode.TileOccupied, city, city => Place(city, (1, 1), (0, 0)));
    }

    /// <summary>Upgrading pays the next level's cost per tile and raises every road one level.</summary>
    [Fact]
    public void UpgradeRoads_RoadsBelowTheLastLevel_PaysAndRaisesThem()
    {
        CityState city = City(purse: 20, roads: [new RoadTile(1, 1, 1), new RoadTile(2, 1, 1), new RoadTile(4, 4, 1)]);

        RulesAssert.Moved(Upgrade(city, (2, 1), (1, 1)), spent: "gold=12", refunded: "");

        Assert.Equal([new RoadTile(1, 1, 2), new RoadTile(2, 1, 2), new RoadTile(4, 4, 1)], city.Roads);
        Assert.Equal(8, city.Purse);
    }

    /// <summary>A tile without a road, also one outside the grid, is <see cref="MutationErrorCode.RoadNotFound"/>.</summary>
    [Theory]
    [InlineData(2, 2)]
    [InlineData(40, 40)]
    public void UpgradeRoads_TileWithoutARoad_IsRejected(int x, int y) =>
        RulesAssert.Rejected(MutationErrorCode.RoadNotFound, City(purse: 100, roads: [new RoadTile(1, 1, 1)]), city => Upgrade(city, (1, 1), (x, y)));

    /// <summary>A road at the last level is <see cref="MutationErrorCode.MaxLevel"/>; a missing road is reported before it.</summary>
    [Fact]
    public void UpgradeRoads_RoadAtTheLastLevel_IsRejectedAfterTheRoadCheck()
    {
        CityState city = City(purse: 100, roads: [new RoadTile(1, 1, 1), new RoadTile(2, 1, 2)]);

        RulesAssert.Rejected(MutationErrorCode.MaxLevel, city, city => Upgrade(city, (1, 1), (2, 1)));
        RulesAssert.Rejected(MutationErrorCode.RoadNotFound, city, city => Upgrade(city, (2, 1), (3, 1)));
    }

    /// <summary>The upgrade of the whole list must be affordable: two tiles cost 12 gold, 11 is not enough.</summary>
    [Fact]
    public void UpgradeRoads_NotEnoughForTheWholeList_IsRejected() =>
        RulesAssert.Rejected(
            MutationErrorCode.InsufficientResource,
            City(purse: 11, roads: [new RoadTile(1, 1, 1), new RoadTile(2, 1, 1)]),
            city => Upgrade(city, (1, 1), (2, 1)));

    /// <summary>Removing takes the roads off their tiles and refunds nothing; the tile is ground again.</summary>
    [Fact]
    public void RemoveRoads_RoadsOnTheTiles_RemovesThemWithoutRefund()
    {
        CityState city = City(purse: 3, roads: [new RoadTile(1, 1, 2), new RoadTile(2, 1, 1)]);

        RulesAssert.Moved(Remove(city, (1, 1)), spent: "", refunded: "");

        Assert.Equal([new RoadTile(2, 1, 1)], city.Roads);
        Assert.Equal(TileContent.Ground, CityQueries.TileAt(city, 1, 1));
        Assert.Equal(3, city.Purse);
    }

    /// <summary>A tile without a road is <see cref="MutationErrorCode.RoadNotFound"/>, and no road of the list is removed.</summary>
    [Fact]
    public void RemoveRoads_TileWithoutARoad_IsRejected() =>
        RulesAssert.Rejected(MutationErrorCode.RoadNotFound, City(roads: [new RoadTile(1, 1, 1)]), city => Remove(city, (1, 1), (0, 0)));

    /// <summary>A null argument is a programmer error and throws, unlike a business rule.</summary>
    [Fact]
    public void AnyRoadCommand_NullArgument_Throws()
    {
        CityState city = City();
        Assert.Throws<ArgumentNullException>(() => RoadRules.PlaceRoads(null!, TestContent, [(1, 1)]));
        Assert.Throws<ArgumentNullException>(() => RoadRules.UpgradeRoads(city, null!, [(1, 1)]));
        Assert.Throws<ArgumentNullException>(() => RoadRules.RemoveRoads(city, TestContent, null!));
    }

    /// <summary>Places roads on the tiles in the test content.</summary>
    private static MutationResult Place(CityState city, params (int X, int Y)[] tiles) => RoadRules.PlaceRoads(city, TestContent, tiles);

    /// <summary>Upgrades the roads on the tiles in the test content.</summary>
    private static MutationResult Upgrade(CityState city, params (int X, int Y)[] tiles) => RoadRules.UpgradeRoads(city, TestContent, tiles);

    /// <summary>Removes the roads on the tiles in the test content.</summary>
    private static MutationResult Remove(CityState city, params (int X, int Y)[] tiles) => RoadRules.RemoveRoads(city, TestContent, tiles);
}
