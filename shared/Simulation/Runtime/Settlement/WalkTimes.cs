#nullable enable
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The couriers' walks of one advance (<c>core-instructions/04-simulation-model.md</c>, sections 7 and 17): the map doesn't change inside an
    /// advance, so the path rule's cost between two buildings is searched once per pair and kept (CR-0008-T1 decision 46). The path finder and
    /// the table are made on the first walk, so an advance without couriers allocates neither. The walk bonus is read at each departure, so a
    /// walk that starts after a bonus research completes is faster, and a walk keeps the time it started with.
    /// </summary>
    internal sealed class WalkTimes
    {
        /// <summary>The advance's city; its buildings and roads don't change during the advance.</summary>
        private readonly CityState _city;

        /// <summary>The game content (tile times, the walk bonus effects).</summary>
        private readonly GameContent _content;

        /// <summary>The path finder over the city's map, made on the first walk.</summary>
        private PathFinder? _paths;

        /// <summary>The cost of each searched pair, by <c>from × buildings + to</c> (building indexes); valid where <see cref="_known"/> is set.</summary>
        private WalkCost[]? _costs;

        /// <summary>Which pairs of <see cref="_costs"/> have been searched.</summary>
        private bool[]? _known;

        /// <summary>Creates the table for one advance of the city.</summary>
        public WalkTimes(CityState city, GameContent content)
        {
            _city = city;
            _content = content;
        }

        /// <summary>
        /// The seconds of a walk that starts now from the building at <paramref name="fromIndex"/> to the one at <paramref name="toIndex"/> (indexes
        /// in <see cref="CityState.Buildings"/>): the path rule's cost with the city's walk bonus now, at least 1 s (<c>04</c> section 17).
        /// </summary>
        public long Seconds(int fromIndex, int toIndex)
        {
            int count = _city.Buildings.Count;
            if (_paths is null)
            {
                _paths = new PathFinder(_content);
                _paths.Load(_city);
                _costs = new WalkCost[count * count];
                _known = new bool[count * count];
            }

            // _costs and _known are made together with _paths just above.
            int pair = (fromIndex * count) + toIndex;
            if (!_known![pair])
            {
                Building from = _city.Buildings[fromIndex];
                Building to = _city.Buildings[toIndex];
                _costs![pair] = _paths.Measure(from.X, from.Y, to.X, to.Y);
                _known[pair] = true;
            }

            return _costs![pair].Seconds(CityQueries.WalkBonusPermille(_city, _content));
        }
    }
}
