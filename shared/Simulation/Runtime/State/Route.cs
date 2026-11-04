#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// The route of one slot of a courier station (<c>core-instructions/04-simulation-model.md</c>, section 19): 2 or more stops, walked in a
    /// loop (after the last, the first). Two consecutive stops, the last and the first included, are at different buildings. A route belongs
    /// to the slot, whether or not a courier holds it (CR-0008-T1 decision 48); <c>SetRoute</c> replaces it. A value.
    /// </summary>
    public sealed class Route
    {
        /// <summary>Creates a route.</summary>
        /// <param name="stationId">The courier station's building id, at least 1.</param>
        /// <param name="slot">The slot, at least 1.</param>
        /// <param name="stops">The stops in order, at least 2; the content's most is <c>SetRoute</c>'s check.</param>
        /// <exception cref="ArgumentNullException">The list or a stop is null.</exception>
        /// <exception cref="ArgumentException">The values don't form a route.</exception>
        public Route(int stationId, int slot, IEnumerable<RouteStop> stops)
        {
            if (stops is null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            var list = new List<RouteStop>();
            foreach (RouteStop stop in stops)
            {
                list.Add(stop ?? throw new ArgumentNullException(nameof(stops), "A stop is null."));
            }

            if (stationId < 1 || slot < 1 || list.Count < 2 || HasRepeatedNeighbour(list))
            {
                throw new ArgumentException(FormattableString.Invariant(
                    $"Route {stationId}/{slot}: the station and the slot are at least 1, and 2 or more stops, no two consecutive ones (the last and the first included) at one building."));
            }

            StationId = stationId;
            Slot = slot;
            Stops = list.AsReadOnly();
        }

        /// <summary>The courier station's building id.</summary>
        public int StationId { get; }

        /// <summary>The slot in the station.</summary>
        public int Slot { get; }

        /// <summary>The stops in order; after the last comes the first.</summary>
        public IReadOnlyList<RouteStop> Stops { get; }

        /// <summary>Whether two consecutive stops of the loop, the last and the first included, are at one building (<c>INVALID_ROUTE</c>).</summary>
        internal static bool HasRepeatedNeighbour(IReadOnlyList<RouteStop> stops)
        {
            for (int index = 0; index < stops.Count; index++)
            {
                if (stops[index].BuildingId == stops[(index + 1) % stops.Count].BuildingId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
