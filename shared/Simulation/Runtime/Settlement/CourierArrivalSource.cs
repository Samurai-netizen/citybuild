#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// Couriers in an advance (<c>core-instructions/04-simulation-model.md</c>, sections 6 and 19). Their arrivals are step 4 of the order at one
    /// second, after the trips, in ascending (station, slot): at a stop a courier does the stop's actions in order, then walks to the next stop
    /// (after the last, the first); at home it stands at its station, or rests without stamina. Their departures are the last part of step 5
    /// (<see cref="StartWalks"/>): a courier at its station with a route and stamina, whose station is neither constructing nor upgrading,
    /// leaves for the first stop. Until CR-0008-T1 part H a courier that arrives after the production cap stops working instead (decision 45),
    /// and none leaves then (the start pass doesn't run). The arrivals are an indexed heap by courier index; a courier stopped inside the
    /// advance (its station's upgrade started) leaves a stale entry, which is dropped when it comes up.
    /// </summary>
    internal sealed class CourierArrivalSource : IEventSource
    {
        /// <summary>The couriers' arrivals, by their index in <see cref="CityState.Couriers"/>, which doesn't change during an advance.</summary>
        private readonly TimerHeap _arrivals;

        /// <summary>The advance's building timers, which a put that starts a project updates.</summary>
        private readonly BuildingTimerQueue _timers;

        /// <summary>The walk times between buildings, searched once per pair.</summary>
        private readonly WalkTimes _walks;

        /// <summary>The last second of the production window; a courier arriving later stops working (decision 45).</summary>
        private readonly long _productionUntil;

        /// <summary>The load parts a put took off a courier, reused from put to put.</summary>
        private readonly List<CourierLoad> _putParts = new List<CourierLoad>();

        /// <summary>Creates the source for one advance of the city.</summary>
        public CourierArrivalSource(CityState city, BuildingTimerQueue timers, WalkTimes walks, long productionUntil)
        {
            _timers = timers;
            _walks = walks;
            _productionUntil = productionUntil;
            IReadOnlyList<Courier> couriers = city.Couriers;
            _arrivals = new TimerHeap(couriers.Count);
            for (int index = 0; index < couriers.Count; index++)
            {
                long? arrivesAt = couriers[index].ArrivesAt;
                if (arrivesAt.HasValue)
                {
                    _arrivals.Add(index, arrivesAt.Value);
                }
            }
        }

        /// <inheritdoc/>
        public long? NextDueAt(CityState city)
        {
            // Drop the entries of couriers that stopped walking since they were set (their station's upgrade started).
            while (_arrivals.Count > 0 && city.Couriers[_arrivals.PeekIndex()].ArrivesAt != _arrivals.PeekEndsAt())
            {
                _arrivals.Pop();
            }

            return _arrivals.Count > 0 ? _arrivals.PeekEndsAt() : (long?)null;
        }

        /// <inheritdoc/>
        public void CompleteDue(CityState city, GameContent content, long time, SettlementReportBuilder report)
        {
            // A courier's next walk ends at least 1 s later, so this loop ends; arrivals at one second come in ascending courier index.
            while (NextDueAt(city) == time)
            {
                int index = _arrivals.Pop();
                Arrive(city, content, index, time, report);
            }
        }

        /// <summary>
        /// The departures of step 5 at <paramref name="time"/> (<c>04</c> section 19), in ascending (station, slot): every courier at its station
        /// with a route and stamina, whose station is neither constructing nor upgrading, walks to its route's first stop.
        /// </summary>
        public void StartWalks(CityState city, long time)
        {
            IReadOnlyList<Courier> couriers = city.Couriers;
            for (int index = 0; index < couriers.Count; index++)
            {
                Courier courier = couriers[index];
                if (courier.State != CourierState.AtStation || courier.Stamina < 1
                    || !city.TryGetRoute(courier.StationId, courier.Slot, out Route? route))
                {
                    continue;
                }

                int station = city.IndexOfBuilding(courier.StationId);
                BuildingPhase phase = city.Buildings[station].Phase;
                if (phase == BuildingPhase.Constructing || phase == BuildingPhase.Upgrading)
                {
                    continue;
                }

                WalkOn(city, index, courier, station, route, 1, time);
            }
        }

        /// <summary>What a courier does when it arrives at <paramref name="time"/>: at home it stands or rests; at a stop it acts and walks on.</summary>
        private void Arrive(CityState city, GameContent content, int index, long time, SettlementReportBuilder report)
        {
            Courier courier = city.Couriers[index];
            if (courier.State == CourierState.Returning)
            {
                if (courier.Stamina < 1)
                {
                    courier.Rest();
                }
                else
                {
                    courier.StandAtStation();
                }

                return;
            }

            if (time > _productionUntil || !city.TryGetRoute(courier.StationId, courier.Slot, out Route? route))
            {
                CourierRules.StopWorking(city, courier);
                return;
            }

            RouteStop stop = route.Stops[courier.ToStop - 1];
            int building = city.IndexOfBuilding(stop.BuildingId);
            if (RunActions(city, content, courier, stop, building, time, report))
            {
                WalkOn(city, index, courier, building, route, (courier.ToStop % route.Stops.Count) + 1, time);
            }
        }

        /// <summary>Sends a courier from the building at <paramref name="fromIndex"/> to stop <paramref name="toStop"/> of its route, from now.</summary>
        private void WalkOn(CityState city, int index, Courier courier, int fromIndex, Route route, int toStop, long time)
        {
            int target = city.IndexOfBuilding(route.Stops[toStop - 1].BuildingId);
            long arrivesAt = checked(time + _walks.Seconds(fromIndex, target));
            courier.WalkTo(toStop, time, arrivesAt);
            _arrivals.Set(index, arrivesAt);
        }

        /// <summary>
        /// A stop's actions in order (<c>04</c> section 19): a take moves the amount of the store's stock, at most the stock and the courier's
        /// free carry room, onto the courier; a put hands the amount of what it carries of the good to the building by the delivery rule (a
        /// waiting project first, then the research there, then the store, <c>04</c> section 16), and what isn't taken stays on the courier.
        /// A courier never waits: what it can't do it skips. A put that starts its own station's upgrade stops the courier: what it didn't
        /// deliver goes back to where it came from, the rest of the stop is skipped, and the result is false (it doesn't walk on).
        /// </summary>
        private bool RunActions(CityState city, GameContent content, Courier courier, RouteStop stop, int buildingIndex, long time, SettlementReportBuilder report)
        {
            Building building = city.Buildings[buildingIndex];
            Building station = city.Buildings[city.IndexOfBuilding(courier.StationId)];
            long carryLimit = CityQueries.DefinitionOf(content, station.TypeId).GetLevel(station.Level).CarryLimit;
            IReadOnlyList<RouteAction> actions = stop.Actions;
            for (int index = 0; index < actions.Count; index++)
            {
                RouteAction action = actions[index];
                if (action.Kind == RouteActionKind.Take)
                {
                    long stock = building.GetStock(action.Item);
                    long room = Math.Max(0, checked(carryLimit - courier.Carried));
                    long quantity = Math.Min(Math.Min(AmountRules.Of(content, action.Amount, stock), stock), room);
                    if (quantity > 0)
                    {
                        building.TakeFromStore(action.Item, quantity);
                        courier.Load(action.Item, building.Id, quantity);
                    }
                }
                else
                {
                    long carried = courier.CarriedOf(action.Item);
                    long quantity = Math.Min(AmountRules.Of(content, action.Amount, carried), carried);
                    if (quantity > 0 && !Put(city, content, courier, action.Item, quantity, buildingIndex, time, report))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Puts <paramref name="quantity"/> of a good from the courier into the building: it comes off the load first (lowest origin first), the
        /// building takes what it can, and what it leaves goes back onto the courier with its origin (the highest origins first, so the parts
        /// delivered are the lowest; decision 47), or, when the delivery stopped the courier, back to the stores it came from. Returns whether
        /// the courier still works.
        /// </summary>
        private bool Put(CityState city, GameContent content, Courier courier, ItemId item, long quantity, int buildingIndex, long time, SettlementReportBuilder report)
        {
            _putParts.Clear();
            courier.Unload(item, quantity, _putParts);
            long left = Deliveries.Deliver(city, content, buildingIndex, item, quantity, time, report, _timers);
            bool working = courier.State == CourierState.Walking;
            for (int index = _putParts.Count - 1; index >= 0 && left > 0; index--)
            {
                long back = Math.Min(left, _putParts[index].Quantity);
                left -= back;
                if (working)
                {
                    courier.Load(item, _putParts[index].FromId, back);
                }
                else
                {
                    city.Buildings[city.IndexOfBuilding(_putParts[index].FromId)].AddToStore(item, back);
                }
            }

            return working;
        }
    }
}
