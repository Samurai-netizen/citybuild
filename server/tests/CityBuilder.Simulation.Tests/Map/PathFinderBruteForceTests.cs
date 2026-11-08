using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Properties;

namespace CityBuilder.Simulation.Tests.Map;

/// <summary>
/// The path rule checked against brute force (<c>core-instructions/04-simulation-model.md</c>, section 17): on small random maps every
/// simple walk between two tiles is listed, and the finder's walk must have the fewest crossings of any of them (so it crosses a building
/// only when every walk does), the least <c>D</c> among those, and be the first of those in step order. A best walk never visits a tile
/// twice (that would only add cost), so simple walks are all there is to compare.
/// </summary>
public sealed class PathFinderBruteForceTests
{
    /// <summary>The steps in the tie rule's order: x+1, y+1, x−1, y−1.</summary>
    private static readonly (int X, int Y)[] _steps = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    /// <summary>The seeds of the random maps, each a 4 × 4 grid (the 3 × 3 and 5 × 3 cases below vary the shape).</summary>
    public static TheoryData<int> Seeds { get; } = [.. Enumerable.Range(0, 60)];

    /// <summary>On a random 4 × 4 map, for random pairs of tiles, the finder's walk is the brute-force best by the path rule and its tie rule.</summary>
    /// <param name="seed">The map's seed.</param>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Route_RandomMap_IsTheBruteForceBest(int seed) => CheckRandomMap(seed, 4, 4);

    /// <summary>The same on 3 × 3 and 5 × 3 maps, where walls cut the grid more often.</summary>
    /// <param name="seed">The map's seed.</param>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Route_RandomNarrowMaps_IsTheBruteForceBest(int seed)
    {
        CheckRandomMap(seed + 1000, 3, 3);
        CheckRandomMap(seed + 2000, 5, 3);
    }

    /// <summary>
    /// Draws a map (about a third buildings, a fifth paths, a tenth paved paths), then checks six random pairs of tiles: the finder's cost and
    /// route against the best of every simple walk.
    /// </summary>
    private static void CheckRandomMap(int seed, int width, int height)
    {
        var random = new SeededRandom(seed);
        string[] rows = new string[height];
        for (int y = 0; y < height; y++)
        {
            char[] row = new char[width];
            for (int x = 0; x < width; x++)
            {
                long draw = random.Between(1, 100);
                row[x] = draw <= 33 ? 'B' : draw <= 53 ? '1' : draw <= 63 ? '2' : '.';
            }

            rows[y] = new string(row);
        }

        GameContent content = MapFixture.Content(width, height);
        CityState city = MapFixture.City(rows);
        var finder = new PathFinder(content);
        finder.Load(city);
        for (int pair = 0; pair < 6; pair++)
        {
            (int X, int Y) from = (random.Index(width), random.Index(height));
            (int X, int Y) to = (random.Index(width), random.Index(height));
            (WalkCost cost, List<(int X, int Y)> route) = BruteForceBest(content, rows, from, to);
            string context = $"seed {seed}, map {string.Join("/", rows)}, from {from} to {to}";

            Assert.True(cost == finder.Measure(from.X, from.Y, to.X, to.Y), $"{context}: expected {cost}, got {finder.Measure(from.X, from.Y, to.X, to.Y)}");
            Assert.True(route.SequenceEqual(finder.Route(from.X, from.Y, to.X, to.Y)), $"{context}: expected {string.Join(" ", route)}, got {string.Join(" ", finder.Route(from.X, from.Y, to.X, to.Y))}");
        }
    }

    /// <summary>
    /// Every simple walk from <paramref name="from"/> to <paramref name="to"/>, tried in step order so the first best one found is the first in
    /// step order, scored by the rule's own definitions (start and end are ground, another building a crossing at 16 000 ms).
    /// </summary>
    private static (WalkCost Cost, List<(int X, int Y)> Route) BruteForceBest(GameContent content, string[] rows, (int X, int Y) from, (int X, int Y) to)
    {
        int width = rows[0].Length;
        int height = rows.Length;
        long ground = content.Rules.GroundTileMillis;
        long crossing = IntegerMath.CeilDiv(ground * 1000, content.Rules.CrossingSpeedPermille);

        // The time of a tile for this walk and whether entering it is a crossing, from the map text.
        (long Millis, bool IsCrossing) Tile((int X, int Y) tile) => tile == from || tile == to
            ? (ground, false)
            : rows[tile.Y][tile.X] switch
            {
                'B' => (crossing, true),
                '1' => (content.GetRoadLevel(1).TileMillis, false),
                '2' => (content.GetRoadLevel(2).TileMillis, false),
                _ => (ground, false),
            };

        WalkCost? best = null;
        List<(int X, int Y)>? bestRoute = null;
        var visited = new HashSet<(int X, int Y)> { from };
        var walk = new List<(int X, int Y)> { from };

        // Depth-first over simple walks, keeping the first walk of the best cost (strictly better replaces it).
        void Extend((int X, int Y) at, int crossings, long doubleMillis)
        {
            if (at == to)
            {
                var cost = new WalkCost(crossings, doubleMillis);
                if (best is null || IsBetter(cost, best.Value))
                {
                    best = cost;
                    bestRoute = [.. walk];
                }

                return;
            }

            foreach ((int stepX, int stepY) in _steps)
            {
                (int X, int Y) next = (at.X + stepX, at.Y + stepY);
                if (next.X < 0 || next.Y < 0 || next.X >= width || next.Y >= height || !visited.Add(next))
                {
                    continue;
                }

                walk.Add(next);
                (long nextMillis, bool isCrossing) = Tile(next);
                Extend(next, crossings + (isCrossing ? 1 : 0), doubleMillis + Tile(at).Millis + nextMillis);
                walk.RemoveAt(walk.Count - 1);
                visited.Remove(next);
            }
        }

        Extend(from, 0, 0);
        return (best!.Value, bestRoute!);
    }

    /// <summary>Whether one cost beats another by the path rule: fewer crossings, then less <c>D</c>.</summary>
    private static bool IsBetter(WalkCost cost, WalkCost other) =>
        cost.Crossings != other.Crossings ? cost.Crossings < other.Crossings : cost.DoubleMillis < other.DoubleMillis;
}
