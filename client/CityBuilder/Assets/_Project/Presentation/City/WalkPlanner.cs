#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Plans the walks the map shows (CR-0008-T2 part D): the path between two buildings by the shared engine's path rule
    /// (<see cref="PathFinder"/>, <c>04</c> section 17), the seconds it takes with the city's walk bonus, and each step's share of the time
    /// (half of the tile left plus half of the tile entered, as the engine counts it). Display only. <see cref="Load"/> reads the map once per
    /// city; a path is planned only when a walker starts a new walk, so the cost is not paid every second.
    /// </summary>
    public sealed class WalkPlanner
    {
        /// <summary>The game content: grid size, ground and crossing times and road levels.</summary>
        private readonly GameContent _content;

        /// <summary>The engine's path finder.</summary>
        private readonly PathFinder _finder;

        /// <summary>The grid's width in tiles.</summary>
        private readonly int _width;

        /// <summary>The time of each tile as open ground or road, in milliseconds, by tile index.</summary>
        private readonly long[] _tileMillis;

        /// <summary>Whether a building stands on each tile, by tile index.</summary>
        private readonly bool[] _hasBuilding;

        /// <summary>The time of another building's tile, in milliseconds: the ground time at the crossing speed, rounded up.</summary>
        private readonly long _crossingMillis;

        /// <summary>The walk bonus of the loaded city, in permille.</summary>
        private int _bonusPermille;

        /// <summary>Whether <see cref="Load"/> ran.</summary>
        private bool _loaded;

        /// <summary>Creates a planner for the content's grid.</summary>
        /// <param name="content">The game content.</param>
        /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
        public WalkPlanner(GameContent content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _finder = new PathFinder(content);
            _width = content.Rules.GridWidth;
            int tiles = checked(_width * content.Rules.GridHeight);
            _tileMillis = new long[tiles];
            _hasBuilding = new bool[tiles];
            long scaled = checked(content.Rules.GroundTileMillis * 1000L);
            long speed = content.Rules.CrossingSpeedPermille;
            _crossingMillis = (scaled + speed - 1) / speed;
        }

        /// <summary>Reads the city's map (buildings and roads) and its walk bonus; call it before planning when the map may have changed.</summary>
        /// <param name="city">The city; it is only read.</param>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Load(CityState city)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            _finder.Load(city);
            _bonusPermille = CityQueries.WalkBonusPermille(city, _content);
            for (int i = 0; i < _tileMillis.Length; i++)
            {
                _tileMillis[i] = _content.Rules.GroundTileMillis;
                _hasBuilding[i] = false;
            }

            IReadOnlyList<RoadTile> roads = city.Roads;
            for (int i = 0; i < roads.Count; i++)
            {
                _tileMillis[(roads[i].Y * _width) + roads[i].X] = _content.GetRoadLevel(roads[i].Level).TileMillis;
            }

            IReadOnlyList<Building> buildings = city.Buildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                _hasBuilding[(buildings[i].Y * _width) + buildings[i].X] = true;
            }

            _loaded = true;
        }

        /// <summary>The seconds the best walk between two buildings takes, as the engine counts it (at least 1).</summary>
        /// <param name="from">Where the walk starts.</param>
        /// <param name="to">Where it ends.</param>
        /// <returns>The seconds.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Load"/> was not called.</exception>
        public long Seconds(Building from, Building to)
        {
            EnsureLoaded();
            return _finder.Measure(from.X, from.Y, to.X, to.Y).Seconds(_bonusPermille);
        }

        /// <summary>The path of the best walk between two buildings with each step's share of the time.</summary>
        /// <param name="from">Where the walk starts.</param>
        /// <param name="to">Where it ends.</param>
        /// <returns>The path.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Load"/> was not called.</exception>
        public WalkPath Path(Building from, Building to)
        {
            EnsureLoaded();
            IReadOnlyList<(int X, int Y)> tiles = _finder.Route(from.X, from.Y, to.X, to.Y);
            var weights = new long[tiles.Count - 1];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = TimeOf(tiles[i], tiles[0], tiles[tiles.Count - 1]) + TimeOf(tiles[i + 1], tiles[0], tiles[tiles.Count - 1]);
            }

            return new WalkPath(tiles, weights);
        }

        /// <summary>One tile's time in milliseconds: a building's tile other than the walk's own ends is a crossing; every other tile is ground or road.</summary>
        private long TimeOf((int X, int Y) tile, (int X, int Y) start, (int X, int Y) end)
        {
            int index = (tile.Y * _width) + tile.X;
            bool ends = tile == start || tile == end;
            return _hasBuilding[index] && !ends ? _crossingMillis : _tileMillis[index];
        }

        /// <summary>Throws when no map was loaded.</summary>
        private void EnsureLoaded()
        {
            if (!_loaded)
            {
                throw new InvalidOperationException("Load a city before planning walks.");
            }
        }
    }
}
