#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>The <c>reenergize-couriers</c> command on the client (CR-0008): intent only, so its body is the request id and nothing else.</summary>
    public sealed class ReenergizeCouriersTests
    {
        /// <summary>An id used by every test.</summary>
        private static readonly Guid RequestId = new Guid("6f1c0000-0000-4000-8000-000000000002");

        /// <summary>The body has the request id and no other member (the server answers 400 to any other).</summary>
        [Test]
        public void Body_IsTheRequestIdOnly()
        {
            string text = new ApiJson().Serialize(new ReenergizeCouriersRequestDto(RequestId));

            Assert.That(text, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000002\"}"));
        }

        /// <summary>Two intents are equal, differ from the other commands, and print as a call.</summary>
        [Test]
        public void Intent_IsEqualToItselfAndNamesItself()
        {
            CommandIntent intent = CommandIntent.ReenergizeCouriers();

            Assert.That(intent, Is.EqualTo(CommandIntent.ReenergizeCouriers()));
            Assert.That(intent.GetHashCode(), Is.EqualTo(CommandIntent.ReenergizeCouriers().GetHashCode()));
            Assert.That(intent, Is.Not.EqualTo(CommandIntent.UpgradeBuilding(0)));
            Assert.That(intent.Kind, Is.EqualTo(CommandKind.ReenergizeCouriers));
            Assert.That(intent.ToString(), Is.EqualTo("ReenergizeCouriers()"));
        }

        /// <summary>The client posts to <c>reenergize-couriers</c> with the token and the body.</summary>
        [Test]
        public async Task Client_PostsToReenergizeCouriers()
        {
            string snapshot = Fixtures.Read("snapshot-command-place-building.json");
            FakeTransport transport = new FakeTransport().Answer(200, snapshot);
            var client = new ApiClient("http://localhost:8080", 15, transport, allowInsecureHttp: true);
            client.SetAccessToken("tok");

            await client.ReenergizeCouriersAsync(new ReenergizeCouriersRequestDto(RequestId), CancellationToken.None);

            ApiTransportRequest sent = transport.Requests[0];
            Assert.That(sent.Method, Is.EqualTo("POST"));
            Assert.That(sent.Url, Is.EqualTo("http://localhost:8080/api/v1/commands/reenergize-couriers"));
            Assert.That(sent.AccessToken, Is.EqualTo("tok"));
        }
    }
}
