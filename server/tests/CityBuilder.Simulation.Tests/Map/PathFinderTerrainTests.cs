using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;

namespace CityBuilder.Simulation.Tests.Map;

/// <summary>
/// The path rule on a map (<c>04</c> section 17, M1-03): forest and rock are crossed at the rough time, water only on a bridge (at the
/// bridge's time), and land under mist never; when nothing joins two buildings there is no path. Times are milliseconds and <c>D</c> counts
/// each twice: ground 4 000, rough 9 000, bridge 1 500 (<see cref="MapContentFixture"/>).
/// </summary>
public sealed class PathFinderTerrainTests
{
    /// <summary>A forest tile between two buildings is crossed at the rough time: <c>D</c> = 4 000 + 9 000 + 9 000 + 4 000.</summary>
    [Fact]
    public void Measure_ForestBetween_CostsTheRoughTime()
    {
        PathFinder finder = Loaded(Map([".F."], ["AAA"]), MapFixture.City("B.B"));

        Assert.Equal(new WalkCost(0, 26_000), finder.Measure(0, 0, 2, 0));
    }

    /// <summary>A rock tile is crossed at the rough time too.</summary>
    [Fact]
    public void Measure_RockBetween_CostsTheRoughTime()
    {
        PathFinder finder = Loaded(Map([".R."], ["AAA"]), MapFixture.City("B.B"));

        Assert.Equal(new WalkCost(0, 26_000), finder.Measure(0, 0, 2, 0));
    }

    /// <summary>Water in the way is walked around: four ground steps instead of two.</summary>
    [Fact]
    public void Measure_WaterInTheWay_GoesAround()
    {
        PathFinder finder = Loaded(Map(["...", ".W.", "..."], ["AAA", "AAA", "AAA"]), MapFixture.City("...", "B.B", "..."));

        Assert.Equal(new WalkCost(0, 32_000), finder.Measure(0, 1, 2, 1));
        Assert.DoesNotContain((1, 1), finder.Route(0, 1, 2, 1));
    }

    /// <summary>A river across the whole map with no bridge leaves no path: the walk can't be measured.</summary>
    [Fact]
    public void TryMeasure_RiverWithoutBridge_HasNoPath()
    {
        PathFinder finder = Loaded(Map([".W.", ".W.", ".W."], ["AAA", "AAA", "AAA"]), MapFixture.City("...", "B.B", "..."));

        Assert.False(finder.TryMeasure(0, 1, 2, 1, out _));
        Assert.Throws<InvalidOperationException>(() => finder.Measure(0, 1, 2, 1));
    }

    /// <summary>A bridge crosses the river at the bridge's time: <c>D</c> = 4 000 + 1 500 + 1 500 + 4 000.</summary>
    [Fact]
    public void Measure_RiverWithBridge_CrossesOnTheBridge()
    {
        PathFinder finder = Loaded(Map([".W.", ".W.", ".W."], ["AAA", "AAA", "AAA"]), MapFixture.City("...", "BbB", "..."));

        Assert.True(finder.TryMeasure(0, 1, 2, 1, out WalkCost cost));
        Assert.Equal(new WalkCost(0, 11_000), cost);
        Assert.Equal([(0, 1), (1, 1), (2, 1)], finder.Route(0, 1, 2, 1));
    }

    /// <summary>A tile under mist is walked around while its area is closed, and crossed once the research opens it.</summary>
    [Fact]
    public void Measure_LandUnderMist_IsWalkedAroundUntilOpened()
    {
        GameContent content = Map(["...", "...", "..."], ["AAA", "ABA", "AAA"]);

        PathFinder closed = Loaded(content, MapFixture.City("...", "B.B", "..."));
        PathFinder open = Loaded(content, MapFixture.CityWithResearch(["tallying"], "...", "B.B", "..."));

        Assert.Equal(new WalkCost(0, 32_000), closed.Measure(0, 1, 2, 1));
        Assert.Equal(new WalkCost(0, 16_000), open.Measure(0, 1, 2, 1));
    }

    /// <summary>A closed column across the map leaves no path.</summary>
    [Fact]
    public void TryMeasure_ClosedLandAcross_HasNoPath()
    {
        PathFinder finder = Loaded(Map(["...", "...", "..."], ["ABA", "ABA", "ABA"]), MapFixture.City("...", "B.B", "..."));

        Assert.False(finder.TryMeasure(0, 1, 2, 1, out _));
    }

    /// <summary>
    /// The map fixture's content on a map of the given rows (the grid takes their size); an <c>openLand</c> effect whose area the rows
    /// don't have is dropped, so the content stays valid.
    /// </summary>
    private static GameContent Map(string[] tiles, string[] areas) => ContentAssert.Valid(MapContentFixture.ValidWith(data =>
    {
        data.Rules!.GridWidth = tiles[0].Length;
        data.Rules.GridHeight = tiles.Length;
        data.Map!.Tiles = [.. tiles];
        data.Map.Areas = [.. areas];
        foreach (ResearchData? node in data.Research!)
        {
            node!.Effects!.RemoveAll(effect => effect!.Type == "openLand" && !areas.Any(row => row.Contains(effect.Area!, StringComparison.Ordinal)));
        }
    }));

    /// <summary>A finder for the content with the city's map loaded.</summary>
    private static PathFinder Loaded(GameContent content, CityState city)
    {
        var finder = new PathFinder(content);
        finder.Load(city);
        return finder;
    }
}
