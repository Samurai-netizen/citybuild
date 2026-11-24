#nullable enable
using System;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The couriers' walks of one advance (<c>core-instructions/04-simulation-model.md</c>, sections 7 and 17): the map changes inside an advance only
    /// when a research completes (it may open land, M1-03), so the path rule's cost between two buildings is searched once per pair and kept until then (CR-0008-T1 decision 46). The path finder and
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

        /// <summary>Whether each searched pair of <see cref="_costs"/> has a walk at all (M1-03: water or mist may lie between).</summary>
        private bool[]? _joined;

        /// <summary>How many research nodes were complete when the map was loaded: a completion may open land (M1-03), so the map is read again.</summary>
        private int _loadedResearch;

        /// <summary>Creates the table for one advance of the city.</summary>
        public WalkTimes(CityState city, GameContent content)
        {
            _city = city;
            _content = content;
        }

        /// <summary>
        /// The seconds of a walk that starts now from the building at <paramref name="fromIndex"/> to the one at <paramref name="toIndex"/> (indexes
        /// in <see cref="CityState.Buildings"/>): the path rule's cost with the city's walk bonus now, at least 1 s (<c>04</c> section 17). False
        /// when no walk joins them (M1-03: water without a bridge, land under mist); the walker then waits (decision 4).
        /// </summary>
        public bool TrySeconds(int fromIndex, int toIndex, out long seconds)
        {
            int count = _city.Buildings.Count;
            if (_paths is null)
            {
                _paths = new PathFinder(_content);
                _costs = new WalkCost[count * count];
                _known = new bool[count * count];
                _joined = new bool[count * count];
                Reload();
            }
            else if (_city.CompletedResearch.Count != _loadedResearch)
            {
                // A research completed in this advance and may have opened land: every walk is searched again on the new map.
                Reload();
            }

            // _costs, _known and _joined are made together with _paths just above.
            int pair = (fromIndex * count) + toIndex;
            if (!_known![pair])
            {
                Building from = _city.Buildings[fromIndex];
                Building to = _city.Buildings[toIndex];
                _joined![pair] = _paths.TryMeasure(from.X, from.Y, to.X, to.Y, out _costs![pair]);
                _known[pair] = true;
            }

            seconds = _joined![pair] ? _costs![pair].Seconds(CityQueries.WalkBonusPermille(_city, _content)) : 0;
            return _joined[pair];
        }

        /// <summary>Reads the city's map into the finder and forgets every searched pair.</summary>
        private void Reload()
        {
            _paths!.Load(_city);
            Array.Clear(_known!, 0, _known!.Length);
            _loadedResearch = _city.CompletedResearch.Count;
        }
    }
}
