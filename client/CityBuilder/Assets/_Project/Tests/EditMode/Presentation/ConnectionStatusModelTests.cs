#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Bootstrap;
using CityBuilder.Client.Tests.EditMode.Net;
using CityBuilder.Simulation.Content;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>The sentences of the start screen (M0-38): "Connected" with the era and the coins, one message per kind of failure, and the fatal error lines.</summary>
    public sealed class ConnectionStatusModelTests
    {
        /// <summary>The real content of the fixture, built the way the client builds it.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: builds the content from the real <c>content.json</c>.</summary>
        [SetUp]
        public async Task LoadContent()
        {
            var transport = new FakeTransport().Answer(200, Fixtures.Read("content.json"));
            var api = new ApiClient("http://localhost:8080", 15, transport);
            _content = (await new ContentService(api).LoadAsync(CancellationToken.None)).Content!;
        }

        /// <summary>The first text is "Connecting...", not fatal.</summary>
        [Test]
        public void Connecting_SaysConnecting_AndIsNotFatal()
        {
            ConnectionStatusModel model = ConnectionStatusModel.Connecting();

            Assert.That(model.Headline, Is.EqualTo("Connecting..."));
            Assert.That(model.IsFatal, Is.False);
            Assert.That(model.Details, Is.Empty);
        }

        /// <summary>A connected start names the era and the coins from the content's names and the snapshot's inventory.</summary>
        [Test]
        public void Connected_NamesTheEraAndTheCoins()
        {
            StartupOutcome outcome = StartupOutcome.Connected(_content, 1, City(Fixtures.Read("snapshot-city-start.json")));

            ConnectionStatusModel model = ConnectionStatusModel.From(outcome);

            Assert.That(model.Headline, Is.EqualTo("Connected - Early Middle Ages - Coins: 700"));
            Assert.That(model.IsFatal, Is.False);
        }

        /// <summary>An era the content does not name shows its number, and an inventory without coins shows zero: the screen never fails on odd data.</summary>
        [Test]
        public void Connected_WithAnUnknownEraAndNoCoins_FallsBack()
        {
            string odd = Fixtures.Read("snapshot-city-start.json").Replace("\"era\":1,", "\"era\":9,").Replace("\"coins\":700,", string.Empty);

            ConnectionStatusModel model = ConnectionStatusModel.From(StartupOutcome.Connected(_content, 1, City(odd)));

            Assert.That(model.Headline, Is.EqualTo("Connected - Era 9 - Coins: 0"));
        }

        /// <summary>Each kind of failed request has a sentence of its own, and every one ends with the code for a bug report.</summary>
        [TestCase(ApiErrorCodes.ClientNetworkError, "Not connected: Cannot reach the server (CLIENT_NETWORK_ERROR)")]
        [TestCase(ApiErrorCodes.ClientTimeout, "Not connected: The server did not answer in time (CLIENT_TIMEOUT)")]
        [TestCase(ApiErrorCodes.ClientInvalidResponse, "Not connected: The server's answer was not understood (CLIENT_INVALID_RESPONSE)")]
        [TestCase(ApiErrorCodes.NotFound, "Not connected: The server has no dev login (it is not a Development server) (NOT_FOUND)")]
        [TestCase(ApiErrorCodes.InvalidArgument, "Not connected: The server refused the dev player name (INVALID_ARGUMENT)")]
        [TestCase(ApiErrorCodes.RateLimited, "Not connected: Too many requests, try again in a moment (RATE_LIMITED)")]
        [TestCase(ApiErrorCodes.InternalError, "Not connected: The server could not be used (INTERNAL_ERROR)")]
        public void Failed_HasASentenceAndTheCode(string code, string expected)
        {
            ConnectionStatusModel model = ConnectionStatusModel.From(StartupOutcome.Failed(new ApiError(code, 0, "detail", null, null, null)));

            Assert.That(model.Headline, Is.EqualTo(expected));
            Assert.That(model.IsFatal, Is.False);
        }

        /// <summary>Invalid content is fatal and lists the errors with their paths.</summary>
        [Test]
        public async Task InvalidContent_IsFatal_AndListsTheErrorsWithTheirPaths()
        {
            IReadOnlyList<ContentError> errors = await ErrorsOf(Fixtures.Read("content.json").Replace("\"gridWidth\":8", "\"gridWidth\":0"));

            ConnectionStatusModel model = ConnectionStatusModel.From(StartupOutcome.InvalidContent(errors));

            Assert.That(model.IsFatal, Is.True);
            Assert.That(model.Headline, Is.EqualTo("The game content from the server is invalid"));
            Assert.That(model.Details, Has.Count.EqualTo(1));
            Assert.That(model.Details[0], Does.StartWith("rules.gridWidth: "));
        }

        /// <summary>More than eight errors are cut to eight lines and a count of the rest.</summary>
        [Test]
        public async Task InvalidContent_WithManyErrors_ListsEightAndCountsTheRest()
        {
            // A negative cost or build time is a rule error per building and research node, so these two edits give well over eight.
            string broken = Fixtures.Read("content.json").Replace("\"cost\":{\"coins\":", "\"cost\":{\"coins\":-").Replace("\"buildSeconds\":", "\"buildSeconds\":-");
            IReadOnlyList<ContentError> errors = await ErrorsOf(broken);
            Assert.That(errors.Count, Is.GreaterThan(8), "the fixture no longer has enough values to break");

            ConnectionStatusModel model = ConnectionStatusModel.From(StartupOutcome.InvalidContent(errors));

            Assert.That(model.Details, Has.Count.EqualTo(9));
            Assert.That(model.Details.Last(), Is.EqualTo("... and " + (errors.Count - 8) + " more"));
        }

        /// <summary>A settings problem is fatal and says what is wrong.</summary>
        [Test]
        public void BadSettings_IsFatal_AndSaysWhy()
        {
            ConnectionStatusModel model = ConnectionStatusModel.BadSettings("the URL is not absolute");

            Assert.That(model.IsFatal, Is.True);
            Assert.That(model.Details.Single(), Is.EqualTo("The API client settings are wrong: the URL is not absolute"));
        }

        /// <summary>Reads a snapshot fixture.</summary>
        private static CitySnapshotDto City(string json)
        {
            return new ApiJson().Deserialize<CitySnapshotDto>(json)!;
        }

        /// <summary>The factory's errors for a broken content text, through the same service the client uses.</summary>
        private static async Task<IReadOnlyList<ContentError>> ErrorsOf(string contentJson)
        {
            var api = new ApiClient("http://localhost:8080", 15, new FakeTransport().Answer(200, contentJson));
            ContentLoadResult result = await new ContentService(api).LoadAsync(CancellationToken.None);
            return result.ContentErrors;
        }
    }
}
