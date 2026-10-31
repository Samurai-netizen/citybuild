#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Bootstrap;
using CityBuilder.Client.Tests.EditMode.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// <see cref="StartupFlow"/> (M0-38): login, then content, then the city, over a fake wire and an in-memory token store. Every test names the
    /// requests the flow made, because the order and the token on each request are the behaviour.
    /// </summary>
    public sealed class StartupFlowTests
    {
        /// <summary>The login answer of a Development server.</summary>
        private const string LoginAnswer = "{\"accessToken\":\"fresh-token\",\"expiresAtUtc\":\"2026-10-27T21:00:00+00:00\"}";

        /// <summary>The fake wire of the current test.</summary>
        private FakeTransport _transport = null!;

        /// <summary>The store the flow's auth service writes to.</summary>
        private MemoryAccessTokenStore _store = null!;

        /// <summary>The city store the flow fills.</summary>
        private ClientCityStore _cityStore = null!;

        /// <summary>The flow under test, for the dev player <c>dev1</c>.</summary>
        private StartupFlow _flow = null!;

        /// <summary>Before each test: a flow over a fake wire with an empty store.</summary>
        [SetUp]
        public void CreateFlow()
        {
            _transport = new FakeTransport();
            _store = new MemoryAccessTokenStore();
            var api = new ApiClient("http://localhost:8080", 15, _transport);
            _cityStore = new ClientCityStore(() => 0);
            _flow = new StartupFlow(new AuthService(api, _store), new ContentService(api), api, "dev1", _cityStore);
        }

        /// <summary>A first start logs in, loads the content and fetches the city, in that order, each with the token it needs.</summary>
        [Test]
        public async Task Run_OnAFirstStart_LogsInThenLoadsContentThenTheCity()
        {
            _transport.Answer(200, LoginAnswer).Answer(200, Fixtures.Read("content.json")).Answer(200, Fixtures.Read("snapshot-city-start.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Connected));
            Assert.That(outcome.Content!.ContentVersion, Is.EqualTo("mvp-1"));
            Assert.That(outcome.City!.City.Inventory["coins"], Is.EqualTo(700L));
            Assert.That(_transport.Requests, Has.Count.EqualTo(3));
            Assert.That(_transport.Requests[0].Method + " " + _transport.Requests[0].Url, Is.EqualTo("POST http://localhost:8080/api/v1/auth/dev-login"));
            Assert.That(_transport.Requests[0].AccessToken, Is.Null);
            Assert.That(_transport.Requests[1].Method + " " + _transport.Requests[1].Url, Is.EqualTo("GET http://localhost:8080/api/v1/content"));
            Assert.That(_transport.Requests[1].AccessToken, Is.EqualTo("fresh-token"));
            Assert.That(_transport.Requests[2].Method + " " + _transport.Requests[2].Url, Is.EqualTo("GET http://localhost:8080/api/v1/city"));
            Assert.That(_transport.Requests[2].AccessToken, Is.EqualTo("fresh-token"));
            Assert.That(_store.Kept!.AccessToken, Is.EqualTo("fresh-token"));
        }

        /// <summary>A kept token for the same player is used without a login.</summary>
        [Test]
        public async Task Run_WithAKeptToken_SkipsTheLogin()
        {
            _store.Kept = new StoredLogin("dev1", "kept-token");
            _transport.Answer(200, Fixtures.Read("content.json")).Answer(200, Fixtures.Read("snapshot-city-start.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Connected));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
            Assert.That(_transport.Requests[0].Url, Does.EndWith("/content"));
            Assert.That(_transport.Requests[0].AccessToken, Is.EqualTo("kept-token"));
            Assert.That(_transport.Requests[1].AccessToken, Is.EqualTo("kept-token"));
        }

        /// <summary>A kept token for another player is not used: the flow logs in as the configured one.</summary>
        [Test]
        public async Task Run_WithAKeptTokenOfAnotherPlayer_LogsIn()
        {
            _store.Kept = new StoredLogin("someone-else", "kept-token");
            _transport.Answer(200, LoginAnswer).Answer(200, Fixtures.Read("content.json")).Answer(200, Fixtures.Read("snapshot-city-start.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Connected));
            Assert.That(_transport.Requests[0].Url, Does.EndWith("/auth/dev-login"));
            Assert.That(_store.Kept!.DisplayName, Is.EqualTo("dev1"));
        }

        /// <summary>A kept token the server refuses is dropped, the flow logs in once, repeats the request with the new token, and connects.</summary>
        [Test]
        public async Task Run_WhenTheKeptTokenIsRefused_LogsInOnceAndConnects()
        {
            _store.Kept = new StoredLogin("dev1", "old-token");
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"))
                .Answer(200, LoginAnswer)
                .Answer(200, Fixtures.Read("content.json"))
                .Answer(200, Fixtures.Read("snapshot-city-start.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Connected));
            Assert.That(_transport.Requests, Has.Count.EqualTo(4));
            Assert.That(_transport.Requests[0].AccessToken, Is.EqualTo("old-token"));
            Assert.That(_transport.Requests[1].Url, Does.EndWith("/auth/dev-login"));
            Assert.That(_transport.Requests[2].AccessToken, Is.EqualTo("fresh-token"));
            Assert.That(_transport.Requests[3].AccessToken, Is.EqualTo("fresh-token"));
            Assert.That(_store.Kept!.AccessToken, Is.EqualTo("fresh-token"));
        }

        /// <summary>
        /// A kept token whose player is gone (a reset development database) passes <c>GET /content</c>, which reads no city, and is refused by
        /// <c>GET /city</c>: the same one login and repeat, and the start connects with the new city (CR-0004).
        /// </summary>
        [Test]
        public async Task Run_WhenOnlyTheCityRefusesTheKeptToken_LogsInOnceAndConnects()
        {
            _store.Kept = new StoredLogin("dev1", "old-token");
            _transport.Answer(200, Fixtures.Read("content.json"))
                .Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"))
                .Answer(200, LoginAnswer)
                .Answer(200, Fixtures.Read("snapshot-city-start.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Connected));
            Assert.That(_transport.Requests, Has.Count.EqualTo(4));
            Assert.That(_transport.Requests[1].AccessToken, Is.EqualTo("old-token"));
            Assert.That(_transport.Requests[2].Url, Does.EndWith("/auth/dev-login"));
            Assert.That(_transport.Requests[3].Url, Does.EndWith("/city"));
            Assert.That(_transport.Requests[3].AccessToken, Is.EqualTo("fresh-token"));
            Assert.That(_store.Kept!.AccessToken, Is.EqualTo("fresh-token"));
            Assert.That(_cityStore.Snapshot, Is.Not.Null);
        }

        /// <summary>A kept token refused by the content and the new one refused by the city is a failure after one login, not a second one.</summary>
        [Test]
        public async Task Run_WhenTheCityRefusesTheTokenOfTheRetry_FailsWithoutAnotherLogin()
        {
            _store.Kept = new StoredLogin("dev1", "old-token");
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"))
                .Answer(200, LoginAnswer)
                .Answer(200, Fixtures.Read("content.json"))
                .Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.AuthenticationRequired));
            Assert.That(_transport.Requests, Has.Count.EqualTo(4));
        }

        /// <summary>A refusal right after a fresh login is a failure, not a reason to log in again and again.</summary>
        [Test]
        public async Task Run_WhenAFreshTokenIsRefused_Fails_WithoutAnotherLogin()
        {
            _transport.Answer(200, LoginAnswer).Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.AuthenticationRequired));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
        }

        /// <summary>A refused re-login after a refused kept token fails with the login's error and asks for nothing more.</summary>
        [Test]
        public async Task Run_WhenTheRetryLoginFails_FailsWithTheLoginError()
        {
            _store.Kept = new StoredLogin("dev1", "old-token");
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json")).Fail("down");

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
            Assert.That(_store.Kept, Is.Null);
        }

        /// <summary>No connection at the login stops the flow there.</summary>
        [Test]
        public async Task Run_WithoutAConnection_FailsAtTheLogin()
        {
            _transport.Fail("Cannot connect to destination host");

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
            Assert.That(outcome.Content, Is.Null);
            Assert.That(outcome.City, Is.Null);
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
        }

        /// <summary>A server that is not in Development has no dev login (404): the flow fails with that code.</summary>
        [Test]
        public async Task Run_AgainstAServerWithoutDevLogin_FailsWithNotFound()
        {
            _transport.Answer(404, Fixtures.Read("error-404-NOT_FOUND.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.NotFound));
        }

        /// <summary>Invalid content ends the flow as invalid content, lists the errors, and never asks for the city.</summary>
        [Test]
        public async Task Run_WithInvalidContent_EndsAsInvalidContent_WithoutFetchingTheCity()
        {
            string broken = Fixtures.Read("content.json").Replace("\"gridWidth\":8", "\"gridWidth\":0");
            _transport.Answer(200, LoginAnswer).Answer(200, broken);

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.InvalidContent));
            Assert.That(outcome.ContentErrors, Is.Not.Empty);
            Assert.That(outcome.Content, Is.Null);
            Assert.That(outcome.City, Is.Null);
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
        }

        /// <summary>A content request that fails (a proxy's page, say) is a failed start that can be retried, not a fatal error.</summary>
        [Test]
        public async Task Run_WhenTheContentRequestFails_Fails()
        {
            _transport.Answer(200, LoginAnswer).Answer(502, "<html>Bad Gateway</html>");

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
        }

        /// <summary>A city request that fails is a failed start.</summary>
        [Test]
        public async Task Run_WhenTheCityRequestFails_Fails()
        {
            _transport.Answer(200, LoginAnswer).Answer(200, Fixtures.Read("content.json")).Fail("down");

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
            Assert.That(outcome.Content, Is.Null);
        }

        /// <summary>A connected start leaves the first snapshot in the city store, and the outcome carries the clock's speed.</summary>
        [Test]
        public async Task Run_Connected_FillsTheCityStore()
        {
            _transport.Answer(200, LoginAnswer).Answer(200, Fixtures.Read("content.json")).Answer(200, Fixtures.Read("snapshot-city-producing.json"));

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Connected));
            Assert.That(outcome.TimeScale, Is.EqualTo(1));
            Assert.That(_cityStore.Snapshot!.StateVersion, Is.EqualTo(25L));
            Assert.That(_cityStore.Snapshot.Clock, Is.EqualTo(199L));
        }

        /// <summary>A snapshot whose values form no city is a failed start, not a connected one, and the store stays empty.</summary>
        [Test]
        public async Task Run_ASnapshotThatIsNoCity_Fails()
        {
            string broken = Fixtures.Read("snapshot-city-producing.json").Replace("\"clock\":199", "\"clock\":-1");
            _transport.Answer(200, LoginAnswer).Answer(200, Fixtures.Read("content.json")).Answer(200, broken);

            StartupOutcome outcome = await _flow.RunAsync(CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(StartupResultKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(_cityStore.Snapshot, Is.Null);
        }

        /// <summary>A cancelled start throws, whichever request was open.</summary>
        [Test]
        public void Run_Cancelled_Throws()
        {
            _transport.NeverAnswer();
            using (var cancel = new CancellationTokenSource())
            {
                Task<StartupOutcome> run = _flow.RunAsync(cancel.Token);
                cancel.Cancel();

                Assert.That(async () => await run, Throws.InstanceOf<OperationCanceledException>());
            }
        }
    }
}
