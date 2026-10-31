#nullable enable

using System;
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
        /// <summary>Every real snapshot maps, and the engine derives from it what the server derived: workers, builders, caps, limits and stall reasons.</summary>
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
                Assert.That(city.Inventory.ToDictionary(item => item.Key.Value, item => item.Value), Is.EquivalentTo(dto.Inventory), name);
                Assert.That(CityQueries.WorkersProvided(city, content), Is.EqualTo(dto.Workers.Provided), name);
                Assert.That(CityQueries.WorkersAssigned(city), Is.EqualTo(dto.Workers.Assigned), name);
                Assert.That(CityQueries.BuildersBusy(city), Is.EqualTo(dto.Builders.Busy), name);
                foreach (var cap in dto.StorageCaps)
                {
                    Assert.That(CityQueries.TryGetEffectiveCap(city, content, new ItemId(cap.Key), out long effective), Is.True, name);
                    Assert.That(effective, Is.EqualTo(cap.Value), name + " " + cap.Key);
                }

                Assert.That(city.Buildings.Select(building => building.Id), Is.EqualTo(dto.Buildings.Select(building => building.Id)), name);
                foreach (SnapshotBuildingDto building in dto.Buildings)
                {
                    Assert.That(city.TryGetBuilding(building.Id, out Building? mapped), Is.True, name);
                    Assert.That(StallReason.Of(city, content, mapped!)?.ToString(), Is.EqualTo(building.StallReason), name + " building " + building.Id);
                }
            }
        }

        /// <summary>A producing building keeps every timer and labor value, and the version and content version come along.</summary>
        [Test]
        public void ToSnapshot_ProducingBuilding_KeepsEveryValue()
        {
            CitySnapshot snapshot = CityTestData.Snapshot("snapshot-city-producing.json");
            CityState city = snapshot.CopyCity();

            Assert.That(snapshot.StateVersion, Is.EqualTo(25L));
            Assert.That(snapshot.ContentVersion, Is.EqualTo("mvp-1"));
            Assert.That(snapshot.Clock, Is.EqualTo(199L));
            Assert.That(city.TryGetBuilding(3, out Building? sawmill), Is.True);
            Assert.That(sawmill!.TypeId.Value, Is.EqualTo("sawmill"));
            Assert.That((sawmill.X, sawmill.Y, sawmill.Level, sawmill.AssignedWorkers), Is.EqualTo((2, 0, 1, 3)));
            Assert.That(sawmill.Phase, Is.EqualTo(BuildingPhase.Producing));
            Assert.That((sawmill.PhaseStartedAt, sawmill.PhaseEndsAt), Is.EqualTo((183L, (long?)243)));
            Assert.That((sawmill.CycleLabor, sawmill.LaborRemaining, sawmill.SegmentStartedAt), Is.EqualTo((180L, 138L, 197L)));
            Assert.That(sawmill.TargetLevel, Is.EqualTo(0));
            Assert.That(city.NextBuildingId, Is.EqualTo(5));
        }

        /// <summary>The running research and the completed list come along.</summary>
        [Test]
        public void ToSnapshot_Research_IsKept()
        {
            CityState running = CityTestData.Snapshot("snapshot-city-producing.json").CopyCity();
            CityState completed = CityTestData.Snapshot("snapshot-city-research-completed.json").CopyCity();

            Assert.That(running.ActiveResearch!.Id.Value, Is.EqualTo("trade_charter"));
            Assert.That((running.ActiveResearch.StartedAt, running.ActiveResearch.EndsAt), Is.EqualTo((4L, 604L)));
            Assert.That(completed.CompletedResearch.Select(id => id.Value), Does.Contain("trade_charter"));
        }

        /// <summary>The answer has no target level; an upgrading building gets its level plus 1, and the engine completes the upgrade to it.</summary>
        [Test]
        public void ToSnapshot_UpgradingBuilding_TargetsTheNextLevel()
        {
            GameContent content = CityTestData.Content();
            CitySnapshotDto dto = CityTestData.Dto("snapshot-city-producing.json");
            dto.City.Buildings[0] = new SnapshotBuildingDto(1, "house", 0, 0, 1, 0, 0, BuildingPhaseDto.Upgrading, 190, 210, 0, 0, 0, null);

            CityState city = SnapshotMapper.ToSnapshot(dto).Value!.CopyCity();

            Assert.That(city.TryGetBuilding(1, out Building? house), Is.True);
            Assert.That((house!.Phase, house.Level, house.TargetLevel), Is.EqualTo((BuildingPhase.Upgrading, 1, 2)));
            CityBuilder.Simulation.Settlement.Simulator.Advance(city, content, 11);
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

        /// <summary>Values no city can have are a broken answer, reported as such: two buildings on a tile, a producing building whose end doesn't follow from its labor, research without times.</summary>
        [Test]
        public void ToSnapshot_ValuesThatFormNoCity_AreAnInvalidResponse()
        {
            CitySnapshotDto sameTile = CityTestData.Dto("snapshot-city-producing.json");
            sameTile.City.Buildings[1] = new SnapshotBuildingDto(2, "house", 0, 0, 1, 0, 0, BuildingPhaseDto.Idle, 0, null, 0, 0, 0, "NoRecipe");
            CitySnapshotDto badTimer = CityTestData.Dto("snapshot-city-producing.json");
            badTimer.City.Buildings[2] = new SnapshotBuildingDto(3, "sawmill", 2, 0, 1, 3, 3, BuildingPhaseDto.Producing, 183, 999, 180, 138, 197, null);
            CitySnapshotDto noTimes = CityTestData.Dto("snapshot-city-start.json");
            CitySnapshotDto researchWithoutTimes = new CitySnapshotDto(
                noTimes.RequestId,
                noTimes.StateVersion,
                noTimes.ServerTimeUtc,
                noTimes.ContentVersion,
                new SnapshotCityDto(
                    noTimes.City.Clock,
                    noTimes.City.Era,
                    noTimes.City.Inventory,
                    noTimes.City.StorageCaps,
                    noTimes.City.Workers,
                    noTimes.City.Builders,
                    new SnapshotResearchDto("trade_charter", null, null, noTimes.City.Research.Completed),
                    noTimes.City.Buildings),
                noTimes.Settlement);

            foreach (CitySnapshotDto broken in new[] { sameTile, badTimer, researchWithoutTimes })
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
            Assert.That(store.Snapshot!.StateVersion, Is.EqualTo(25L));
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
            var api = new ApiClient("http://localhost:8080", 15, transport);
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
            broken.City.Buildings[1] = new SnapshotBuildingDto(1, "house", 1, 0, 1, 0, 0, BuildingPhaseDto.Idle, 0, null, 0, 0, 0, "NoRecipe");

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
            Assert.That(store.Snapshot!.StateVersion, Is.EqualTo(25L));
        }
    }
}
