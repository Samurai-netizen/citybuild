#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Tests.EditMode.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// <see cref="SnapshotMapper"/> (M0-39): a real answer becomes the engine's city with nothing lost, proved by the engine computing the same derived
    /// values the server sent; a refused or broken answer never reaches the store.
    /// </summary>
    public sealed class SnapshotMapperTests
    {
        /// <summary>
        /// Every real snapshot maps, and the engine derives from it what the server derived: the purse and totals, workers (on trips too), builders,
        /// each store, its limits and needs, the project, the stall reasons, roads and trips (CR-0008).
        /// </summary>
        [Test]
        public void ToSnapshot_EveryRealAnswer_GivesTheServersDerivedValues()
        {
            GameContent content = CityTestData.Content();
            string[] names = Fixtures.Names("snapshot-");
            Assert.That(names, Is.Not.Empty);

            foreach (string name in names)
            {
                SnapshotCityDto dto = CityTestData.Dto(name).City;
                CityState city = CityTestData.Snapshot(name).CopyCity();

                Assert.That(city.Clock, Is.EqualTo(dto.Clock), name);
                Assert.That(city.Era, Is.EqualTo(dto.Era), name);
                Assert.That(city.Purse, Is.EqualTo(dto.Purse), name);
                Assert.That(
                    CityQueries.Totals(city, content).ToDictionary(line => line.Item.Value, line => line.Quantity),
                    Is.EquivalentTo(dto.Totals),
                    name);
                Assert.That(CityQueries.WorkersProvided(city, content), Is.EqualTo(dto.Workers.Provided), name);
                Assert.That(CityQueries.WorkersAssigned(city), Is.EqualTo(dto.Workers.Assigned), name);
                Assert.That((long)city.Trips.Count, Is.EqualTo(dto.Workers.OnTrips), name);
                Assert.That(CityQueries.FreeWorkers(city, content), Is.EqualTo(dto.Workers.Free), name);
                Assert.That(CityQueries.BuildersBusy(city), Is.EqualTo(dto.Builders.Busy), name);
                Assert.That(Amounts(city.ActiveResearch?.Needs), Is.EquivalentTo(dto.Research.Needs), name);

                Assert.That(city.Buildings.Select(building => building.Id), Is.EqualTo(dto.Buildings.Select(building => building.Id)), name);
                foreach (SnapshotBuildingDto building in dto.Buildings)
                {
                    string where = name + " building " + building.Id;
                    Assert.That(city.TryGetBuilding(building.Id, out Building? mapped), Is.True, name);
                    Assert.That(StallReason.Of(city, content, mapped!)?.ToString(), Is.EqualTo(building.StallReason), where);
                    Assert.That(Amounts(mapped!.Store), Is.EquivalentTo(building.Store), where);
                    Assert.That(Amounts(mapped.Needs), Is.EquivalentTo(building.Needs), where);
                    Assert.That(ProjectOf(mapped), Is.EqualTo(building.Project), where);
                    foreach (KeyValuePair<string, long> limit in building.StoreLimits)
                    {
                        Assert.That(CityQueries.TryGetEffectiveLimit(city, content, mapped, new ItemId(limit.Key), out long effective), Is.True, where);
                        Assert.That(effective, Is.EqualTo(limit.Value), where + " " + limit.Key);
                    }
                }

                Assert.That(
                    city.Trips.Select(trip => (trip.Id, trip.SourceId, trip.TargetId, trip.Item.Value, trip.Quantity, trip.DepartedAt, trip.ArrivesAt)),
                    Is.EqualTo(dto.Trips.Select(trip => (trip.Id, trip.SourceId, trip.TargetId, trip.ItemId, trip.Quantity, trip.DepartedAt, trip.ArrivesAt))),
                    name);

                Assert.That(
                    city.Roads.Select(road => (road.X, road.Y, road.Level)),
                    Is.EqualTo(dto.Roads.Select(road => (road.X, road.Y, road.Level))),
                    name);
            }
        }

        /// <summary>The answer's roads reach the engine's city with their tiles and levels, so the client's map is the server's (CR-0008).</summary>
        [Test]
        public void ToSnapshot_Roads_AreKept()
        {
            CitySnapshotDto dto = CityTestData.Dto("snapshot-city-producing.json");
            dto.City.Roads.Add(new SnapshotRoadDto(4, 4, 2));
            dto.City.Roads.Add(new SnapshotRoadDto(5, 4, 1));

            CityState city = SnapshotMapper.ToSnapshot(dto).Value!.CopyCity();

            Assert.That(city.Roads.Select(road => (road.X, road.Y, road.Level)), Is.EqualTo(new[] { (4, 4, 2), (5, 4, 1) }));
            Assert.That(CityQueries.TileAt(city, 4, 4), Is.EqualTo(TileContent.Road));
        }

        /// <summary>A producing building keeps every timer and labor value and its store, and the version and content version come along.</summary>
        [Test]
        public void ToSnapshot_ProducingBuilding_KeepsEveryValue()
        {
            CitySnapshot snapshot = CityTestData.Snapshot("snapshot-city-producing.json");
            CityState city = snapshot.CopyCity();

            Assert.That(snapshot.StateVersion, Is.EqualTo(10L));
            Assert.That(snapshot.ContentVersion, Is.EqualTo("mvp-2"));
            Assert.That(snapshot.Clock, Is.EqualTo(196L));
            Assert.That(city.TryGetBuilding(2, out Building? woodcutter), Is.True);
            Assert.That(woodcutter!.TypeId.Value, Is.EqualTo("woodcutter"));
            Assert.That((woodcutter.X, woodcutter.Y, woodcutter.Level, woodcutter.AssignedWorkers), Is.EqualTo((1, 0, 1, 2)));
            Assert.That(woodcutter.Phase, Is.EqualTo(BuildingPhase.Producing));
            Assert.That((woodcutter.PhaseStartedAt, woodcutter.PhaseEndsAt), Is.EqualTo((183L, (long?)213)));
            Assert.That((woodcutter.CycleLabor, woodcutter.LaborRemaining, woodcutter.SegmentStartedAt), Is.EqualTo((60L, 60L, 183L)));
            Assert.That(woodcutter.TargetLevel, Is.EqualTo(0));
            Assert.That(woodcutter.Store[new ItemId("logs")], Is.EqualTo(4L));
            Assert.That((city.Purse, city.NextBuildingId, city.NextTripId), Is.EqualTo((420L, 5, 1)));
        }

        /// <summary>A construction site keeps its needs and has no timer; a trip keeps every value and the next trip id follows it (CR-0008).</summary>
        [Test]
        public void ToSnapshot_SiteAndTrip_AreKept()
        {
            CityState city = CityTestData.Snapshot("snapshot-command-start-trip.json").CopyCity();

            Assert.That(city.TryGetBuilding(4, out Building? site), Is.True);
            Assert.That((site!.Phase, site.PhaseEndsAt, site.IsConstructionSite), Is.EqualTo((BuildingPhase.Constructing, (long?)null, true)));
            Assert.That(site.Needs[new ItemId("logs")], Is.EqualTo(10L));
            Trip trip = city.Trips.Single();
            Assert.That((trip.Id, trip.SourceId, trip.TargetId, trip.Item.Value, trip.Quantity), Is.EqualTo((1, 2, 4, "logs", 4L)));
            Assert.That((trip.DepartedAt, trip.ArrivesAt, city.NextTripId), Is.EqualTo((197L, 217L, 2)));
        }

        /// <summary>The couriers and the route keep every value, in the engine's types (CR-0008-T1 part G).</summary>
        [Test]
        public void ToSnapshot_CouriersAndRoute_AreKept()
        {
            CityState city = CityTestData.Snapshot("snapshot-command-set-route.json").CopyCity();

            Assert.That(city.TryGetRoute(5, 1, out Route? route), Is.True);
            Assert.That(route!.Stops.Select(stop => stop.BuildingId), Is.EqualTo(new[] { 2, 4 }));
            RouteAction take = route.Stops[0].Actions.Single();
            Assert.That((take.Kind, take.Item.Value, take.Amount.ToString()), Is.EqualTo((RouteActionKind.Take, "logs", "all")));
            Assert.That(route.Stops[1].Actions.Single().Kind, Is.EqualTo(RouteActionKind.Put));
            Courier walking = city.Couriers[0];
            Assert.That(
                (walking.StationId, walking.Slot, walking.State, walking.ToStop, walking.DepartedAt, walking.ArrivesAt, walking.Stamina),
                Is.EqualTo((5, 1, CourierState.Walking, 1, (long?)894, (long?)904, 14400L)));
            Assert.That((city.Couriers[1].Slot, city.Couriers[1].State), Is.EqualTo((2, CourierState.AtStation)));
        }

        /// <summary>The running research and the completed list come along.</summary>
        [Test]
        public void ToSnapshot_Research_IsKept()
        {
            CityState running = CityTestData.Snapshot("snapshot-command-start-research.json").CopyCity();
            CityState completed = CityTestData.Snapshot("snapshot-city-research-completed.json").CopyCity();

            Assert.That(running.ActiveResearch!.Id.Value, Is.EqualTo("trade_charter"));
            Assert.That((running.ActiveResearch.StartedAt, running.ActiveResearch.EndsAt), Is.EqualTo(((long?)197, (long?)797)));
            Assert.That(completed.CompletedResearch.Select(id => id.Value), Does.Contain("trade_charter"));
        }

        /// <summary>A research that waits for goods at the science building has no times and keeps its needs (CR-0008).</summary>
        [Test]
        public void ToSnapshot_WaitingResearch_KeepsItsNeedsWithoutTimes()
        {
            CitySnapshotDto dto = CityTestData.Dto("snapshot-city-start.json");
            dto.City.Research.Needs.Add("planks", 30);
            CitySnapshotDto waiting = WithResearch(dto, new SnapshotResearchDto("frame_saw", null, null, dto.City.Research.Needs, dto.City.Research.Completed));

            CityState city = SnapshotMapper.ToSnapshot(waiting).Value!.CopyCity();

            Assert.That((city.ActiveResearch!.StartedAt, city.ActiveResearch.EndsAt), Is.EqualTo(((long?)null, (long?)null)));
            Assert.That(city.ActiveResearch.Needs[new ItemId("planks")], Is.EqualTo(30L));
        }

        /// <summary>The settlement block of a real answer comes along in the engine's terms: times, items per name, completed buildings and research.</summary>
        [Test]
        public void ToSnapshot_Settlement_IsKept()
        {
            SettlementSummary research = CityTestData.Snapshot("snapshot-city-research-completed.json").Settlement!;
            SettlementSummary buildings = CityTestData.Snapshot("snapshot-city-settled-buildings.json").Settlement!;

            Assert.That((research.ElapsedSeconds, research.ProductionSeconds), Is.EqualTo((620L, 620L)));
            Assert.That(research.Produced.Select(line => line.Key.Value + "=" + line.Value), Is.EqualTo(new[] { "logs=21" }));
            Assert.That(research.Consumed, Is.Empty);
            Assert.That(research.CompletedResearch.Select(id => id.Value), Is.EqualTo(new[] { "trade_charter" }));
            Assert.That(research.EraChanged, Is.False);
            Assert.That(buildings.CompletedBuildings, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(buildings.Produced, Is.Empty);
        }

        /// <summary>A settlement that says production counted more seconds than passed is a broken answer.</summary>
        [Test]
        public void ToSnapshot_ProductionAboveElapsed_IsAnInvalidResponse()
        {
            CitySnapshotDto good = CityTestData.Dto("snapshot-city-research-completed.json");
            var settlement = new SnapshotSettlementDto(
                10,
                11,
                good.Settlement.Produced,
                good.Settlement.Consumed,
                good.Settlement.Delivered,
                good.Settlement.Moved,
                good.Settlement.Refunded,
                good.Settlement.CompletedBuildings,
                good.Settlement.CompletedResearch,
                false);
            var broken = new CitySnapshotDto(good.RequestId, good.StateVersion, good.ServerTimeUtc, good.ContentVersion, good.City, settlement);

            ApiResult<CitySnapshot> result = SnapshotMapper.ToSnapshot(broken);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
        }

        /// <summary>The answer has no target level; an upgrading building gets its level plus 1, and the engine completes the upgrade to it.</summary>
        [Test]
        public void ToSnapshot_UpgradingBuilding_TargetsTheNextLevel()
        {
            GameContent content = CityTestData.Content();
            CitySnapshotDto dto = CityTestData.Dto("snapshot-city-producing.json");
            dto.City.Buildings[0] = Building(1, "house", 0, 0, 0, 0, BuildingPhaseDto.Upgrading, 190, 210, 0, 0, 0, null);

            CityState city = SnapshotMapper.ToSnapshot(dto).Value!.CopyCity();

            Assert.That(city.TryGetBuilding(1, out Building? house), Is.True);
            Assert.That((house!.Phase, house.Level, house.TargetLevel), Is.EqualTo((BuildingPhase.Upgrading, 1, 2)));
            CityBuilder.Simulation.Settlement.Simulator.Advance(city, content, 14);
            Assert.That((house.Phase, house.Level, house.TargetLevel), Is.EqualTo((BuildingPhase.Idle, 2, 0)));
        }

        /// <summary>A city without buildings gets the first building id.</summary>
        [Test]
        public void ToSnapshot_NoBuildings_NextIdIsOne()
        {
            CitySnapshotDto dto = CityTestData.Dto("snapshot-city-start.json");
            dto.City.Buildings.Clear();

            Assert.That(SnapshotMapper.ToSnapshot(dto).Value!.CopyCity().NextBuildingId, Is.EqualTo(1));
        }

        /// <summary>
        /// Values no city can have are a broken answer, reported as such: two buildings on a tile, a producing building whose end doesn't follow
        /// from its labor, research without times and without needs, a road on a building's tile.
        /// </summary>
        [Test]
        public void ToSnapshot_ValuesThatFormNoCity_AreAnInvalidResponse()
        {
            CitySnapshotDto sameTile = CityTestData.Dto("snapshot-city-producing.json");
            sameTile.City.Buildings[1] = Building(2, "house", 0, 0, 0, 0, BuildingPhaseDto.Idle, 0, null, 0, 0, 0, "NoRecipe");
            CitySnapshotDto badTimer = CityTestData.Dto("snapshot-city-producing.json");
            badTimer.City.Buildings[1] = Building(2, "woodcutter", 1, 0, 2, 2, BuildingPhaseDto.Producing, 183, 999, 60, 60, 183, null);
            CitySnapshotDto noTimes = CityTestData.Dto("snapshot-city-start.json");
            CitySnapshotDto researchWithoutTimes = WithResearch(
                noTimes, new SnapshotResearchDto("trade_charter", null, null, new Dictionary<string, long>(), noTimes.City.Research.Completed));
            CitySnapshotDto roadOnABuilding = CityTestData.Dto("snapshot-city-producing.json");
            SnapshotBuildingDto first = roadOnABuilding.City.Buildings[0];
            roadOnABuilding.City.Roads.Add(new SnapshotRoadDto(first.X, first.Y, 1));

            foreach (CitySnapshotDto broken in new[] { sameTile, badTimer, researchWithoutTimes, roadOnABuilding })
            {
                ApiResult<CitySnapshot> result = SnapshotMapper.ToSnapshot(broken);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
                Assert.That(result.Error.Status, Is.EqualTo(200));
            }
        }

        /// <summary>A good answer reaches the store.</summary>
        [Test]
        public void Apply_ASnapshot_ReachesTheStore()
        {
            var store = new ClientCityStore(() => 0);

            ApiError? error = SnapshotMapper.Apply(store, ApiResult<CitySnapshotDto>.Success(CityTestData.Dto("snapshot-city-producing.json")));

            Assert.That(error, Is.Null);
            Assert.That(store.Snapshot!.StateVersion, Is.EqualTo(10L));
        }

        /// <summary>A command the rules refuse, over the real client and a real refusal, leaves the store exactly as it was, and says why.</summary>
        [Test]
        public async Task Apply_ARefusedCommand_LeavesTheStoreUnchanged()
        {
            double now = 10;
            var store = new ClientCityStore(() => now);
            int changes = 0;
            SnapshotMapper.Apply(store, ApiResult<CitySnapshotDto>.Success(CityTestData.Dto("snapshot-city-producing.json")));
            CitySnapshot held = store.Snapshot!;
            store.SnapshotChanged += () => changes++;
            var transport = new FakeTransport().Answer(409, Fixtures.Read("error-409-TILE_OCCUPIED.json"));
            var api = new ApiClient("http://localhost:8080", 15, transport, allowInsecureHttp: true);
            api.SetAccessToken("tok-1");
            now = 20;

            ApiResult<CitySnapshotDto> refused = await api.PlaceBuildingAsync(new PlaceBuildingRequestDto(Guid.NewGuid(), "house", 0, 0), CancellationToken.None);
            ApiError? error = SnapshotMapper.Apply(store, refused);

            Assert.That(error!.Code, Is.EqualTo("TILE_OCCUPIED"));
            Assert.That(store.Snapshot, Is.SameAs(held));
            Assert.That(store.ReceivedAtSeconds, Is.EqualTo(10d));
            Assert.That(changes, Is.EqualTo(0));
        }

        /// <summary>A broken answer is reported and leaves the store unchanged.</summary>
        [Test]
        public void Apply_ABrokenSnapshot_LeavesTheStoreUnchanged()
        {
            var store = new ClientCityStore(() => 0);
            CitySnapshotDto broken = CityTestData.Dto("snapshot-city-producing.json");
            broken.City.Buildings[1] = Building(1, "house", 1, 0, 0, 0, BuildingPhaseDto.Idle, 0, null, 0, 0, 0, "NoRecipe");

            ApiError? error = SnapshotMapper.Apply(store, ApiResult<CitySnapshotDto>.Success(broken));

            Assert.That(error!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(store.Snapshot, Is.Null);
        }

        /// <summary>An old answer is no error: the store just ignores it.</summary>
        [Test]
        public void Apply_AnOlderAnswer_IsNoErrorAndIsIgnored()
        {
            var store = new ClientCityStore(() => 0);
            SnapshotMapper.Apply(store, ApiResult<CitySnapshotDto>.Success(CityTestData.Dto("snapshot-city-producing.json")));

            ApiError? error = SnapshotMapper.Apply(store, ApiResult<CitySnapshotDto>.Success(CityTestData.Dto("snapshot-sawmill-constructing.json")));

            Assert.That(error, Is.Null);
            Assert.That(store.Snapshot!.StateVersion, Is.EqualTo(10L));
        }

        /// <summary>A level-1 building's answer with the given values and no store, limits, needs or project.</summary>
        private static SnapshotBuildingDto Building(
            int id, string typeId, int x, int y, int assigned, int max, BuildingPhaseDto phase, long started, long? ends, long cycle, long remaining,
            long segment, string? stall)
        {
            return new SnapshotBuildingDto(
                id, typeId, x, y, 1, assigned, max, phase, started, ends, cycle, remaining, segment, stall, new Dictionary<string, long>(),
                new Dictionary<string, long>(), new Dictionary<string, long>(), null);
        }

        /// <summary><paramref name="dto"/> with another research block.</summary>
        private static CitySnapshotDto WithResearch(CitySnapshotDto dto, SnapshotResearchDto research)
        {
            SnapshotCityDto city = dto.City;
            return new CitySnapshotDto(
                dto.RequestId,
                dto.StateVersion,
                dto.ServerTimeUtc,
                dto.ContentVersion,
                new SnapshotCityDto(
                    city.Clock, city.Era, city.Purse, city.Totals, city.Workers, city.Builders, research, city.Buildings, city.Roads, city.Trips,
                    city.Couriers, city.Routes),
                dto.Settlement);
        }

        /// <summary>Quantities as the answer writes them: item id to quantity; none for null.</summary>
        private static Dictionary<string, long> Amounts(IReadOnlyDictionary<ItemId, long>? amounts)
        {
            var result = new Dictionary<string, long>();
            foreach (KeyValuePair<ItemId, long> amount in amounts ?? new Dictionary<ItemId, long>())
            {
                result.Add(amount.Key.Value, amount.Value);
            }

            return result;
        }

        /// <summary>The project the answer names for <paramref name="building"/>: a site's construction, a waiting upgrade, or none.</summary>
        private static string? ProjectOf(Building building)
        {
            return building.IsConstructionSite ? "Construction" : building.HasUpgradeWaiting ? "Upgrade" : null;
        }
    }
}
