#nullable enable
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// Trip arrivals (<c>core-instructions/04-simulation-model.md</c>, sections 6 and 18), step 3 of the order at one second, after building
    /// completions: trips that arrive at the same second do so in ascending trip id, each fully before the next. A trip's goods are delivered
    /// to its target (<see cref="Deliveries"/>); what is left goes back into the source's store at that second, even above its limit (a
    /// returned load); the trip ends and its worker is free. Only mutations start trips, so during an advance the set only shrinks, and the
    /// source keeps the next arrival instead of scanning the trips at every event.
    /// </summary>
    internal sealed class TripArrivalSource : IEventSource
    {
        /// <summary>The advance's building timers, which a delivery that starts a project updates.</summary>
        private readonly BuildingTimerQueue _timers;
        /// <summary>The earliest arrival of the trips under way, or null when there are none.</summary>
        private long? _nextDueAt;

        /// <summary>Creates the source for one advance of the city, over that advance's timer queue.</summary>
        public TripArrivalSource(CityState city, BuildingTimerQueue timers)
        {
            _timers = timers;
            _nextDueAt = EarliestArrival(city);
        }

        /// <inheritdoc/>
        public long? NextDueAt(CityState city) => _nextDueAt;

        /// <inheritdoc/>
        public void CompleteDue(CityState city, GameContent content, long time, SettlementReportBuilder report)
        {
            if (_nextDueAt != time)
            {
                return;
            }

            // The trips are sorted by id, so walking the list keeps the order of 04 section 6; an arrived trip leaves the list in place.
            IReadOnlyList<Trip> trips = city.Trips;
            int index = 0;
            while (index < trips.Count)
            {
                Trip trip = trips[index];
                if (trip.ArrivesAt != time)
                {
                    index++;
                    continue;
                }

                long left = Deliveries.Deliver(city, content, city.IndexOfBuilding(trip.TargetId), trip.Item, trip.Quantity, time, report, _timers);
                if (left > 0)
                {
                    city.Buildings[city.IndexOfBuilding(trip.SourceId)].AddToStore(trip.Item, left);
                }

                city.RemoveTripAt(index);
            }

            _nextDueAt = EarliestArrival(city);
        }

        /// <summary>The earliest arrival of the city's trips, or null when it has none.</summary>
        private static long? EarliestArrival(CityState city)
        {
            long? earliest = null;
            IReadOnlyList<Trip> trips = city.Trips;
            for (int index = 0; index < trips.Count; index++)
            {
                if (!earliest.HasValue || trips[index].ArrivesAt < earliest.Value)
                {
                    earliest = trips[index].ArrivesAt;
                }
            }

            return earliest;
        }
    }
}
