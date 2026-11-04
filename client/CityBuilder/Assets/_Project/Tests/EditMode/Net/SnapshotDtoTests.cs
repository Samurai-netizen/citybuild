using System;
using System.Collections.Generic;
using System.Linq;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// The snapshot DTOs (<c>05</c> Snapshot) against real answers of the development server: <c>snapshot-*.json</c> in
    /// <c>Tests/Fixtures</c>. Each fixture must read, hold the values the request that made it should give, and write back to the same JSON.
    /// </summary>
    public sealed class SnapshotDtoTests
    {
        /// <summary>The reader and writer under test.</summary>
        private readonly ApiJson _json = new ApiJson();

        /// <summary>
        /// Every snapshot fixture reads, and writing the DTO again gives the same text, byte for byte: the DTOs mirror the answer member
        /// for member and in the server's order, nulls included.
        /// </summary>
        [Test]
        public void EverySnapshotFixture_ReadsAndWritesBackToTheSameJson([ValueSource(nameof(SnapshotFixtureNames))] string name)
        {
            string text = Fixtures.Read(name);

            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(text);

            Assert.That(snapshot, Is.Not.Null);
            Assert.That(_json.Serialize(snapshot), Is.EqualTo(text), name + " does not round trip");
        }

        /// <summary>The names of every <c>snapshot-*.json</c> fixture.</summary>
        public static string[] SnapshotFixtureNames()
        {
            return Fixtures.Names("snapshot-");
        }

        /// <summary>
        /// A starter city: coins in the purse and no goods (CR-0008), no buildings, roads, trips or research, <c>requestId</c> null because
        /// <c>GET /city</c> is no command.
        /// </summary>
        [Test]
        public void CityStart_HasTheStarterCityAndNoRequestId()
        {
            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(Fixtures.Read("snapshot-city-start.json"));

            Assert.That(snapshot.RequestId, Is.Null);
            Assert.That(snapshot.ContentVersion, Is.EqualTo("mvp-2"));
            Assert.That(snapshot.City.Era, Is.EqualTo(1));
            Assert.That(snapshot.City.Purse, Is.EqualTo(800L));
            Assert.That(snapshot.City.Totals, Is.EqualTo(new Dictionary<string, long> { ["coins"] = 800, ["logs"] = 0, ["planks"] = 0 }));
            Assert.That(snapshot.City.Buildings, Is.Empty);
            Assert.That(snapshot.City.Roads, Is.Empty);
            Assert.That(snapshot.City.Trips, Is.Empty);
            Assert.That(snapshot.City.Research.Needs, Is.Empty);
            Assert.That(snapshot.City.Research.ActiveId, Is.Null);
            Assert.That(snapshot.City.Research.StartedAt, Is.Null);
            Assert.That(snapshot.City.Research.Completed, Is.Empty);
        }

        /// <summary>
        /// The <c>roads</c> list in the server's form (<c>x</c>, <c>y</c>, <c>level</c>, sorted by tile) reads into its DTOs and writes back to the same
        /// text (CR-0008).
        /// </summary>
        [Test]
        public void Roads_ReadAndWriteBackToTheSameJson()
        {
            const string roads = "\"roads\":[{\"x\":1,\"y\":3,\"level\":2},{\"x\":4,\"y\":0,\"level\":1}]";
            string text = Fixtures.Read("snapshot-city-start.json").Replace("\"roads\":[]", roads);
            Assert.That(text, Does.Contain(roads), "the fixture changed: update this test");

            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(text);

            Assert.That(snapshot.City.Roads.ConvertAll(road => (road.X, road.Y, road.Level)), Is.EqualTo(new[] { (1, 3, 2), (4, 0, 1) }));
            Assert.That(_json.Serialize(snapshot), Is.EqualTo(text));
        }

        /// <summary>A command's snapshot carries the command's request id, and the replay of a command is the same text and so the same snapshot.</summary>
        [Test]
        public void PlaceBuilding_CarriesTheRequestId_AndItsReplayIsIdentical()
        {
            string first = Fixtures.Read("snapshot-command-place-building.json");
            string replay = Fixtures.Read("snapshot-replay-place-building.json");

            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(first);

            Assert.That(snapshot.RequestId, Is.Not.Null.And.Not.EqualTo(Guid.Empty));
            Assert.That(snapshot.City.Buildings, Has.Count.EqualTo(1));
            Assert.That(snapshot.City.Buildings[0].Phase, Is.EqualTo(BuildingPhaseDto.Constructing));
            Assert.That(replay, Is.EqualTo(first), "a replay is the stored answer byte for byte (05 Idempotency rules)");
        }

        /// <summary>A running research shows its id and its start and end second.</summary>
        [Test]
        public void StartResearch_ShowsTheRunningResearch()
        {
            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(Fixtures.Read("snapshot-command-start-research.json"));

            Assert.That(snapshot.City.Research.ActiveId, Is.EqualTo("trade_charter"));
            Assert.That(snapshot.City.Research.EndsAt - snapshot.City.Research.StartedAt, Is.EqualTo(600L));
        }

        /// <summary>
        /// A city with a producer: phases read as enums, a running cycle has an end time and no stall reason, a house says <c>NoRecipe</c>, the
        /// producer's store and limits read, and a construction site waits for goods with no end time (CR-0008).
        /// </summary>
        [Test]
        public void CityProducing_ReadsPhasesStallReasonsStoresAndTheSite()
        {
            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(Fixtures.Read("snapshot-city-producing.json"));

            SnapshotBuildingDto house = snapshot.City.Buildings[0];
            SnapshotBuildingDto woodcutter = snapshot.City.Buildings[1];
            SnapshotBuildingDto site = snapshot.City.Buildings[3];
            Assert.That(house.Phase, Is.EqualTo(BuildingPhaseDto.Idle));
            Assert.That(house.StallReason, Is.EqualTo("NoRecipe"));
            Assert.That(house.PhaseEndsAt, Is.Null);
            Assert.That(house.Store, Is.Empty);
            Assert.That(house.StoreLimits, Is.Empty);
            Assert.That(house.Needs, Is.Empty);
            Assert.That(house.Project, Is.Null);
            Assert.That(woodcutter.TypeId, Is.EqualTo("woodcutter"));
            Assert.That(woodcutter.Phase, Is.EqualTo(BuildingPhaseDto.Producing));
            Assert.That(woodcutter.StallReason, Is.Null);
            Assert.That(woodcutter.PhaseEndsAt, Is.Not.Null);
            Assert.That(woodcutter.Store["logs"], Is.EqualTo(4L));
            Assert.That(woodcutter.StoreLimits["logs"], Is.EqualTo(40L));
            Assert.That((site.TypeId, site.Phase, site.PhaseEndsAt, site.Project), Is.EqualTo(("sawmill", BuildingPhaseDto.Constructing, (long?)null, "Construction")));
            Assert.That(site.Needs["logs"], Is.EqualTo(10L));
            Assert.That(snapshot.City.Totals["logs"], Is.EqualTo(4L));
            SnapshotWorkersDto workers = snapshot.City.Workers;
            Assert.That(workers.Free, Is.EqualTo(workers.Provided - workers.Assigned - workers.OnTrips));
        }

        /// <summary>A started trip reads with its source, target, good, load and times, and its worker counts as on a trip (CR-0008).</summary>
        [Test]
        public void StartTrip_ShowsTheTripAndItsWorker()
        {
            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(Fixtures.Read("snapshot-command-start-trip.json"));

            SnapshotTripDto trip = snapshot.City.Trips.Single();
            Assert.That(
                (trip.Id, trip.SourceId, trip.TargetId, trip.ItemId, trip.Quantity, trip.ArrivesAt - trip.DepartedAt),
                Is.EqualTo((1, 2, 4, "logs", 4L, 20L)));
            Assert.That((snapshot.City.Workers.OnTrips, snapshot.City.Workers.Free), Is.EqualTo((1L, 1L)));
            Assert.That(snapshot.City.Totals["logs"], Is.EqualTo(4L), "the load counts in the city's total");
        }

        /// <summary>The settlement lists what completed in the request's own time span.</summary>
        [Test]
        public void SettlementFixtures_ListWhatCompleted()
        {
            CitySnapshotDto buildings = _json.Deserialize<CitySnapshotDto>(Fixtures.Read("snapshot-city-settled-buildings.json"));
            CitySnapshotDto research = _json.Deserialize<CitySnapshotDto>(Fixtures.Read("snapshot-city-research-completed.json"));

            Assert.That(buildings.Settlement.CompletedBuildings, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(research.Settlement.CompletedResearch, Is.EqualTo(new[] { "trade_charter" }));
            Assert.That(research.City.Research.Completed, Is.EqualTo(new[] { "trade_charter" }));
            Assert.That(research.City.Research.ActiveId, Is.Null);
            Assert.That(research.Settlement.Produced["logs"], Is.GreaterThan(0L));
            Assert.That(research.Settlement.Delivered["logs"], Is.EqualTo(4L), "the trip arrived at the site in the same span");
            Assert.That(research.Settlement.Moved, Is.Empty);
            Assert.That(research.Settlement.Refunded, Is.Empty);
            Assert.That(research.Settlement.EraChanged, Is.False);
        }

        /// <summary>A snapshot without a member the contract requires is refused instead of read with a zero in its place.</summary>
        [Test]
        public void ASnapshotWithoutARequiredMember_IsRefused()
        {
            string text = Fixtures.Read("snapshot-city-start.json").Replace("\"totals\":{\"coins\":800,\"logs\":0,\"planks\":0},", string.Empty);

            Assert.That(text, Does.Not.Contain("totals"), "the fixture changed: update this test");
            Assert.That(() => _json.Deserialize<CitySnapshotDto>(text), Throws.Exception);
        }

        /// <summary>A member the DTOs don't know (a later server version adding one) is ignored.</summary>
        [Test]
        public void AnUnknownMember_IsIgnored()
        {
            string text = "{\"somethingNew\":7," + Fixtures.Read("snapshot-city-start.json").Substring(1);

            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(text);

            Assert.That(snapshot, Is.Not.Null);
        }

        /// <summary>A phase the enum lacks fails the answer: a guess would show a wrong building state.</summary>
        [Test]
        public void AnUnknownPhase_IsRefused()
        {
            string text = Fixtures.Read("snapshot-city-producing.json").Replace("\"Producing\"", "\"Dancing\"");

            Assert.That(() => _json.Deserialize<CitySnapshotDto>(text), Throws.Exception);
        }
    }
}
