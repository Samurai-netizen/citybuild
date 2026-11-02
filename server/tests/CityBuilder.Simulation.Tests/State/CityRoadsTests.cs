using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>
/// The roads of a city (<c>core-instructions/04-simulation-model.md</c>, section 17): <see cref="RoadTile"/> values, the restoring constructor
/// (sorted by tile, one per tile, never on a building), the lookups by tile and <see cref="CityQueries.TileAt"/>.
/// </summary>
public sealed class CityRoadsTests
{
    /// <summary>A road keeps its tile and level; a negative coordinate or a level below 1 is refused.</summary>
    [Fact]
    public void RoadTile_Values_AreKeptAndCheckedOnCreation()
    {
        var road = new RoadTile(3, 1, 2);

        Assert.Equal((3, 1, 2), (road.X, road.Y, road.Level));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoadTile(-1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoadTile(0, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoadTile(0, 0, 0));
    }

    /// <summary>Roads are values: equal tile and level are equal, another level is not.</summary>
    [Fact]
    public void RoadTile_Equality_IsByTileAndLevel()
    {
        Assert.Equal(new RoadTile(2, 2, 1), new RoadTile(2, 2, 1));
        Assert.True(new RoadTile(2, 2, 1) == new RoadTile(2, 2, 1));
        Assert.True(new RoadTile(2, 2, 1) != new RoadTile(2, 2, 2));
        Assert.Equal(new RoadTile(2, 2, 1).GetHashCode(), new RoadTile(2, 2, 1).GetHashCode());
        Assert.Equal("road (2, 2) level 1", new RoadTile(2, 2, 1).ToString());
    }

    /// <summary>The restoring constructor sorts the roads by column, then row, whatever order they come in.</summary>
    [Fact]
    public void Constructor_RoadsInAnyOrder_SortsThemByTile()
    {
        CityState city = City(roads: [new RoadTile(2, 0, 1), new RoadTile(0, 3, 2), new RoadTile(0, 1, 1)]);

        Assert.Equal([new RoadTile(0, 1, 1), new RoadTile(0, 3, 2), new RoadTile(2, 0, 1)], city.Roads);
    }

    /// <summary>A city without roads has none; a city made without the road argument too.</summary>
    [Fact]
    public void Constructor_NoRoads_HasNone() => Assert.Empty(City().Roads);

    /// <summary>Two roads on one tile, even of different levels, are not a valid city.</summary>
    [Fact]
    public void Constructor_TwoRoadsOnOneTile_Throws() =>
        Assert.Throws<ArgumentException>(() => City(roads: [new RoadTile(1, 1, 1), new RoadTile(1, 1, 2)]));

    /// <summary>A road on a building's tile is not a valid city.</summary>
    [Fact]
    public void Constructor_RoadOnABuildingsTile_Throws() =>
        Assert.Throws<ArgumentException>(() => City(buildings: [Idle(1, "hut", 2, 3)], roads: [new RoadTile(2, 3, 1)]));

    /// <summary>The road of a tile is found; a tile without one finds nothing.</summary>
    [Fact]
    public void TryGetRoad_ByTile_FindsTheRoadOrNothing()
    {
        CityState city = City(roads: [new RoadTile(1, 0, 1), new RoadTile(1, 2, 2), new RoadTile(4, 4, 1)]);

        Assert.True(city.TryGetRoad(1, 2, out RoadTile road));
        Assert.Equal(new RoadTile(1, 2, 2), road);
        Assert.False(city.TryGetRoad(1, 1, out _));
        Assert.False(city.TryGetRoad(0, 0, out _));
        Assert.False(city.TryGetRoad(5, 5, out _));
    }

    /// <summary>The building of a tile is found in every phase; an empty tile finds nothing.</summary>
    [Fact]
    public void TryGetBuildingAt_ByTile_FindsTheBuildingInAnyPhase()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0), Constructing(2, "lumber_camp", 3, 2, 990, 1000)]);

        Assert.True(city.TryGetBuildingAt(3, 2, out Building? site));
        Assert.Equal(2, site!.Id);
        Assert.False(city.TryGetBuildingAt(2, 3, out _));
    }

    /// <summary>A tile holds a building, a road or nothing.</summary>
    [Fact]
    public void TileAt_EachKindOfTile_SaysWhatLiesOnIt()
    {
        CityState city = City(buildings: [Idle(1, "hut", 0, 0)], roads: [new RoadTile(1, 0, 1)]);

        Assert.Equal(TileContent.Building, CityQueries.TileAt(city, 0, 0));
        Assert.Equal(TileContent.Road, CityQueries.TileAt(city, 1, 0));
        Assert.Equal(TileContent.Ground, CityQueries.TileAt(city, 2, 0));
    }
}
