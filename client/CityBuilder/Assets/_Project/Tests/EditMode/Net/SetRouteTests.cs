#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>The <c>set-route</c> command on the client (CR-0008): its body, its intent and the request <see cref="ApiClient"/> builds.</summary>
    public sealed class SetRouteTests
    {
        /// <summary>An id used by every test.</summary>
        private static readonly Guid RequestId = new Guid("6f1c0000-0000-4000-8000-000000000001");

        /// <summary>A two-stop route: take logs at 2, put them at 5.</summary>
        private static RouteStopRequestDto[] TwoStops()
        {
            return new[]
            {
                new RouteStopRequestDto(2, new[] { new RouteActionRequestDto(RouteActionKindDto.Take, "logs", "all") }),
                new RouteStopRequestDto(5, new[] { new RouteActionRequestDto(RouteActionKindDto.Put, "logs", "25%") }),
            };
        }

        /// <summary>The body has exactly the members of the contract; kinds are lower case words.</summary>
        [Test]
        public void Body_HasStationSlotAndOrderedStops()
        {
            string text = new ApiJson().Serialize(new SetRouteRequestDto(RequestId, 3, 1, TwoStops()));

            Assert.That(
                text,
                Is.EqualTo(
                    "{\"requestId\":\"6f1c0000-0000-4000-8000-000000000001\",\"stationId\":3,\"slot\":1,\"stops\":["
                    + "{\"buildingId\":2,\"actions\":[{\"kind\":\"take\",\"itemId\":\"logs\",\"amount\":\"all\"}]},"
                    + "{\"buildingId\":5,\"actions\":[{\"kind\":\"put\",\"itemId\":\"logs\",\"amount\":\"25%\"}]}]}"));
        }

        /// <summary>No stops is a valid intent (it clears the route) and sends an empty list, never null.</summary>
        [Test]
        public void ClearingRoute_SendsAnEmptyStopList()
        {
            string text = new ApiJson().Serialize(new SetRouteRequestDto(RequestId, 3, 2, Array.Empty<RouteStopRequestDto>()));

            Assert.That(text, Does.EndWith("\"slot\":2,\"stops\":[]}"));
        }

        /// <summary>Equal arguments make equal intents; any difference in station, slot, or a stop's action does not.</summary>
        [Test]
        public void Intent_EqualityCoversEveryArgument()
        {
            CommandIntent a = CommandIntent.SetRoute(3, 1, TwoStops());

            Assert.That(a, Is.EqualTo(CommandIntent.SetRoute(3, 1, TwoStops())));
            Assert.That(a.GetHashCode(), Is.EqualTo(CommandIntent.SetRoute(3, 1, TwoStops()).GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(CommandIntent.SetRoute(4, 1, TwoStops())));
            Assert.That(a, Is.Not.EqualTo(CommandIntent.SetRoute(3, 2, TwoStops())));
            RouteStopRequestDto[] changed = TwoStops();
            changed[1] = new RouteStopRequestDto(5, new[] { new RouteActionRequestDto(RouteActionKindDto.Put, "logs", "half") });
            Assert.That(a, Is.Not.EqualTo(CommandIntent.SetRoute(3, 1, changed)));
            Assert.That(a, Is.Not.EqualTo(CommandIntent.SetRoute(3, 1, Array.Empty<RouteStopRequestDto>())));
        }

        /// <summary>A null list or a null stop is refused; an empty list is allowed.</summary>
        [Test]
        public void Intent_RefusesNullButAllowsEmpty()
        {
            Assert.That(() => CommandIntent.SetRoute(3, 1, null!), Throws.ArgumentException);
            Assert.That(() => CommandIntent.SetRoute(3, 1, new RouteStopRequestDto[] { null! }), Throws.ArgumentException);
            Assert.That(CommandIntent.SetRoute(3, 1, Array.Empty<RouteStopRequestDto>()).Stops, Is.Empty);
        }

        /// <summary>The intent copies the list, so later changes by the caller do not change what is sent.</summary>
        [Test]
        public void Intent_CopiesTheStopList()
        {
            RouteStopRequestDto[] stops = TwoStops();
            CommandIntent intent = CommandIntent.SetRoute(3, 1, stops);
            stops[0] = stops[1];

            Assert.That(intent.Stops[0].BuildingId, Is.EqualTo(2));
            Assert.That(intent.ToString(), Is.EqualTo("SetRoute(station 3, slot 1, 2 stops)"));
        }

        /// <summary>The client posts to <c>set-route</c> with the token and the body.</summary>
        [Test]
        public async Task Client_PostsToSetRoute()
        {
            string snapshot = Fixtures.Read("snapshot-command-place-building.json");
            FakeTransport transport = new FakeTransport().Answer(200, snapshot);
            var client = new ApiClient("http://localhost:8080", 15, transport, allowInsecureHttp: true);
            client.SetAccessToken("tok");

            await client.SetRouteAsync(new SetRouteRequestDto(RequestId, 3, 1, TwoStops()), CancellationToken.None);

            ApiTransportRequest sent = transport.Requests[0];
            Assert.That(sent.Method, Is.EqualTo("POST"));
            Assert.That(sent.Url, Is.EqualTo("http://localhost:8080/api/v1/commands/set-route"));
            Assert.That(sent.AccessToken, Is.EqualTo("tok"));
            Assert.That(sent.JsonBody, Does.Contain("\"stationId\":3"));
        }
    }
}
