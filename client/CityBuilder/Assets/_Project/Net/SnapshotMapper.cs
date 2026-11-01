#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Turns a snapshot answer into the engine's terms and hands it to the <see cref="ClientCityStore"/>. Two stored fields are not in the
    /// answer, and both follow from what is: an upgrade's target level is the level plus 1, and the next building id only matters when placing
    /// a building, which only the server does, so the id above the highest building serves. Everything else the answer holds beyond the stored city (workers, builders, caps, stall reasons) is derived
    /// and <c>CityQueries</c> computes it again.
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
        /// <exception cref="ArgumentException">The values are no settlement: a negative number, production above the elapsed time, a blank id.</exception>
        private static SettlementSummary ToSettlement(SnapshotSettlementDto settlement)
        {
            return new SettlementSummary(
                settlement.ElapsedSeconds,
                settlement.ProductionSeconds,
                ToQuantities(settlement.Produced),
                ToQuantities(settlement.Consumed),
                settlement.CompletedBuildings,
                settlement.CompletedResearch.ConvertAll(id => new ResearchId(id)),
                settlement.EraChanged);
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
            var inventory = new List<KeyValuePair<ItemId, long>>(city.Inventory.Count);
            foreach (KeyValuePair<string, long> stock in city.Inventory)
            {
                inventory.Add(new KeyValuePair<ItemId, long>(new ItemId(stock.Key), stock.Value));
            }

            int highestId = 0;
            var buildings = new List<Building>(city.Buildings.Count);
            foreach (SnapshotBuildingDto building in city.Buildings)
            {
                highestId = Math.Max(highestId, building.Id);
                buildings.Add(ToBuilding(building));
            }

            return new CityState(
                city.Clock,
                city.Era,
                highestId + 1,
                inventory,
                buildings,
                city.Research.Completed.ConvertAll(id => new ResearchId(id)),
                ToActiveResearch(city.Research));
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
                building.SegmentStartedAt);
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

        /// <summary>The running research, or null when the slot is free.</summary>
        /// <exception cref="ArgumentException">A running research lacks its times.</exception>
        private static ActiveResearch? ToActiveResearch(SnapshotResearchDto research)
        {
            if (research.ActiveId == null)
            {
                return null;
            }

            if (research.StartedAt == null || research.EndsAt == null)
            {
                throw new ArgumentException("Research " + research.ActiveId + " is running but has no start or end.");
            }

            return new ActiveResearch(new ResearchId(research.ActiveId), research.StartedAt.Value, research.EndsAt.Value);
        }
    }
}
