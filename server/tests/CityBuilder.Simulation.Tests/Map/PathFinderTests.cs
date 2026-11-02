using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Map;

/// <summary>
/// The path rule of <c>core-instructions/04-simulation-model.md</c>, section 17, one rule per test: four-direction steps through tile centres,
/// a step costing half of each tile, the walk's own buildings as ground, roads used when they are faster, other buildings avoided whenever a
/// way around exists and crossed as few times as possible otherwise, and the tie rule. Times in the comments are milliseconds; <c>D</c> counts
/// each twice (ground 4 000, crossing 16 000, path 2 000, paved path 1 000).
/// </summary>
public sealed class PathFinderTests
{
    /// <summary>A finder with a map loaded from text (<see cref="MapFixture.City"/>) on a grid of the text's size.</summary>
    private static PathFinder Loaded(params string[] rows)
    {
        var finder = new PathFinder(MapFixture.Content(rows[0].Length, rows.Length));
        finder.Load(MapFixture.City(rows));
        return finder;
    }

    /// <summary>On open ground a walk goes straight: two steps of 4 000 ms each, <c>D</c> 16 000, 8 s.</summary>
    [Fact]
    public void Measure_OpenGroundInALine_IsTheStraightWalk()
    {
        PathFinder finder = Loaded("B.B", "...");

        WalkCost cost = finder.Measure(0, 0, 2, 0);

        Assert.Equal(new WalkCost(0, 16_000), cost);
        Assert.Equal(8, cost.Seconds(0));
        Assert.Equal([(0, 0), (1, 0), (2, 0)], finder.Route(0, 0, 2, 0));
    }

    /// <summary>The walk's own start and end buildings count as ground: one step between neighbours is one ground tile, 4 s.</summary>
    [Fact]
    public void Measure_AdjacentBuildings_CountAsGround()
    {
        PathFinder finder = Loaded("BB");

        Assert.Equal(new WalkCost(0, 8_000), finder.Measure(0, 0, 1, 0));
    }

    /// <summary>A step costs half the tile it leaves plus half the tile it enters: ground, path, path, ground is 3 000 + 2 000 + 3 000 ms.</summary>
    [Fact]
    public void Measure_AlongARoad_EachStepIsHalfOfEachTile()
    {
        PathFinder finder = Loaded("B11B");

        Assert.Equal(new WalkCost(0, 16_000), finder.Measure(0, 0, 3, 0));
    }

    /// <summary>
    /// A road that makes the walk faster is taken even with more tiles: the straight walk over ground is 3 steps (<c>D</c> 24 000); the detour
    /// over paved paths is 5 steps of <c>D</c> 5 000 + 2 000 + 2 000 + 2 000 + 5 000 = 16 000.
    /// </summary>
    [Fact]
    public void Measure_FasterRoadDetour_IsTakenOverTheShorterGroundWalk()
    {
        PathFinder finder = Loaded(
            "B..B",
            "2222");

        Assert.Equal(new WalkCost(0, 16_000), finder.Measure(0, 0, 3, 0));
        Assert.Equal([(0, 0), (0, 1), (1, 1), (2, 1), (3, 1), (3, 0)], finder.Route(0, 0, 3, 0));
    }

    /// <summary>A road that doesn't make the walk faster is left alone: a path beside a two-step ground walk only adds steps.</summary>
    [Fact]
    public void Measure_SlowerRoadDetour_IsNotTaken()
    {
        PathFinder finder = Loaded(
            "B.B",
            "111");

        Assert.Equal(new WalkCost(0, 16_000), finder.Measure(0, 0, 2, 0));
        Assert.Equal([(0, 0), (1, 0), (2, 0)], finder.Route(0, 0, 2, 0));
    }

    /// <summary>
    /// Another building is never crossed while a way around exists, even when crossing would be faster: through the wall is <c>D</c> 56 000
    /// with 1 crossing, round it through the gap at the bottom is 12 ground steps (<c>D</c> 96 000) with none.
    /// </summary>
    [Fact]
    public void Measure_WayAroundExists_NeverCrossesEvenWhenSlower()
    {
        PathFinder finder = Loaded(
            "B.B.B",
            "..B..",
            "..B..",
            "..B..",
            ".....");

        WalkCost cost = finder.Measure(0, 0, 4, 0);

        Assert.Equal(new WalkCost(0, 96_000), cost);
        Assert.DoesNotContain((2, 0), finder.Route(0, 0, 4, 0));
    }

    /// <summary>When every way crosses a building, the walk crosses as few as possible: the end is walled in by one ring, so one crossing.</summary>
    [Fact]
    public void Measure_EndWalledIn_CrossesExactlyOnce()
    {
        PathFinder finder = Loaded(
            "B....",
            ".BBB.",
            ".BBB.",
            ".BBB.",
            ".....");

        WalkCost cost = finder.Measure(0, 0, 2, 2);

        Assert.Equal(1, cost.Crossings);
    }

    /// <summary>
    /// Among walks with the fewest crossings the fastest wins. The end (2,2) sits inside a ring of buildings, so every walk crosses one; the
    /// way in from (2,0) comes over paved paths (<c>D</c> 5 000 + 2 000 + 17 000 + 20 000 = 44 000) and beats the way in from (0,2) over
    /// ground (<c>D</c> 8 000 + 8 000 + 20 000 + 20 000 = 56 000).
    /// </summary>
    [Fact]
    public void Measure_OnlyCrossingWays_TakesTheFastestOfThem()
    {
        PathFinder finder = Loaded(
            "B22..",
            ".BBB.",
            ".BBB.",
            ".BBB.",
            ".....");

        Assert.Equal(new WalkCost(1, 44_000), finder.Measure(0, 0, 2, 2));
        Assert.Equal([(0, 0), (1, 0), (2, 0), (2, 1), (2, 2)], finder.Route(0, 0, 2, 2));
    }

    /// <summary>
    /// The tie rule: among equally good walks, the one whose steps come first in the order x+1, y+1, x−1, y−1. From (0,0) to (1,1) on open
    /// ground both L-shaped walks cost the same; the one that steps x+1 first wins. Back from (1,1) to (0,0), x−1 comes before y−1.
    /// </summary>
    [Fact]
    public void Route_EqualWalks_TakesTheFirstInStepOrder()
    {
        PathFinder finder = Loaded("B.", ".B");

        Assert.Equal([(0, 0), (1, 0), (1, 1)], finder.Route(0, 0, 1, 1));
        Assert.Equal([(1, 1), (0, 1), (0, 0)], finder.Route(1, 1, 0, 0));
    }

    /// <summary>A walk from a tile to itself has no step and costs nothing; it still takes the minimum of one second.</summary>
    [Fact]
    public void Measure_SameTile_IsNoStep()
    {
        PathFinder finder = Loaded("B.");

        Assert.Equal(new WalkCost(0, 0), finder.Measure(0, 0, 0, 0));
        Assert.Equal([(0, 0)], finder.Route(0, 0, 0, 0));
        Assert.Equal(1, finder.Measure(0, 0, 0, 0).Seconds(0));
    }

    /// <summary>The map is read at <see cref="PathFinder.Load"/>: a road built afterwards counts only after the next load.</summary>
    [Fact]
    public void Load_AgainAfterTheMapChanged_UsesTheNewMap()
    {
        var finder = new PathFinder(MapFixture.Content(4, 1));
        finder.Load(MapFixture.City("B..B"));
        WalkCost before = finder.Measure(0, 0, 3, 0);

        finder.Load(MapFixture.City("B22B"));

        Assert.Equal(new WalkCost(0, 24_000), before);
        Assert.Equal(new WalkCost(0, 12_000), finder.Measure(0, 0, 3, 0));
    }

    /// <summary>The same map and walk give the same route and cost from a second finder (nothing depends on order of chance).</summary>
    [Fact]
    public void Route_SameMapTwice_IsIdentical()
    {
        string[] map = ["B.1.B", ".2.B.", "B...B"];

        IReadOnlyList<(int X, int Y)> first = Loaded(map).Route(0, 0, 4, 2);
        IReadOnlyList<(int X, int Y)> second = Loaded(map).Route(0, 0, 4, 2);

        Assert.Equal(first, second);
    }

    /// <summary>Searching before a city is loaded, or from or to a tile outside the grid, is a programmer error.</summary>
    [Fact]
    public void Measure_WithoutALoadOrOutsideTheGrid_Throws()
    {
        var finder = new PathFinder(MapFixture.Content(3, 3));
        Assert.Throws<InvalidOperationException>(() => finder.Measure(0, 0, 1, 1));

        finder.Load(MapFixture.City("...", "...", "..."));
        Assert.Throws<ArgumentOutOfRangeException>(() => finder.Measure(0, 0, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => finder.Route(-1, 0, 1, 0));
        Assert.Throws<ArgumentNullException>(() => finder.Load(null!));
        Assert.Throws<ArgumentNullException>(() => new PathFinder(null!));
    }

    /// <summary>A walk bonus shortens the seconds but never changes the route: 200 ‰ off <c>D</c> 16 000 is 12 800, 7 s.</summary>
    [Fact]
    public void Seconds_WithABonus_ScalesTheBestWalk()
    {
        PathFinder finder = Loaded("B.B");

        Assert.Equal(7, finder.Measure(0, 0, 2, 0).Seconds(200));
    }
}
