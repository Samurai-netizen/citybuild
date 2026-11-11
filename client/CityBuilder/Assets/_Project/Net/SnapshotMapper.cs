#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Turns a snapshot answer into the engine's terms and hands it to the <see cref="ClientCityStore"/>. Two stored fields are not in the
    /// answer, and both follow from what is: an upgrade's target level is the level plus 1, and the next building and trip ids only matter
    /// when placing a building or starting a trip, which only the server does, so the ids above the highest serve. Everything else the answer
    /// holds beyond the stored city (workers, builders, totals, store limits, projects, stall reasons) is derived and <c>CityQueries</c>
    /// computes it again.
    /// </summary>
    public static class SnapshotMapper
    {
        /// <summary>
        /// Gives the store what a request answered: a snapshot if it succeeded. A failure, such as a command the rules refused, leaves the store
        /// as it was.
        /// </summary>
        /// <param name="store">The store.</param>
        /// <param name="response">The answer of <c>GET /city</c> or of a command.</param>
        /// <returns>Null when the answer held a usable snapshot (also one the store ignored as old); otherwise why it didn't.</returns>
        public static ApiError? Apply(ClientCityStore store, ApiResult<CitySnapshotDto> response)
        {
            return Give(store, response, replace: false);
        }

        /// <summary>
        /// Like <see cref="Apply"/>, but the store takes the snapshot whatever it holds (<see cref="ClientCityStore.Replace"/>): for the answer right after a
        /// new login, which may be another city.
        /// </summary>
        /// <param name="store">The store that takes the snapshot.</param>
        /// <param name="response">The API's answer.</param>
        /// <returns>Null when the answer was a valid city, otherwise why it wasn't.</returns>
        public static ApiError? Replace(ClientCityStore store, ApiResult<CitySnapshotDto> response)
        {
            return Give(store, response, replace: true);
        }

        /// <summary>Maps the answer and hands it to the store, unless the answer is a failure or no valid city.</summary>
        private static ApiError? Give(ClientCityStore store, ApiResult<CitySnapshotDto> response, bool replace)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (!response.IsSuccess)
            {
                return response.Error;
            }

            ApiResult<CitySnapshot> snapshot = ToSnapshot(response.Value!);
            if (!snapshot.IsSuccess)
            {
                return snapshot.Error;
            }

            if (replace)
            {
                store.Replace(snapshot.Value!);
            }
            else
            {
                store.Apply(snapshot.Value!);
            }

            return null;
        }

        /// <summary>Maps an answer. The engine's own constructors check the values, so the client holds no city its engine could not.</summary>
        /// <param name="snapshot">The decoded answer.</param>
        /// <returns>The snapshot, or <c>CLIENT_INVALID_RESPONSE</c> when the values don't form a valid city.</returns>
        public static ApiResult<CitySnapshot> ToSnapshot(CitySnapshotDto snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            try
            {
                return ApiResult<CitySnapshot>.Success(new CitySnapshot(snapshot.StateVersion, snapshot.ContentVersion, ToCity(snapshot.City), ToSettlement(snapshot.Settlement)));
            }
            catch (ArgumentException exception)
            {
                string detail = "The snapshot is not a valid city: " + exception.Message;
                return ApiResult<CitySnapshot>.Failure(new ApiError(ApiErrorCodes.ClientInvalidResponse, 200, detail, null, snapshot.RequestId, null));
            }
        }

        /// <summary>The settlement part of the answer: item ids and research ids become the engine's, in the order the server sent them.</summary>
        /// <exception cref="ArgumentException">The values are no settlement: a negative number, a blank id.</exception>
        private static SettlementSummary ToSettlement(SnapshotSettlementDto settlement)
        {
            return new SettlementSummary(
                settlement.ElapsedSeconds,
                ToQuantities(settlement.Produced),
                ToQuantities(settlement.Consumed),
                settlement.CourierSeconds,
                settlement.TiredCouriers.ConvertAll(tired => new TiredCourier(tired.StationId, tired.Slot, tired.At)),
                settlement.CompletedBuildings,
                settlement.CompletedResearch.ConvertAll(id => new ResearchId(id)),
                settlement.EraChanged,
                ToQuantities(settlement.Delivered),
                ToQuantities(settlement.Moved));
        }

        /// <summary>Item id to quantity as the engine's pairs, in the dictionary's order (the server sends them sorted).</summary>
        private static List<KeyValuePair<ItemId, long>> ToQuantities(Dictionary<string, long> quantities)
        {
            var lines = new List<KeyValuePair<ItemId, long>>(quantities.Count);
            foreach (KeyValuePair<string, long> line in quantities)
            {
                lines.Add(new KeyValuePair<ItemId, long>(new ItemId(line.Key), line.Value));
            }

            return lines;
        }

        /// <summary>The city part of the answer.</summary>
        /// <exception cref="ArgumentException">The values don't form a valid city.</exception>
        private static CityState ToCity(SnapshotCityDto city)
        {
            int highestId = 0;
            var buildings = new List<Building>(city.Buildings.Count);
            foreach (SnapshotBuildingDto building in city.Buildings)
            {
                highestId = Math.Max(highestId, building.Id);
                buildings.Add(ToBuilding(building));
            }

            var roads = new List<RoadTile>(city.Roads.Count);
            foreach (SnapshotRoadDto road in city.Roads)
            {
                roads.Add(new RoadTile(road.X, road.Y, road.Level));
            }

            int highestTripId = 0;
            var trips = new List<Trip>(city.Trips.Count);
            foreach (SnapshotTripDto trip in city.Trips)
            {
                highestTripId = Math.Max(highestTripId, trip.Id);
                trips.Add(new Trip(trip.Id, trip.SourceId, trip.TargetId, new ItemId(trip.ItemId), trip.Quantity, trip.DepartedAt, trip.ArrivesAt));
            }

            var couriers = new List<Courier>(city.Couriers.Count);
            foreach (SnapshotCourierDto courier in city.Couriers)
            {
                couriers.Add(ToCourier(courier));
            }

            var routes = new List<Route>(city.Routes.Count);
            foreach (SnapshotRouteDto route in city.Routes)
            {
                routes.Add(ToRoute(route));
            }

            return new CityState(
                city.Clock,
                city.Era,
                highestId + 1,
                city.Purse,
                buildings,
                city.Research.Completed.ConvertAll(id => new ResearchId(id)),
                ToActiveResearch(city.Research),
                roads,
                trips,
                highestTripId + 1,
                couriers,
                routes);
        }

        /// <summary>One courier; the state names match the engine's one to one (CR-0008).</summary>
        /// <exception cref="ArgumentException">The values don't form a valid courier.</exception>
        private static Courier ToCourier(SnapshotCourierDto courier)
        {
            var loads = new List<CourierLoad>(courier.Load.Count);
            foreach (SnapshotCourierLoadDto part in courier.Load)
            {
                loads.Add(new CourierLoad(new ItemId(part.ItemId), part.FromId, part.Quantity));
            }

            return new Courier(
                courier.StationId, courier.Slot, ToCourierState(courier.State), courier.ToStop, courier.DepartedAt, courier.ArrivesAt, courier.Stamina, loads);
        }

        /// <summary>A courier's state in the engine's form.</summary>
        /// <exception cref="ArgumentException">The state is none the engine knows.</exception>
        private static CourierState ToCourierState(CourierStateDto state)
        {
            switch (state)
            {
                case CourierStateDto.AtStation:
                    return CourierState.AtStation;
                case CourierStateDto.Walking:
                    return CourierState.Walking;
                case CourierStateDto.Returning:
                    return CourierState.Returning;
                case CourierStateDto.Resting:
                    return CourierState.Resting;
                default:
                    throw new ArgumentException("Unknown courier state: " + state);
            }
        }

        /// <summary>One slot's route: its stops and actions in order, each amount parsed from the API's form (CR-0008).</summary>
        /// <exception cref="ArgumentException">The values don't form a valid route, or an amount has no valid form.</exception>
        private static Route ToRoute(SnapshotRouteDto route)
        {
            var stops = new List<RouteStop>(route.Stops.Count);
            foreach (SnapshotRouteStopDto stop in route.Stops)
            {
                var actions = new List<RouteAction>(stop.Actions.Count);
                foreach (SnapshotRouteActionDto action in stop.Actions)
                {
                    if (!Amount.TryParse(action.Amount, out Amount? amount))
                    {
                        throw new ArgumentException("Not an amount: " + action.Amount);
                    }

                    actions.Add(new RouteAction(
                        action.Kind == RouteActionKindDto.Take ? RouteActionKind.Take : RouteActionKind.Put, new ItemId(action.ItemId), amount!));
                }

                stops.Add(new RouteStop(stop.BuildingId, actions));
            }

            return new Route(route.StationId, route.Slot, stops);
        }

        /// <summary>One building; the phase names match the engine's one to one.</summary>
        /// <exception cref="ArgumentException">The values don't form a valid building.</exception>
        private static Building ToBuilding(SnapshotBuildingDto building)
        {
            BuildingPhase phase = ToPhase(building.Phase);
            return new Building(
                building.Id,
                new BuildingTypeId(building.TypeId),
                building.X,
                building.Y,
                building.Level,
                building.AssignedWorkers,
                phase,
                building.PhaseStartedAt,
                building.PhaseEndsAt,
                phase == BuildingPhase.Upgrading ? building.Level + 1 : 0,
                building.CycleLabor,
                building.LaborRemaining,
                building.SegmentStartedAt,
                ToQuantities(building.Store),
                ToQuantities(building.Needs));
        }

        /// <summary>The engine's phase of an answer's phase.</summary>
        /// <exception cref="ArgumentException">The value has no engine phase.</exception>
        private static BuildingPhase ToPhase(BuildingPhaseDto phase)
        {
            switch (phase)
            {
                case BuildingPhaseDto.Constructing:
                    return BuildingPhase.Constructing;
                case BuildingPhaseDto.Upgrading:
                    return BuildingPhase.Upgrading;
                case BuildingPhaseDto.Idle:
                    return BuildingPhase.Idle;
                case BuildingPhaseDto.Producing:
                    return BuildingPhase.Producing;
                default:
                    throw new ArgumentException("Unknown building phase: " + phase);
            }
        }

        /// <summary>The running research with its needs (a research waiting for goods has no times; CR-0008), or null when the slot is free.</summary>
        /// <exception cref="ArgumentException">The times and needs don't form a research the engine takes.</exception>
        private static ActiveResearch? ToActiveResearch(SnapshotResearchDto research)
        {
            if (research.ActiveId == null)
            {
                return null;
            }

            return new ActiveResearch(new ResearchId(research.ActiveId), research.StartedAt, research.EndsAt, ToQuantities(research.Needs));
        }
    }
}
