#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Map
{
    /// <summary>
    /// Finds walks on a city's map by the path rule of <c>core-instructions/04-simulation-model.md</c>, section 17: steps of one tile in four
    /// directions inside the grid; among all walks the fewest crossings of other buildings, then the least <c>D</c>; among those the first in
    /// step order (x+1, y+1, x−1, y−1). A search runs from the end (Dijkstra over the reversed steps), so every tile knows its best cost to the
    /// end; the path then follows, from the start, the first direction that stays on a best walk.
    /// The finder keeps its arrays for its whole life: make one, <see cref="Load"/> a city, then measure as many walks as needed without
    /// allocating (the map of one advance never changes, <c>04</c> section 7). It is not thread-safe.
    /// </summary>
    public sealed class PathFinder
    {
        /// <summary>The column change of each direction, in the tie rule's order: x+1, y+1, x−1, y−1.</summary>
        private static readonly int[] _stepX = { 1, 0, -1, 0 };
        /// <summary>The row change of each direction, in the tie rule's order.</summary>
        private static readonly int[] _stepY = { 0, 1, 0, -1 };

        /// <summary>The content whose walking rules and road levels the tile times come from.</summary>
        private readonly GameContent _content;
        /// <summary>The grid's width in tiles.</summary>
        private readonly int _width;
        /// <summary>The grid's height in tiles.</summary>
        private readonly int _height;
        /// <summary>Each tile's time in milliseconds as open ground or road, by tile index <c>y × width + x</c>; a building's tile holds ground.</summary>
        private readonly long[] _tileMillis;
        /// <summary>Whether a building stands on each tile, by tile index.</summary>
        private readonly bool[] _hasBuilding;
        /// <summary>The best crossings from each tile to the current search's end; valid where <see cref="_settled"/> is set.</summary>
        private readonly int[] _bestCrossings;
        /// <summary>The best <c>D</c> from each tile to the current search's end; valid where <see cref="_settled"/> is set.</summary>
        private readonly long[] _bestDouble;
        /// <summary>Whether a tile's best cost to the end is final in the current search.</summary>
        private readonly bool[] _settled;
        /// <summary>Whether a tile has a tentative cost in the current search.</summary>
        private readonly bool[] _reached;
        /// <summary>The search's queue: tile indices with their tentative cost when pushed (a tile may be in it more than once).</summary>
        private readonly int[] _heapTile;
        /// <summary>The crossings each queue entry was pushed with.</summary>
        private readonly int[] _heapCrossings;
        /// <summary>The <c>D</c> each queue entry was pushed with.</summary>
        private readonly long[] _heapDouble;
        /// <summary>How many entries the queue holds.</summary>
        private int _heapCount;
        /// <summary>The time of another building's tile: the ground time at the crossing speed, rounded up.</summary>
        private readonly long _crossingMillis;
        /// <summary>Whether <see cref="Load"/> has read a city.</summary>
        private bool _loaded;
        /// <summary>The start tile of the last search: it counts as ground, never as a crossing.</summary>
        private int _searchStart;
        /// <summary>The end tile of the last search: it counts as ground, never as a crossing.</summary>
        private int _searchEnd;

        /// <summary>Creates a finder for the content's grid; it allocates its arrays once, here.</summary>
        /// <param name="content">The game content: grid size, ground time, crossing speed and road levels.</param>
        /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
        public PathFinder(GameContent content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _width = content.Rules.GridWidth;
            _height = content.Rules.GridHeight;
            int tiles = checked(_width * _height);
            _tileMillis = new long[tiles];
            _hasBuilding = new bool[tiles];
            _bestCrossings = new int[tiles];
            _bestDouble = new long[tiles];
            _settled = new bool[tiles];
            _reached = new bool[tiles];

            // Each tile is pushed at most once per neighbour that improves it, so four pushes per tile bound the queue.
            int capacity = checked((tiles * 4) + 1);
            _heapTile = new int[capacity];
            _heapCrossings = new int[capacity];
            _heapDouble = new long[capacity];
            _crossingMillis = IntegerMath.CeilDiv(
                checked(content.Rules.GroundTileMillis * IntegerMath.PermilleScale), content.Rules.CrossingSpeedPermille);
        }

        /// <summary>
        /// Reads the city's map: which tiles have a building and the time of every other tile (ground or its road's level). Call it again after
        /// the map changed (a building placed, a road built, upgraded or removed); a search uses the map as last loaded.
        /// </summary>
        /// <param name="city">The city; it is only read.</param>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Load(CityState city)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            long ground = _content.Rules.GroundTileMillis;
            for (int index = 0; index < _tileMillis.Length; index++)
            {
                _tileMillis[index] = ground;
                _hasBuilding[index] = false;
            }

            IReadOnlyList<RoadTile> roads = city.Roads;
            for (int index = 0; index < roads.Count; index++)
            {
                RoadTile road = roads[index];
                _tileMillis[IndexOf(road.X, road.Y)] = _content.GetRoadLevel(road.Level).TileMillis;
            }

            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                _hasBuilding[IndexOf(buildings[index].X, buildings[index].Y)] = true;
            }

            _loaded = true;
        }

        /// <summary>
        /// The cost of the best walk from one tile to another on the loaded map (the path rule): its crossings and <c>D</c>. The two tiles count
        /// as ground even when a building stands on them (the walk's own start and end). Allocates nothing.
        /// </summary>
        /// <param name="fromX">The start tile's column.</param>
        /// <param name="fromY">The start tile's row.</param>
        /// <param name="toX">The end tile's column.</param>
        /// <param name="toY">The end tile's row.</param>
        /// <returns>The best walk's cost; zero crossings and 0 when the tiles are the same.</returns>
        /// <exception cref="InvalidOperationException">No city was loaded.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A tile is outside the grid.</exception>
        public WalkCost Measure(int fromX, int fromY, int toX, int toY)
        {
            int start = Search(fromX, fromY, toX, toY);
            return new WalkCost(_bestCrossings[start], _bestDouble[start]);
        }

        /// <summary>
        /// The tiles of the best walk from one tile to another on the loaded map, start and end included, in walking order (the path rule and
        /// its tie rule). For display and tests: it allocates the list it returns.
        /// </summary>
        /// <param name="fromX">The start tile's column.</param>
        /// <param name="fromY">The start tile's row.</param>
        /// <param name="toX">The end tile's column.</param>
        /// <param name="toY">The end tile's row.</param>
        /// <returns>The tiles, at least the start; each next to the one before.</returns>
        /// <exception cref="InvalidOperationException">No city was loaded.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A tile is outside the grid.</exception>
        public IReadOnlyList<(int X, int Y)> Route(int fromX, int fromY, int toX, int toY)
        {
            int current = Search(fromX, fromY, toX, toY);
            var tiles = new List<(int X, int Y)> { (fromX, fromY) };
            while (current != _searchEnd)
            {
                current = NextOnABestWalk(current);
                tiles.Add((current % _width, current / _width));
            }

            return tiles.AsReadOnly();
        }

        /// <summary>
        /// Runs the search from the end until the start's best cost is final; returns the start's tile index. Every tile on a best walk from the
        /// start has a smaller cost (each step costs more than nothing), so it is final too when the search stops.
        /// </summary>
        private int Search(int fromX, int fromY, int toX, int toY)
        {
            if (!_loaded)
            {
                throw new InvalidOperationException("Load a city before searching its map.");
            }

            int start = IndexOf(fromX, fromY);
            int end = IndexOf(toX, toY);
            _searchStart = start;
            _searchEnd = end;
            for (int index = 0; index < _settled.Length; index++)
            {
                _settled[index] = false;
                _reached[index] = false;
            }

            _heapCount = 0;
            Offer(end, 0, 0);
            while (_heapCount > 0)
            {
                Pop(out int tile, out int crossings, out long doubleMillis);
                if (_settled[tile])
                {
                    continue;
                }

                _settled[tile] = true;
                if (tile == start)
                {
                    return start;
                }

                // The reversed step: a walker on a neighbour steps onto this tile.
                int x = tile % _width;
                int y = tile / _width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int neighbourX = x + _stepX[direction];
                    int neighbourY = y + _stepY[direction];
                    if (neighbourX < 0 || neighbourY < 0 || neighbourX >= _width || neighbourY >= _height)
                    {
                        continue;
                    }

                    int neighbour = (neighbourY * _width) + neighbourX;
                    if (!_settled[neighbour])
                    {
                        Offer(
                            neighbour,
                            checked(crossings + CrossingsEntering(tile)),
                            checked(doubleMillis + TimeOf(neighbour) + TimeOf(tile)));
                    }
                }
            }

            throw new InvalidOperationException("The grid is connected, so the search reaches every tile; this is a bug in the finder.");
        }

        /// <summary>
        /// The tile after <paramref name="current"/> on the best walk of the last search: the first direction in the tie rule's order whose tile
        /// is final and whose best cost plus the step's cost is <paramref name="current"/>'s best cost. Following it from the start never comes
        /// back to the start (that would make a cheaper walk from the start), so the times stay those of the search.
        /// </summary>
        private int NextOnABestWalk(int current)
        {
            int x = current % _width;
            int y = current / _width;
            for (int direction = 0; direction < 4; direction++)
            {
                int nextX = x + _stepX[direction];
                int nextY = y + _stepY[direction];
                if (nextX < 0 || nextY < 0 || nextX >= _width || nextY >= _height)
                {
                    continue;
                }

                int next = (nextY * _width) + nextX;
                if (!_settled[next])
                {
                    continue;
                }

                int crossings = checked(_bestCrossings[next] + CrossingsEntering(next));
                long doubleMillis = checked(_bestDouble[next] + TimeOf(current) + TimeOf(next));
                if (crossings == _bestCrossings[current] && doubleMillis == _bestDouble[current])
                {
                    return next;
                }
            }

            throw new InvalidOperationException("A tile with a final cost always has a next tile on a best walk; this is a bug in the finder.");
        }

        /// <summary>1 when stepping onto <paramref name="tile"/> crosses another building (not the search's start or end), else 0.</summary>
        private int CrossingsEntering(int tile) => _hasBuilding[tile] && tile != _searchStart && tile != _searchEnd ? 1 : 0;

        /// <summary>The time of a tile in milliseconds for the search's walk: its start and end are ground, another building a crossing.</summary>
        private long TimeOf(int tile)
        {
            if (tile == _searchStart || tile == _searchEnd)
            {
                return _content.Rules.GroundTileMillis;
            }

            return _hasBuilding[tile] ? _crossingMillis : _tileMillis[tile];
        }

        /// <summary>The index of a tile in the arrays, <c>y × width + x</c>; throws for a tile outside the grid.</summary>
        private int IndexOf(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), FormattableString.Invariant($"The tile ({x}, {y}) is outside the {_width} × {_height} grid."));
            }

            return (y * _width) + x;
        }

        /// <summary>Records a tentative cost for a tile when it is better than the one it has, and queues it.</summary>
        private void Offer(int tile, int crossings, long doubleMillis)
        {
            if (_reached[tile] && !IsBetter(crossings, doubleMillis, _bestCrossings[tile], _bestDouble[tile]))
            {
                return;
            }

            _reached[tile] = true;
            _bestCrossings[tile] = crossings;
            _bestDouble[tile] = doubleMillis;
            Push(tile, crossings, doubleMillis);
        }

        /// <summary>Whether one cost beats another by the path rule: fewer crossings, then less <c>D</c>.</summary>
        private static bool IsBetter(int crossings, long doubleMillis, int otherCrossings, long otherDouble) =>
            crossings != otherCrossings ? crossings < otherCrossings : doubleMillis < otherDouble;

        /// <summary>Adds an entry to the binary min-heap, ordered by cost and then by tile index (so the order never depends on chance).</summary>
        private void Push(int tile, int crossings, long doubleMillis)
        {
            int child = _heapCount++;
            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (!Precedes(tile, crossings, doubleMillis, parent))
                {
                    break;
                }

                MoveEntry(parent, child);
                child = parent;
            }

            _heapTile[child] = tile;
            _heapCrossings[child] = crossings;
            _heapDouble[child] = doubleMillis;
        }

        /// <summary>Takes the cheapest entry off the heap.</summary>
        private void Pop(out int tile, out int crossings, out long doubleMillis)
        {
            tile = _heapTile[0];
            crossings = _heapCrossings[0];
            doubleMillis = _heapDouble[0];
            _heapCount--;
            int lastTile = _heapTile[_heapCount];
            int lastCrossings = _heapCrossings[_heapCount];
            long lastDouble = _heapDouble[_heapCount];
            int parent = 0;
            while (true)
            {
                int child = (parent * 2) + 1;
                if (child >= _heapCount)
                {
                    break;
                }

                if (child + 1 < _heapCount && Precedes(_heapTile[child + 1], _heapCrossings[child + 1], _heapDouble[child + 1], child))
                {
                    child++;
                }

                if (!Precedes(_heapTile[child], _heapCrossings[child], _heapDouble[child], lastTile, lastCrossings, lastDouble))
                {
                    break;
                }

                MoveEntry(child, parent);
                parent = child;
            }

            _heapTile[parent] = lastTile;
            _heapCrossings[parent] = lastCrossings;
            _heapDouble[parent] = lastDouble;
        }

        /// <summary>Whether an entry comes before the heap's entry at <paramref name="slot"/>.</summary>
        private bool Precedes(int tile, int crossings, long doubleMillis, int slot) =>
            Precedes(tile, crossings, doubleMillis, _heapTile[slot], _heapCrossings[slot], _heapDouble[slot]);

        /// <summary>Whether one entry comes before another: cheaper by the path rule, or as cheap with a lower tile index.</summary>
        private static bool Precedes(int tile, int crossings, long doubleMillis, int otherTile, int otherCrossings, long otherDouble) =>
            crossings != otherCrossings || doubleMillis != otherDouble
                ? IsBetter(crossings, doubleMillis, otherCrossings, otherDouble)
                : tile < otherTile;

        /// <summary>Copies the heap entry at <paramref name="from"/> to <paramref name="to"/>.</summary>
        private void MoveEntry(int from, int to)
        {
            _heapTile[to] = _heapTile[from];
            _heapCrossings[to] = _heapCrossings[from];
            _heapDouble[to] = _heapDouble[from];
        }
    }
}
