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
    /// second, after the trips, in ascending (station, slot): every arrival first spends the walk's stamina from the absence start on
    /// (<c>04</c> section 2); at a stop a courier does the stop's actions in order, then walks to the next stop (after the last, the first), or
    /// home when its stamina reached 0; at home it stands at its station, or rests without stamina. A courier without a route runs its delivery
    /// lines instead (<c>04</c> sections 18 and 19, CR-0008 D16): stop 1 is the first line's source, stop 2 its target, and after the last line
    /// it walks home. At a paused station (CR-0008 D24) a courier finishes the stop it walked to, gives its load back and walks home. Their
    /// departures are the last part of step 5 (<see cref="StartWalks"/>): a courier at its station with a route or a line and stamina, whose
    /// station is neither constructing, upgrading nor paused, leaves for the first stop. The arrivals are an indexed heap keyed by station and slot (the station's building
    /// index times the content's most courier slots, plus the slot), which stays valid while auto-staffing adds couriers during the advance
    /// and orders arrivals at one second by (station, slot); a courier stopped inside the advance (its station's upgrade started) leaves a
    /// stale entry, which is dropped when it comes up.
    /// </summary>
    internal sealed class CourierArrivalSource : IEventSource
    {
        /// <summary>The couriers' arrivals, by <see cref="Key"/>.</summary>
        private readonly TimerHeap _arrivals;

        /// <summary>The most slots a courier station level of the content has (at least 1): the stride of <see cref="Key"/>.</summary>
        private readonly int _slots;

        /// <summary>The advance's building timers, which a put that starts a project updates.</summary>
        private readonly BuildingTimerQueue _timers;

        /// <summary>The walk times between buildings, searched once per pair.</summary>
        private readonly WalkTimes _walks;

        /// <summary>The city-clock second the player is absent from, or null when present for the whole advance (<c>04</c> section 2).</summary>
        private readonly long? _absentFrom;

        /// <summary>The stamina each courier spent so far in the advance, by <see cref="Key"/>, for the report's most (<c>04</c> section 11).</summary>
        private readonly long[] _spent;

        /// <summary>The load parts a put took off a courier, reused from put to put.</summary>
        private readonly List<CourierLoad> _putParts = new List<CourierLoad>();

        /// <summary>Creates the source for one advance of the city.</summary>
        public CourierArrivalSource(CityState city, GameContent content, BuildingTimerQueue timers, WalkTimes walks, long? absentFrom)
        {
            _timers = timers;
            _walks = walks;
            _absentFrom = absentFrom;
            _slots = MostSlots(content);
            IReadOnlyList<Courier> couriers = city.Couriers;
            _arrivals = new TimerHeap(checked(city.Buildings.Count * _slots));
            _spent = new long[checked(city.Buildings.Count * _slots)];
            for (int index = 0; index < couriers.Count; index++)
            {
                long? arrivesAt = couriers[index].ArrivesAt;
                if (arrivesAt.HasValue)
                {
                    _arrivals.Add(Key(city, couriers[index]), arrivesAt.Value);
                }
            }
        }

        /// <inheritdoc/>
        public long? NextDueAt(CityState city)
        {
            // Drop the entries of couriers that stopped walking since they were set (their station's upgrade started).
            while (_arrivals.Count > 0 && CourierAt(city, _arrivals.PeekIndex())?.ArrivesAt != _arrivals.PeekEndsAt())
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
                int key = _arrivals.Pop();
                // NextDueAt dropped every stale entry in front, so the courier of this key exists and arrives now.
                Arrive(city, content, CourierAt(city, key)!, time, report);
            }
        }

        /// <summary>
        /// The departures of step 5 at <paramref name="time"/> (<c>04</c> section 19), in ascending (station, slot): every courier at its station
        /// with stamina, whose station is neither constructing, upgrading nor paused, walks to its route's first stop, or to its first line's
        /// source.
        /// </summary>
        public void StartWalks(CityState city, long time)
        {
            IReadOnlyList<Courier> couriers = city.Couriers;
            for (int index = 0; index < couriers.Count; index++)
            {
                Courier courier = couriers[index];
                if (courier.State != CourierState.AtStation || courier.Stamina < 1)
                {
                    continue;
                }

                bool hasRoute = city.TryGetRoute(courier.StationId, courier.Slot, out Route? route);
                DeliveryLine? line = null;
                if (!hasRoute && !city.TryGetFirstLine(courier.StationId, courier.Slot, out line))
                {
                    continue;
                }

                int station = city.IndexOfBuilding(courier.StationId);
                BuildingPhase phase = city.Buildings[station].Phase;
                if (phase == BuildingPhase.Constructing || phase == BuildingPhase.Upgrading || city.Buildings[station].Paused)
                {
                    continue;
                }

                if (hasRoute)
                {
                    WalkOn(city, courier, station, route!, 1, time);
                }
                else
                {
                    WalkTo(city, courier, station, line!.SourceId, 1, time);
                }
            }
        }

        /// <summary>
        /// What a courier does when it arrives at <paramref name="time"/>: it spends the walk's stamina, then at home stands or rests; at a stop it
        /// acts and walks on, or home when it is tired or its station is paused (then without its load, D24).
        /// </summary>
        private void Arrive(CityState city, GameContent content, Courier courier, long time, SettlementReportBuilder report)
        {
            SpendWalk(city, courier, time, report);
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

            if (!city.TryGetRoute(courier.StationId, courier.Slot, out Route? route))
            {
                if (city.TryGetFirstLine(courier.StationId, courier.Slot, out DeliveryLine? line))
                {
                    ArriveOnLine(city, content, courier, line, time, report);
                }
                else
                {
                    CourierRules.StopWorking(city, courier);
                }

                return;
            }

            RouteStop stop = route.Stops[courier.ToStop - 1];
            int building = city.IndexOfBuilding(stop.BuildingId);
            if (!RunActions(city, content, courier, stop, building, time, report))
            {
                return;
            }

            if (IsStationPaused(city, courier))
            {
                // Paused: it did this stop and goes home empty (04 section 19, D24).
                CourierRules.GiveLoadBack(city, courier);
                WalkHome(city, courier, building, time);
                return;
            }

            if (courier.Stamina < 1)
            {
                // Tired: it did this stop's actions and walks home with what it carries (04 section 19).
                int station = city.IndexOfBuilding(courier.StationId);
                long home = checked(time + _walks.Seconds(building, station));
                courier.ReturnHome(time, home);
                _arrivals.Set(Key(city, courier), home);
                return;
            }

            WalkOn(city, courier, building, route, (courier.ToStop % route.Stops.Count) + 1, time);
        }

        /// <summary>
        /// Spends the stamina of the walk that ends now (<c>04</c> section 2): the part of it from the absence start on, <c>max(0, a − max(d,
        /// absentFrom))</c>, at most what is left; nothing while the player is present. Notes the courier's total for the report and, when the
        /// last of its stamina goes, that it is tired at this second.
        /// </summary>
        private void SpendWalk(CityState city, Courier courier, long time, SettlementReportBuilder report)
        {
            if (!_absentFrom.HasValue || courier.Stamina < 1)
            {
                return;
            }

            // A courier that arrives walked since its DepartedAt; the walk is set exactly while it walks.
            long from = Math.Max(courier.DepartedAt!.Value, _absentFrom.Value);
            long spent = courier.Spend(time - from);
            if (spent < 1)
            {
                return;
            }

            int key = Key(city, courier);
            _spent[key] = checked(_spent[key] + spent);
            report.RecordCourierSeconds(_spent[key]);
            if (courier.Stamina < 1)
            {
                report.AddTiredCourier(courier.StationId, courier.Slot, time);
            }
        }

        /// <summary>
        /// A courier on its first delivery line arrives (<c>04</c> section 19, CR-0008 D16). At the source (stop 1) it takes the line's quantity
        /// less what it already carries of the good, at most the stock and its free carry room, and walks to the target; carrying none, the line
        /// ends. At the target (stop 2) it puts all it carries of the good by the delivery rule, gives the rest of its load back to where it came
        /// from (a returned load), and the line ends. After a line ends it walks to its next line's source, or home. A tired courier walks home
        /// instead, its lines waiting. At a paused station (D24) it takes nothing at a source and walks home with its load given back, and after
        /// a put it walks home, its other lines waiting.
        /// </summary>
        private void ArriveOnLine(CityState city, GameContent content, Courier courier, DeliveryLine line, long time, SettlementReportBuilder report)
        {
            int here = city.IndexOfBuilding(courier.ToStop == 1 ? line.SourceId : line.TargetId);
            Building building = city.Buildings[here];
            if (courier.ToStop == 1)
            {
                if (IsStationPaused(city, courier))
                {
                    CourierRules.GiveLoadBack(city, courier);
                    WalkHome(city, courier, here, time);
                    return;
                }

                Building station = city.Buildings[city.IndexOfBuilding(courier.StationId)];
                long carryLimit = CityQueries.DefinitionOf(content, station.TypeId).GetLevel(station.Level).CarryLimit;
                long room = Math.Max(0, checked(carryLimit - courier.Carried));
                long wanted = Math.Max(0, checked(line.Quantity - courier.CarriedOf(line.Item)));
                long quantity = Math.Min(Math.Min(wanted, building.GetStock(line.Item)), room);
                if (quantity > 0)
                {
                    building.TakeFromStore(line.Item, quantity);
                    courier.Load(line.Item, building.Id, quantity);
                }

                if (courier.CarriedOf(line.Item) > 0)
                {
                    if (courier.Stamina < 1)
                    {
                        WalkHome(city, courier, here, time);
                    }
                    else
                    {
                        WalkTo(city, courier, here, line.TargetId, 2, time);
                    }

                    return;
                }
            }
            else
            {
                long carried = courier.CarriedOf(line.Item);
                if (carried > 0 && !Put(city, content, courier, line.Item, carried, here, time, report))
                {
                    return;
                }

                CourierRules.GiveLoadBack(city, courier);
            }

            // The line ended: the next one, or home.
            city.RemoveLineAt(city.FirstLineIndex(courier.StationId, courier.Slot));
            if (courier.Stamina >= 1 && !IsStationPaused(city, courier) && city.TryGetFirstLine(courier.StationId, courier.Slot, out DeliveryLine? next))
            {
                WalkTo(city, courier, here, next.SourceId, 1, time);
            }
            else
            {
                WalkHome(city, courier, here, time);
            }
        }

        /// <summary>Whether a courier's station is paused (CR-0008 D24).</summary>
        private static bool IsStationPaused(CityState city, Courier courier) => city.Buildings[city.IndexOfBuilding(courier.StationId)].Paused;

        /// <summary>Sends a courier from the building at <paramref name="fromIndex"/> home to its station, from now.</summary>
        private void WalkHome(CityState city, Courier courier, int fromIndex, long time)
        {
            long home = checked(time + _walks.Seconds(fromIndex, city.IndexOfBuilding(courier.StationId)));
            courier.ReturnHome(time, home);
            _arrivals.Set(Key(city, courier), home);
        }

        /// <summary>Sends a courier from the building at <paramref name="fromIndex"/> to building <paramref name="toId"/> as stop <paramref name="toStop"/> of its line, from now.</summary>
        private void WalkTo(CityState city, Courier courier, int fromIndex, int toId, int toStop, long time)
        {
            long arrivesAt = checked(time + _walks.Seconds(fromIndex, city.IndexOfBuilding(toId)));
            courier.WalkTo(toStop, time, arrivesAt);
            _arrivals.Set(Key(city, courier), arrivesAt);
        }

        /// <summary>Sends a courier from the building at <paramref name="fromIndex"/> to stop <paramref name="toStop"/> of its route, from now.</summary>
        private void WalkOn(CityState city, Courier courier, int fromIndex, Route route, int toStop, long time)
        {
            int target = city.IndexOfBuilding(route.Stops[toStop - 1].BuildingId);
            long arrivesAt = checked(time + _walks.Seconds(fromIndex, target));
            courier.WalkTo(toStop, time, arrivesAt);
            _arrivals.Set(Key(city, courier), arrivesAt);
        }

        /// <summary>A courier's key in the heap: its station's building index times <see cref="_slots"/>, plus its slot − 1.</summary>
        private int Key(CityState city, Courier courier) => (city.IndexOfBuilding(courier.StationId) * _slots) + courier.Slot - 1;

        /// <summary>The courier of a key, or null when its station's slot has none (any more).</summary>
        private Courier? CourierAt(CityState city, int key)
        {
            int index = city.FindCourier(city.Buildings[key / _slots].Id, (key % _slots) + 1);
            return index >= 0 ? city.Couriers[index] : null;
        }

        /// <summary>The most workers any courier station level of the content takes, at least 1 (content rule 3 gives every such level 1 or more).</summary>
        private static int MostSlots(GameContent content)
        {
            int most = 1;
            IReadOnlyList<BuildingDefinition> buildings = content.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                if (buildings[index].Kind != BuildingKind.CourierStation)
                {
                    continue;
                }

                IReadOnlyList<BuildingLevelDefinition> levels = buildings[index].Levels;
                for (int level = 0; level < levels.Count; level++)
                {
                    most = Math.Max(most, levels[level].MaxWorkers);
                }
            }

            return most;
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
