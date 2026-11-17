#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// Goods that go back to the store they came from, never above its limit (<c>04</c> section 10 invariant 6, sections 18 and 19; M0-C-3):
    /// a returned load goes back as far as the store has room, and what doesn't fit stays with its walker. A courier without work at its
    /// station and a trip worker waiting at its source put the rest back as room appears: <see cref="PutBackWhereRoom"/> runs first in every
    /// start pass of the advance, so also after every mutation (its zero-length advance).
    /// </summary>
    internal static class HeldLoads
    {
        /// <summary>
        /// Puts each part of the courier's load back into the store it came from, at most that store's room; what doesn't fit stays on the
        /// courier with its origin. Returns whether the courier carries nothing any more.
        /// </summary>
        internal static bool PutBack(CityState city, GameContent content, Courier courier)
        {
            if (courier.Loads.Count == 0)
            {
                return true;
            }

            foreach (CourierLoad part in courier.TakeAllLoads())
            {
                Building origin = city.Buildings[city.IndexOfBuilding(part.FromId)];
                long back = Math.Min(part.Quantity, CityQueries.Room(city, content, origin, part.Item));
                if (back > 0)
                {
                    origin.AddToStore(part.Item, back);
                }

                if (back < part.Quantity)
                {
                    courier.Load(part.Item, part.FromId, part.Quantity - back);
                }
            }

            return courier.Loads.Count == 0;
        }

        /// <summary>Whether every part of the courier's load would fit back into its store now (nothing changes).</summary>
        internal static bool FitsBack(CityState city, GameContent content, Courier courier)
        {
            IReadOnlyList<CourierLoad> loads = courier.Loads;
            for (int index = 0; index < loads.Count; index++)
            {
                Building origin = city.Buildings[city.IndexOfBuilding(loads[index].FromId)];
                if (loads[index].Quantity > CityQueries.Room(city, content, origin, loads[index].Item))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Puts held goods back where room has appeared, in a fixed order: couriers by station and slot (those at their station, at it or
        /// resting, without a route and without delivery lines; a courier with work keeps its load for it), then waiting trips by id (a trip
        /// whose load is all back ends and frees its worker).
        /// </summary>
        internal static void PutBackWhereRoom(CityState city, GameContent content)
        {
            IReadOnlyList<Courier> couriers = city.Couriers;
            for (int index = 0; index < couriers.Count; index++)
            {
                Courier courier = couriers[index];
                if (courier.Loads.Count > 0
                    && (courier.State == CourierState.AtStation || courier.State == CourierState.Resting)
                    && !city.TryGetRoute(courier.StationId, courier.Slot, out _)
                    && !city.TryGetFirstLine(courier.StationId, courier.Slot, out _))
                {
                    PutBack(city, content, courier);
                }
            }

            IReadOnlyList<Trip> trips = city.Trips;
            int tripIndex = 0;
            while (tripIndex < trips.Count)
            {
                Trip trip = trips[tripIndex];
                if (trip.Leg == TripLeg.Waiting && PutBack(city, content, trip))
                {
                    city.RemoveTripAt(tripIndex);
                    continue;
                }

                tripIndex++;
            }
        }

        /// <summary>
        /// Puts a trip's load back into its source store, at most its room; returns whether the worker carries nothing any more. A trip that
        /// carries the rest back waits at the source until it is all back.
        /// </summary>
        internal static bool PutBack(CityState city, GameContent content, Trip trip)
        {
            Building source = city.Buildings[city.IndexOfBuilding(trip.SourceId)];
            long back = Math.Min(trip.Quantity, CityQueries.Room(city, content, source, trip.Item));
            if (back > 0)
            {
                source.AddToStore(trip.Item, back);
                trip.Unload(back);
            }

            return trip.Quantity == 0;
        }
    }
}
