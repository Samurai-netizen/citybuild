using System;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// The request bodies (<c>05</c> Commands): each holds exactly the members the contract names, in camelCase, and nothing else
    /// (the server refuses a body with any other property, <c>02</c> rule 2).
    /// </summary>
    public sealed class RequestDtoTests
    {
        /// <summary>The writer under test.</summary>
        private readonly ApiJson _json = new ApiJson();

        /// <summary>An id used by every test.</summary>
        private static readonly Guid RequestId = new Guid("6f1c0000-0000-4000-8000-000000000001");

        /// <summary>Place-building: <c>requestId</c>, <c>buildingTypeId</c>, <c>x</c>, <c>y</c>.</summary>
        [Test]
        public void PlaceBuilding_HasExactlyItsFourMembers()
        {
            string text = _json.Serialize(new PlaceBuildingRequestDto(RequestId, "sawmill", 2, 4));

            Assert.That(text, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000001\",\"buildingTypeId\":\"sawmill\",\"x\":2,\"y\":4}"));
        }

        /// <summary>Upgrade-building: <c>requestId</c> and <c>buildingId</c>.</summary>
        [Test]
        public void UpgradeBuilding_HasExactlyItsTwoMembers()
        {
            string text = _json.Serialize(new UpgradeBuildingRequestDto(RequestId, 4));

            Assert.That(text, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000001\",\"buildingId\":4}"));
        }

        /// <summary>Assign-workers: the absolute count, 0 included.</summary>
        [Test]
        public void AssignWorkers_SendsTheAbsoluteCount()
        {
            string text = _json.Serialize(new AssignWorkersRequestDto(RequestId, 4, 0));

            Assert.That(text, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000001\",\"buildingId\":4,\"workers\":0}"));
        }

        /// <summary>Start-research: <c>requestId</c> and <c>researchId</c>.</summary>
        [Test]
        public void StartResearch_HasExactlyItsTwoMembers()
        {
            string text = _json.Serialize(new StartResearchRequestDto(RequestId, "trade_charter"));

            Assert.That(text, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000001\",\"researchId\":\"trade_charter\"}"));
        }

        /// <summary>Start-trip: <c>requestId</c>, <c>sourceId</c>, <c>targetId</c>, <c>itemId</c>, <c>amount</c> (CR-0008).</summary>
        [Test]
        public void StartTrip_HasExactlyItsFiveMembers()
        {
            string text = _json.Serialize(new StartTripRequestDto(RequestId, 2, 5, "logs", "all"));

            Assert.That(text, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000001\",\"sourceId\":2,\"targetId\":5,\"itemId\":\"logs\",\"amount\":\"all\"}"));
        }

        /// <summary>Road commands (<c>place-roads</c>, <c>upgrade-roads</c>, <c>remove-roads</c>): <c>requestId</c> and <c>tiles</c> (CR-0008).</summary>
        [Test]
        public void RoadTiles_HasRequestIdAndOrderedTiles()
        {
            var tiles = new[] { new RoadTileRequestDto(1, 2), new RoadTileRequestDto(2, 2) };
            string text = _json.Serialize(new RoadTilesRequestDto(RequestId, tiles));

            Assert.That(text, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000001\",\"tiles\":[{\"x\":1,\"y\":2},{\"x\":2,\"y\":2}]}"));
        }

        /// <summary>The dev login sends the name only; its answer reads the token and a UTC expiry.</summary>
        [Test]
        public void DevLogin_SendsTheNameAndReadsTheToken()
        {
            string request = _json.Serialize(new DevLoginRequestDto("devtest"));
            DevLoginResponseDto response = _json.Deserialize<DevLoginResponseDto>("{\"accessToken\":\"abc.def.ghi\",\"expiresAtUtc\":\"2026-10-27T21:00:00+00:00\"}");

            Assert.That(request, Is.EqualTo("{\"displayName\":\"devtest\"}"));
            Assert.That(response.AccessToken, Is.EqualTo("abc.def.ghi"));
            Assert.That(response.ExpiresAtUtc, Is.EqualTo(new DateTimeOffset(2026, 10, 27, 21, 0, 0, TimeSpan.Zero)));
        }
    }
}
