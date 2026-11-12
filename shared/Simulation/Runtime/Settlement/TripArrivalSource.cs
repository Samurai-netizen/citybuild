#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The legs of workers' deliveries (<c>core-instructions/04-simulation-model.md</c>, sections 6 and 18, CR-0008 D21), step 3 of the order at
    /// one second, after building completions: trips whose leg ends at the same second do so in ascending trip id, each fully before the next.
    /// At the target the load is delivered (<see cref="Deliveries"/>) and what is left goes back into the source's store at that second, even
    /// above its limit (a returned load); with goods still to fetch the worker walks back to the source, otherwise the trip ends and its worker
    /// is free. At the source the worker takes the next load, at most the rest, the stock and the trip carry limit, and walks to the target;
    /// with none to take the trip ends. A leg lasts at least 1 s, so a trip never comes up twice at one second; the source keeps the next end
    /// of a leg instead of scanning the trips at every event.
    /// </summary>
    internal sealed class TripArrivalSource : IEventSource
    {
        /// <summary>The advance's building timers, which a delivery that starts a project updates.</summary>
        private readonly BuildingTimerQueue _timers;

        /// <summary>The walk times between buildings, searched once per pair (shared with the couriers).</summary>
        private readonly WalkTimes _walks;

        /// <summary>The earliest end of a leg of the trips under way, or null when there are none.</summary>
        private long? _nextDueAt;

        /// <summary>Creates the source for one advance of the city, over that advance's timer queue and walk times.</summary>
        public TripArrivalSource(CityState city, BuildingTimerQueue timers, WalkTimes walks)
        {
            _timers = timers;
            _walks = walks;
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

            // The trips are sorted by id, so walking the list keeps the order of 04 section 6; an ended trip leaves the list in place, and one
            // that walks on now ends its next leg at least 1 s later.
            IReadOnlyList<Trip> trips = city.Trips;
            int index = 0;
            while (index < trips.Count)
            {
                Trip trip = trips[index];
                if (trip.ArrivesAt == time && !EndLeg(city, content, trip, time, report))
                {
                    city.RemoveTripAt(index);
                    continue;
                }

                index++;
            }

            _nextDueAt = EarliestArrival(city);
        }

        /// <summary>Ends the current leg of <paramref name="trip"/> at <paramref name="time"/>; returns whether the worker walks on (false: the trip ends).</summary>
        private bool EndLeg(CityState city, GameContent content, Trip trip, long time, SettlementReportBuilder report)
        {
            int source = city.IndexOfBuilding(trip.SourceId);
            int target = city.IndexOfBuilding(trip.TargetId);
            if (trip.Leg == TripLeg.ToTarget)
            {
                long left = Deliveries.Deliver(city, content, target, trip.Item, trip.Quantity, time, report, _timers);
                if (left > 0)
                {
                    city.Buildings[source].AddToStore(trip.Item, left);
                }

                if (trip.Remaining < 1)
                {
                    return false;
                }

                trip.WalkBack(time, checked(time + _walks.Seconds(target, source)));
                return true;
            }

            Building store = city.Buildings[source];
            long load = Math.Min(Math.Min(trip.Remaining, store.GetStock(trip.Item)), content.Rules.TripCarryLimit);
            if (load < 1)
            {
                return false;
            }

            store.TakeFromStore(trip.Item, load);
            trip.CarryOn(load, time, checked(time + _walks.Seconds(source, target)));
            return true;
        }

        /// <summary>The earliest end of a leg of the city's trips, or null when it has none.</summary>
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
