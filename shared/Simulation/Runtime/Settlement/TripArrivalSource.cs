#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The legs of workers' deliveries (<c>core-instructions/04-simulation-model.md</c>, sections 6 and 18, CR-0008 D21), step 3 of the order at
    /// one second, after building completions: trips whose leg ends at the same second do so in ascending trip id, each fully before the next.
    /// At the target the load is delivered (<see cref="Deliveries"/>); what is left ends the sending and the worker carries it back to the
    /// source, puts back what fits and waits there with the rest (<see cref="TripLeg.Waiting"/>, M0-C-3); with nothing left and goods still to
    /// fetch the worker walks back to the source, otherwise the trip ends and its worker is free. At the source the worker takes the next
    /// load, at most the rest, the stock and the trip carry limit, and walks to the target; with none to take the trip ends. A waiting trip
    /// has no leg to end: <see cref="Rules.HeldLoads"/> puts its goods back as room appears. A leg lasts at least 1 s, so a trip never comes up twice at one second; the source keeps the next end
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
                if ((trip.Leg == TripLeg.ToTarget || trip.Leg == TripLeg.ToSource) && trip.ArrivesAt == time && !EndLeg(city, content, trip, time, report))
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
                if (left < 1 && trip.Remaining < 1)
                {
                    return false;
                }

                if (!_walks.TrySeconds(target, source, out long back))
                {
                    // No walk back (a removed bridge, M1-03 decision 4): the worker waits at the target with what is left.
                    trip.BlockAtTarget(left);
                    return true;
                }

                if (left > 0)
                {
                    // The target is full: the sending ends and the worker carries the rest back (M0-C-3; never above the source's limit).
                    trip.CarryBack(left, time, checked(time + back));
                }
                else
                {
                    trip.WalkBack(time, checked(time + back));
                }

                return true;
            }

            if (trip.Quantity > 0)
            {
                // Back with goods the target didn't take: what fits goes into the store; with some left the worker waits there.
                if (HeldLoads.PutBack(city, content, trip))
                {
                    return false;
                }

                trip.Wait();
                return true;
            }

            return FetchNext(city, content, trip, source, target, time);
        }

        /// <summary>
        /// The worker at the source takes the next load, at most the rest, the stock and the trip carry limit, and walks to the target; returns
        /// false when there is none to take (the trip ends). With no walk to the target (M1-03 decision 4) it takes nothing and waits there.
        /// </summary>
        private bool FetchNext(CityState city, GameContent content, Trip trip, int source, int target, long time)
        {
            Building store = city.Buildings[source];
            long load = Math.Min(Math.Min(trip.Remaining, store.GetStock(trip.Item)), content.Rules.TripCarryLimit);
            if (load < 1)
            {
                return false;
            }

            if (!_walks.TrySeconds(source, target, out long walk))
            {
                if (trip.Leg != TripLeg.BlockedAtSource)
                {
                    trip.BlockAtSource();
                }

                return true;
            }

            store.TakeFromStore(trip.Item, load);
            trip.CarryOn(load, time, checked(time + walk));
            return true;
        }

        /// <summary>
        /// Blocked workers try again at <paramref name="time"/> (the start pass, M1-03 decision 4), in ascending trip id: one waiting at the
        /// target walks back once a walk joins it to the source; one waiting at the source takes its load and walks once a walk joins it to the
        /// target, or ends its trip when nothing is left to take. The map changes only by a mutation (before a zero-length advance) or a
        /// research completion, both at a pass, so a walk starts at the first second it exists.
        /// </summary>
        public void StartBlocked(CityState city, GameContent content, long time)
        {
            IReadOnlyList<Trip> trips = city.Trips;
            int index = 0;
            while (index < trips.Count)
            {
                Trip trip = trips[index];
                int source = city.IndexOfBuilding(trip.SourceId);
                int target = city.IndexOfBuilding(trip.TargetId);
                if (trip.Leg == TripLeg.BlockedAtTarget && _walks.TrySeconds(target, source, out long back))
                {
                    trip.ResumeToSource(time, checked(time + back));
                    Due(trip.ArrivesAt);
                }
                else if (trip.Leg == TripLeg.BlockedAtSource)
                {
                    if (!FetchNext(city, content, trip, source, target, time))
                    {
                        city.RemoveTripAt(index);
                        continue;
                    }

                    if (trip.Leg == TripLeg.ToTarget)
                    {
                        Due(trip.ArrivesAt);
                    }
                }

                index++;
            }
        }

        /// <summary>Moves the next end of a leg forward to <paramref name="arrivesAt"/> when it comes earlier.</summary>
        private void Due(long arrivesAt)
        {
            if (!_nextDueAt.HasValue || arrivesAt < _nextDueAt.Value)
            {
                _nextDueAt = arrivesAt;
            }
        }

        /// <summary>The earliest end of a leg of the city's trips, or null when it has none.</summary>
        private static long? EarliestArrival(CityState city)
        {
            long? earliest = null;
            IReadOnlyList<Trip> trips = city.Trips;
            for (int index = 0; index < trips.Count; index++)
            {
                // A waiting or blocked trip walks nowhere: it puts its goods back, or walks again, in the start pass.
                if ((trips[index].Leg == TripLeg.ToTarget || trips[index].Leg == TripLeg.ToSource) && (!earliest.HasValue || trips[index].ArrivesAt < earliest.Value))
                {
                    earliest = trips[index].ArrivesAt;
                }
            }

            return earliest;
        }
    }
}
