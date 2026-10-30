using System;
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

        /// <summary>A starter city: coins and logs in stock, no buildings, no research, <c>requestId</c> null because <c>GET /city</c> is no command.</summary>
        [Test]
        public void CityStart_HasTheStarterCityAndNoRequestId()
        {
            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(Fixtures.Read("snapshot-city-start.json"));

            Assert.That(snapshot.RequestId, Is.Null);
            Assert.That(snapshot.ContentVersion, Is.EqualTo("mvp-1"));
            Assert.That(snapshot.City.Era, Is.EqualTo(1));
            Assert.That(snapshot.City.Inventory["coins"], Is.EqualTo(700L));
            Assert.That(snapshot.City.StorageCaps["logs"], Is.EqualTo(500L));
            Assert.That(snapshot.City.Buildings, Is.Empty);
            Assert.That(snapshot.City.Research.ActiveId, Is.Null);
            Assert.That(snapshot.City.Research.StartedAt, Is.Null);
            Assert.That(snapshot.City.Research.Completed, Is.Empty);
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

        /// <summary>A city with producers: phases read as enums, a running cycle has an end time and no stall reason, a house says <c>NoRecipe</c>.</summary>
        [Test]
        public void CityProducing_ReadsPhasesAndStallReasons()
        {
            CitySnapshotDto snapshot = _json.Deserialize<CitySnapshotDto>(Fixtures.Read("snapshot-city-producing.json"));

            SnapshotBuildingDto house = snapshot.City.Buildings[0];
            SnapshotBuildingDto sawmill = snapshot.City.Buildings[2];
            Assert.That(house.Phase, Is.EqualTo(BuildingPhaseDto.Idle));
            Assert.That(house.StallReason, Is.EqualTo("NoRecipe"));
            Assert.That(house.PhaseEndsAt, Is.Null);
            Assert.That(sawmill.TypeId, Is.EqualTo("sawmill"));
            Assert.That(sawmill.Phase, Is.EqualTo(BuildingPhaseDto.Producing));
            Assert.That(sawmill.StallReason, Is.Null);
            Assert.That(sawmill.PhaseEndsAt, Is.Not.Null);
            Assert.That(snapshot.City.Workers.Free, Is.EqualTo(snapshot.City.Workers.Provided - snapshot.City.Workers.Assigned));
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
            Assert.That(research.Settlement.Produced["planks"], Is.GreaterThan(0L));
            Assert.That(research.Settlement.EraChanged, Is.False);
        }

        /// <summary>A snapshot without a member the contract requires is refused instead of read with a zero in its place.</summary>
        [Test]
        public void ASnapshotWithoutARequiredMember_IsRefused()
        {
            string text = Fixtures.Read("snapshot-city-start.json").Replace("\"storageCaps\":{\"logs\":500,\"planks\":500},", string.Empty);

            Assert.That(text, Does.Not.Contain("storageCaps"), "the fixture changed: update this test");
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
