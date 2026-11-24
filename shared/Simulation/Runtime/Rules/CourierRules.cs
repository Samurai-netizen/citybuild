#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// Couriers, routes and delivery lines (<c>core-instructions/04-simulation-model.md</c>, sections 8, 18 and 19): <see cref="SetRoute"/>, the
    /// mutation that gives a station's slot its route, <see cref="CancelDelivery"/>, which cancels a courier's delivery lines,
    /// <see cref="ReenergizeCouriers"/>, which fills every courier's stamina again, <see cref="PickFreeCourier"/>, which chooses the courier of a
    /// new delivery, and the courier side of staffing a station and of starting its upgrade, which <see cref="CityRules"/> and the project rules
    /// call. Couriers walk their routes and lines in <see cref="Settlement.Simulator"/>.
    /// </summary>
    public static class CourierRules
    {
        /// <summary>
        /// Sets the route of a station's slot (0 stops clears it). The slot's courier, if the slot has one, gives its load back to where each
        /// part came from (a returned load, at most the room; the rest stays on the courier, M0-C-3) and stands at its station; a resting one keeps resting (<c>04</c> section 19).
        /// It leaves for the first stop in the request's zero-length advance. Goods only move, so the result spends and refunds nothing.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="stationId">The courier station's building id.</param>
        /// <param name="slot">The slot, 1 to the station level's <see cref="BuildingLevelDefinition.MaxWorkers"/>.</param>
        /// <param name="stops">The stops in order: none, or 2 to <see cref="GameRules.MaxRouteStops"/>.</param>
        /// <returns>
        /// Ok, or the first failed check in the order of <c>04</c> section 8: <see cref="MutationErrorCode.BuildingNotFound"/> (the station),
        /// <see cref="MutationErrorCode.InvalidArgument"/> (not a courier station), <see cref="MutationErrorCode.InvalidRoute"/> (the slot, the
        /// number of stops or of actions, two consecutive stops at one building), <see cref="MutationErrorCode.BuildingNotFound"/> (a stop),
        /// then for every action in order <see cref="MutationErrorCode.UnknownItem"/> or <see cref="MutationErrorCode.InvalidArgument"/> (the
        /// currency), then for every amount in order <see cref="MutationErrorCode.InvalidArgument"/> (an unknown preset) or
        /// <see cref="MutationErrorCode.AmountModeLocked"/>; with 1 or more stops then <see cref="MutationErrorCode.RoutesLocked"/> (the route
        /// building isn't built, D15) and <see cref="MutationErrorCode.CourierBusy"/> (the slot's courier has delivery lines, D19).
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/>, <paramref name="content"/> or <paramref name="stops"/> is null.</exception>
        public static MutationResult SetRoute(CityState city, GameContent content, int stationId, int slot, IReadOnlyList<RouteStop> stops)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (stops is null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            if (!city.TryGetBuilding(stationId, out Building? station))
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingNotFound);
            }

            BuildingDefinition definition = CityQueries.DefinitionOf(content, station.TypeId);
            if (definition.Kind != BuildingKind.CourierStation)
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            MutationErrorCode? shape = RouteShapeError(content, definition.GetLevel(station.Level), slot, stops);
            if (shape.HasValue)
            {
                return MutationResult.Rejected(shape.Value);
            }

            MutationErrorCode? actions = ActionError(city, content, stops);
            if (actions.HasValue)
            {
                return MutationResult.Rejected(actions.Value);
            }

            if (stops.Count > 0 && !CityQueries.AreRoutesUnlocked(city, content))
            {
                return MutationResult.Rejected(MutationErrorCode.RoutesLocked);
            }

            if (stops.Count > 0 && city.CountLines(stationId, slot) > 0)
            {
                return MutationResult.Rejected(MutationErrorCode.CourierBusy);
            }

            if (stops.Count > 0 && !IsRouteWalkable(city, content, station, stops))
            {
                return MutationResult.Rejected(MutationErrorCode.NoPath);
            }

            if (stops.Count == 0)
            {
                city.ClearRoute(stationId, slot);
            }
            else
            {
                city.SetRoute(new Route(stationId, slot, stops));
            }

            int courier = city.FindCourier(stationId, slot);
            if (courier >= 0)
            {
                StopWorking(city, content, city.Couriers[courier]);
            }

            return MutationResult.Ok;
        }

        /// <summary>
        /// Whether the loads of the couriers that staffing <paramref name="station"/> with <paramref name="workers"/> removes (the slots above
        /// it) all fit back into the stores they came from (<c>04</c> section 8, AssignWorkers; M0-C-3). Changes nothing.
        /// </summary>
        internal static bool RemovedLoadsFitBack(CityState city, GameContent content, Building station, int workers)
        {
            for (int slot = station.AssignedWorkers; slot > workers; slot--)
            {
                int index = city.FindCourier(station.Id, slot);
                if (index >= 0 && !HeldLoads.FitsBack(city, content, city.Couriers[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The courier side of <see cref="CityRules.AssignWorkers"/> on a courier station, after its count changed from
        /// <paramref name="before"/> to its <see cref="Building.AssignedWorkers"/> (<c>04</c> section 19): each new worker is a courier at the
        /// station with its level's full stamina, in the next slot; each removed one is the highest slot's courier, which gives its load back
        /// and leaves. Routes stay with their slots (CR-0008-T1 decision 48).
        /// </summary>
        internal static void StaffStation(CityState city, GameContent content, BuildingLevelDefinition level, Building station, int before)
        {
            int after = station.AssignedWorkers;
            for (int slot = before + 1; slot <= after; slot++)
            {
                if (city.FindCourier(station.Id, slot) < 0)
                {
                    city.AddCourier(Courier.AtStationWith(station.Id, slot, level.StaminaSeconds));
                }
            }

            for (int slot = before; slot > after; slot--)
            {
                int index = city.FindCourier(station.Id, slot);
                if (index >= 0)
                {
                    GiveLoadBack(city, content, city.Couriers[index]);
                    city.RemoveLinesOf(station.Id, slot);
                    city.RemoveCourierAt(index);
                }
            }
        }

        /// <summary>
        /// Stops every courier of a station whose upgrade starts (<c>04</c> section 8): each gives its load back and stands at the station (a
        /// resting one keeps resting). Inside an advance a stopped courier's arrival stays in the advance's queue as a stale entry, which the
        /// arrival source skips.
        /// </summary>
        internal static void StopCouriersOf(CityState city, GameContent content, int stationId)
        {
            IReadOnlyList<Courier> couriers = city.Couriers;
            for (int index = 0; index < couriers.Count; index++)
            {
                if (couriers[index].StationId == stationId)
                {
                    StopWorking(city, content, couriers[index]);
                }
            }
        }

        /// <summary>
        /// Cancels one delivery line of a station's courier, or with <paramref name="lineId"/> null all of its lines and its route (<c>04</c>
        /// section 8, CR-0008 D18). When what it was doing goes (the first line while it walks, or anything with "all"), it gives its load back
        /// to where each part came from and stands at its station (a tired one rests), ready to leave for its next line in the request's
        /// zero-length advance; cancelling its first line while it is not walking still gives back what that line loaded. Goods only move.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="stationId">The courier station's building id.</param>
        /// <param name="slot">The courier's slot.</param>
        /// <param name="lineId">The line to cancel, or null for all of the courier's lines and its route.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.BuildingNotFound"/> (the station), <see cref="MutationErrorCode.InvalidArgument"/>
        /// (not a courier station, or no courier in the slot), <see cref="MutationErrorCode.DeliveryNotFound"/> (the line is not that courier's).
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static MutationResult CancelDelivery(CityState city, GameContent content, int stationId, int slot, int? lineId)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!city.TryGetBuilding(stationId, out Building? station))
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingNotFound);
            }

            int courierIndex = city.FindCourier(stationId, slot);
            if (CityQueries.DefinitionOf(content, station.TypeId).Kind != BuildingKind.CourierStation || courierIndex < 0)
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            Courier courier = city.Couriers[courierIndex];
            if (!lineId.HasValue)
            {
                city.RemoveLinesOf(stationId, slot);
                city.ClearRoute(stationId, slot);
                StopWorking(city, content, courier);
                return MutationResult.Ok;
            }

            int index = city.IndexOfLine(lineId.Value);
            if (index < 0 || city.DeliveryLines[index].StationId != stationId || city.DeliveryLines[index].Slot != slot)
            {
                return MutationResult.Rejected(MutationErrorCode.DeliveryNotFound);
            }

            bool first = index == city.FirstLineIndex(stationId, slot);
            city.RemoveLineAt(index);
            // A courier waiting for a path to that line's stop (M1-03) stops like one walking there; one waiting to walk home keeps waiting.
            if (first && (courier.State == CourierState.Walking || (courier.State == CourierState.Blocked && courier.ToStop >= 1)))
            {
                StopWorking(city, content, courier);
            }
            else if (first)
            {
                GiveLoadBack(city, content, courier);
            }

            return MutationResult.Ok;
        }

        /// <summary>Whether the city has a courier without a route, which may carry delivery lines (whether a walk reaches it or not).</summary>
        internal static bool HasFreeCourier(CityState city)
        {
            IReadOnlyList<Courier> couriers = city.Couriers;
            for (int index = 0; index < couriers.Count; index++)
            {
                if (!city.TryGetRoute(couriers[index].StationId, couriers[index].Slot, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a walk joins every leg of a route (M1-03): the station to the first stop, each stop to the next, the last stop back to the
        /// station. The stops' buildings exist (checked before).
        /// </summary>
        private static bool IsRouteWalkable(CityState city, GameContent content, Building station, IReadOnlyList<RouteStop> stops)
        {
            var paths = new PathFinder(content);
            paths.Load(city);
            Building previous = station;
            for (int index = 0; index <= stops.Count; index++)
            {
                Building next = index < stops.Count ? city.Buildings[city.IndexOfBuilding(stops[index].BuildingId)] : station;
                if (!paths.TryMeasure(previous.X, previous.Y, next.X, next.Y, out _))
                {
                    return false;
                }

                previous = next;
            }

            return true;
        }

        /// <summary>
        /// The courier a new delivery from <paramref name="source"/> goes to (<c>04</c> section 18, CR-0008 D17): among the couriers without a
        /// route, the one with the fewest delivery lines, then the shortest walk from its station to the source, then the lowest station id and
        /// slot; null when every courier has a route (or the city has none).
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="paths">A path finder loaded with the city's map.</param>
        /// <param name="source">The building the delivery's goods come from.</param>
        /// <returns>The courier, or null.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static Courier? PickFreeCourier(CityState city, GameContent content, PathFinder paths, Building source)
        {
            if (city is null || content is null || paths is null || source is null)
            {
                throw new ArgumentNullException(city is null ? nameof(city) : content is null ? nameof(content) : paths is null ? nameof(paths) : nameof(source));
            }

            int bonus = CityQueries.WalkBonusPermille(city, content);
            Courier? best = null;
            int bestLines = 0;
            long bestSeconds = 0;
            IReadOnlyList<Courier> couriers = city.Couriers;
            // The couriers come by (station, slot), so keeping only a strictly better one leaves the lowest ids on a tie.
            for (int index = 0; index < couriers.Count; index++)
            {
                Courier courier = couriers[index];
                if (city.TryGetRoute(courier.StationId, courier.Slot, out _))
                {
                    continue;
                }

                Building station = city.Buildings[city.IndexOfBuilding(courier.StationId)];
                int lines = city.CountLines(courier.StationId, courier.Slot);
                if (!paths.TryMeasure(station.X, station.Y, source.X, source.Y, out WalkCost walk))
                {
                    continue;
                }

                long seconds = walk.Seconds(bonus);
                if (best is null || lines < bestLines || (lines == bestLines && seconds < bestSeconds))
                {
                    best = courier;
                    bestLines = lines;
                    bestSeconds = seconds;
                }
            }

            return best;
        }

        /// <summary>
        /// Re-energizes every courier of the city (<c>04</c> section 8): its stamina is its station level's <see cref="BuildingLevelDefinition.StaminaSeconds"/>
        /// again; a resting courier stands at its station with its load, ready to leave in the request's zero-length advance, and a returning
        /// one stands there when it arrives (section 19). It always succeeds and moves no goods, so the result spends and refunds nothing.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <returns>Ok.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static MutationResult ReenergizeCouriers(CityState city, GameContent content)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            IReadOnlyList<Courier> couriers = city.Couriers;
            for (int index = 0; index < couriers.Count; index++)
            {
                Courier courier = couriers[index];
                Building station = city.Buildings[city.IndexOfBuilding(courier.StationId)];
                courier.Reenergize(CityQueries.DefinitionOf(content, station.TypeId).GetLevel(station.Level).StaminaSeconds);
                if (courier.State == CourierState.Resting)
                {
                    courier.StandAtStation();
                }
            }

            return MutationResult.Ok;
        }

        /// <summary>
        /// A courier stops working (<c>04</c> section 19): its load goes back, and it stands at its station, unless it has no stamina (resting,
        /// or walking home tired): then it rests there until it is re-energized.
        /// </summary>
        internal static void StopWorking(CityState city, GameContent content, Courier courier)
        {
            GiveLoadBack(city, content, courier);
            if (courier.State == CourierState.Resting || courier.Stamina == 0)
            {
                courier.Rest();
            }
            else
            {
                courier.StandAtStation();
            }
        }

        /// <summary>
        /// Gives each part of a courier's load back to the store it came from, at most its room (a returned load, M0-C-3); what doesn't fit
        /// stays on the courier, which puts it back once it has no work and room appears (<see cref="HeldLoads.PutBackWhereRoom"/>).
        /// </summary>
        internal static void GiveLoadBack(CityState city, GameContent content, Courier courier) => HeldLoads.PutBack(city, content, courier);

        /// <summary>The <see cref="MutationErrorCode.InvalidRoute"/> checks (slot, stops, actions, neighbours), or null when they pass.</summary>
        private static MutationErrorCode? RouteShapeError(GameContent content, BuildingLevelDefinition level, int slot, IReadOnlyList<RouteStop> stops)
        {
            bool badShape = slot < 1 || slot > level.MaxWorkers || stops.Count == 1 || stops.Count > content.Rules.MaxRouteStops;
            for (int index = 0; !badShape && index < stops.Count; index++)
            {
                badShape = stops[index].Actions.Count > content.Rules.MaxStopActions;
            }

            if (badShape || (stops.Count >= 2 && Route.HasRepeatedNeighbour(stops)))
            {
                return MutationErrorCode.InvalidRoute;
            }

            return null;
        }

        /// <summary>The checks of the stops' buildings, the actions' goods and their amounts, in that order, or null when they pass.</summary>
        private static MutationErrorCode? ActionError(CityState city, GameContent content, IReadOnlyList<RouteStop> stops)
        {
            for (int index = 0; index < stops.Count; index++)
            {
                if (!city.TryGetBuilding(stops[index].BuildingId, out _))
                {
                    return MutationErrorCode.BuildingNotFound;
                }
            }

            for (int index = 0; index < stops.Count; index++)
            {
                foreach (RouteAction action in stops[index].Actions)
                {
                    if (!content.TryGetItem(action.Item, out _))
                    {
                        return MutationErrorCode.UnknownItem;
                    }

                    if (action.Item == content.Currency)
                    {
                        return MutationErrorCode.InvalidArgument;
                    }
                }
            }

            for (int index = 0; index < stops.Count; index++)
            {
                foreach (RouteAction action in stops[index].Actions)
                {
                    MutationErrorCode? amount = AmountRules.Check(city, content, action.Amount);
                    if (amount.HasValue)
                    {
                        return amount;
                    }
                }
            }

            return null;
        }
    }
}
