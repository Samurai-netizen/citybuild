using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Map;

/// <summary>
/// Maps for the path tests: the rule fixture's content (ground 4 000 ms, crossings at 250 ‰ so 16 000 ms, a path of 2 000 ms and a paved
/// path of 1 000 ms) on a grid of any size, and cities drawn from text, one row per line from y = 0 down: <c>.</c> ground, <c>B</c> a
/// building, <c>1</c> and <c>2</c> a road of that level.
/// </summary>
internal static class MapFixture
{
    /// <summary>The rule fixture's content with the grid set to <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static GameContent Content(int width, int height) => ContentAssert.Valid(ContentFixture.ValidWith(data =>
    {
        data.Rules!.GridWidth = width;
        data.Rules.GridHeight = height;
    }));

    /// <summary>
    /// The city a map text draws: its buildings get ids from 1 in reading order (row by row, left to right), all huts, all idle; its roads
    /// the levels written.
    /// </summary>
    public static CityState City(params string[] rows)
    {
        var buildings = new List<Building>();
        var roads = new List<RoadTile>();
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                switch (rows[y][x])
                {
                    case 'B': buildings.Add(Idle(buildings.Count + 1, "hut", x, y)); break;
                    case '1': roads.Add(new RoadTile(x, y, 1)); break;
                    case '2': roads.Add(new RoadTile(x, y, 2)); break;
                    case '.': break;
                    default: throw new ArgumentException($"Unknown map character '{rows[y][x]}'.", nameof(rows));
                }
            }
        }

        return State.CityFixture.City(buildings: [.. buildings], roads: [.. roads]);
    }
}
