#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Tests.EditMode.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// <see cref="ConnectionMonitor"/> (M0-41) over the real <see cref="ApiClient"/> and a fake wire: the state each answer leads to, the one
    /// new login for a refused token, what reaches the city store, and that a poll starts only when the scheduler (on a fake clock) says so.
    /// </summary>
    public sealed class ConnectionMonitorTests
    {
        /// <summary>The login answer of a Development server.</summary>
        private const string LoginAnswer = "{\"accessToken\":\"fresh-token\",\"expiresAtUtc\":\"2026-10-27T21:00:00+00:00\"}";

        /// <summary>A server fault in the shape of <c>05</c>.</summary>
        private const string InternalError = "{\"type\":\"urn:citybuilder:error:internal-error\",\"title\":\"Internal error\",\"status\":500,\"detail\":\"x\",\"code\":\"INTERNAL_ERROR\"}";

        /// <summary>The fake wire of the current test.</summary>
        private FakeTransport _transport = null!;

        /// <summary>Where the auth service keeps the token.</summary>
        private MemoryAccessTokenStore _tokens = null!;

        /// <summary>The city store the monitor fills.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The fake real-time clock of the scheduler, in seconds.</summary>
        private double _now;

        /// <summary>What the device says about the network.</summary>
        private bool _reachable;

        /// <summary>The scheduler under the monitor.</summary>
        private PollScheduler _scheduler = null!;

        /// <summary>The monitor under test, for the dev player <c>dev1</c> and the content <c>mvp-1</c>.</summary>
        private ConnectionMonitor _monitor = null!;

        /// <summary>How many times the monitor raised <see cref="ConnectionMonitor.StateChanged"/>.</summary>
        private int _changes;

        /// <summary>Before each test: a monitor that has just connected with a kept token, at fake second 1000, with a network.</summary>
        [SetUp]
        public void CreateMonitor()
        {
            _transport = new FakeTransport();
            _tokens = new MemoryAccessTokenStore { Kept = new StoredLogin("dev1", "kept-token") };
            var api = new ApiClient("http://localhost:8080", 15, _transport);
            var auth = new AuthService(api, _tokens);
            Assert.That(auth.TryRestore("dev1"), Is.True);
            _store = new ClientCityStore(() => 0);
            _now = 1000;
            _reachable = true;
            _scheduler = new PollScheduler(() => _now, PollScheduler.DefaultIntervalSeconds);
            _monitor = new ConnectionMonitor(api, auth, _store, "mvp-1", "dev1", _scheduler, () => _reachable);
            _changes = 0;
            _monitor.StateChanged += () => _changes++;
        }

        /// <summary><c>GameRoot</c> calls <see cref="ConnectionMonitor.TickAsync"/> every frame: with no poll due it allocates nothing (M0-48).</summary>
        [Test]
        public void Tick_WithNoPollDue_AllocatesNothing()
        {
            _monitor.TickAsync(CancellationToken.None);

            Assert.That(() => { _monitor.TickAsync(CancellationToken.None); }, Is.Not.AllocatingGCMemory());
        }

        /// <summary>A monitor made after a successful start is connected and has sent nothing.</summary>
        [Test]
        public void New_IsConnected_AndSendsNothing()
        {
            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(_monitor.LastError, Is.Null);
            Assert.That(_transport.Requests, Is.Empty);
        }

        /// <summary>A usable snapshot goes to the store through the one <c>GET /city</c>, with the token.</summary>
        [Test]
        public async Task Poll_WithASnapshot_AppliesItAndStaysConnected()
        {
            _transport.Answer(200, Fixtures.Read("snapshot-city-producing.json"));

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(_store.Snapshot!.StateVersion, Is.EqualTo(25L));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_transport.Requests[0].Method + " " + _transport.Requests[0].Url, Is.EqualTo("GET http://localhost:8080/api/v1/city"));
            Assert.That(_transport.Requests[0].AccessToken, Is.EqualTo("kept-token"));
            Assert.That(_changes, Is.EqualTo(0));
        }

        /// <summary>An older snapshot (a poll that overtook a command) is a usable answer: connected, and the store keeps the newer one.</summary>
        [Test]
        public async Task Poll_WithAnOlderSnapshot_StaysConnected_AndTheStoreKeepsTheNewer()
        {
            _transport.Answer(200, Fixtures.Read("snapshot-city-producing.json")).Answer(200, Fixtures.Read("snapshot-city-start.json"));

            await _monitor.PollAsync(CancellationToken.None);
            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(_store.Snapshot!.StateVersion, Is.EqualTo(25L));
        }

        /// <summary>No connection and no answer in time are both offline, with the client's code; the next usable answer is connected again.</summary>
        [Test]
        public async Task Poll_WithoutAnAnswer_IsOffline_ThenConnectedAgain()
        {
            _transport.Fail("no route").TimeOut().Answer(200, Fixtures.Read("snapshot-city-start.json"));

            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Offline));
            Assert.That(_monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));

            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Offline));
            Assert.That(_monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.ClientTimeout));

            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(_monitor.LastError, Is.Null);
            Assert.That(_store.Snapshot, Is.Not.Null);
        }

        /// <summary>A 5xx, also a proxy's page in place of the answer, is a server error and leaves the store alone.</summary>
        [Test]
        public async Task Poll_With5xx_IsAServerError()
        {
            _transport.Answer(500, InternalError).Answer(502, "<html>Bad gateway</html>");

            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.ServerError));
            Assert.That(_monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.InternalError));

            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.ServerError));
            Assert.That(_monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(_store.Snapshot, Is.Null);
        }

        /// <summary>An unexpected refusal of <c>GET /city</c> (a 404 here) is a server error, not a login problem.</summary>
        [Test]
        public async Task Poll_WithAnUnexpected4xx_IsAServerError()
        {
            _transport.Answer(404, Fixtures.Read("error-404-NOT_FOUND.json"));

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.ServerError));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
        }

        /// <summary>A 200 whose body is not the contract's is a client that does not fit the server.</summary>
        [Test]
        public async Task Poll_WithAnAnswerThatIsNotTheContracts_IsIncompatible()
        {
            _transport.Answer(200, "{\"hello\":\"world\"}");

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.IncompatibleVersion));
            Assert.That(_monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(_store.Snapshot, Is.Null);
        }

        /// <summary>A snapshot of other content is incompatible, has no error, and is not given to the store.</summary>
        [Test]
        public async Task Poll_WithOtherContent_IsIncompatible_AndTheStoreIsUntouched()
        {
            string other = Fixtures.Read("snapshot-city-producing.json").Replace("\"contentVersion\":\"mvp-1\"", "\"contentVersion\":\"mvp-2\"");
            Assert.That(other, Does.Contain("mvp-2"));
            _transport.Answer(200, other);

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.IncompatibleVersion));
            Assert.That(_monitor.LastError, Is.Null);
            Assert.That(_store.Snapshot, Is.Null);
            Assert.That(_changes, Is.EqualTo(1));
        }

        /// <summary>A refused token: forget it, log in once, repeat the request with the new token; the city arrives and the state is connected.</summary>
        [Test]
        public async Task Poll_WithARefusedToken_LogsInOnce_AndRepeatsTheRequest()
        {
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"))
                .Answer(200, LoginAnswer)
                .Answer(200, Fixtures.Read("snapshot-city-producing.json"));

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(_store.Snapshot!.StateVersion, Is.EqualTo(25L));
            Assert.That(_transport.Requests, Has.Count.EqualTo(3));
            Assert.That(_transport.Requests[0].Url, Does.EndWith("/city"));
            Assert.That(_transport.Requests[0].AccessToken, Is.EqualTo("kept-token"));
            Assert.That(_transport.Requests[1].Url, Does.EndWith("/auth/dev-login"));
            Assert.That(_transport.Requests[1].JsonBody, Does.Contain("dev1"));
            Assert.That(_transport.Requests[2].Url, Does.EndWith("/city"));
            Assert.That(_transport.Requests[2].AccessToken, Is.EqualTo("fresh-token"));
            Assert.That(_tokens.Kept!.AccessToken, Is.EqualTo("fresh-token"));
            Assert.That(_changes, Is.EqualTo(0));
        }

        /// <summary>
        /// After a new login the server's answer is the city, even with a lower version: the player may be new (a reset development database,
        /// where the versions start over). The store would refuse it as old, and the old city would stay on screen as <c>Connected</c> (CR-0004).
        /// </summary>
        [Test]
        public async Task Poll_AfterANewLogin_TakesTheCityEvenWithALowerVersion()
        {
            _store.Apply(CityTestData.Snapshot("snapshot-city-producing.json"));
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"))
                .Answer(200, LoginAnswer)
                .Answer(200, Fixtures.Read("snapshot-city-start.json"));

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(_store.Snapshot!.StateVersion, Is.EqualTo(2L));
        }

        /// <summary>Without a new login a lower version is still an old answer: the store keeps what it has.</summary>
        [Test]
        public async Task Poll_WithoutANewLogin_KeepsTheNewerCity()
        {
            _store.Apply(CityTestData.Snapshot("snapshot-city-producing.json"));
            _transport.Answer(200, Fixtures.Read("snapshot-city-start.json"));

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_store.Snapshot!.StateVersion, Is.EqualTo(25L));
        }

        /// <summary>
        /// Something unexpected in a poll is logged, because the frame loop discards the task: without the log it would leave no trace. The
        /// scheduler is free again, so the next poll runs (CR-0004).
        /// </summary>
        [Test]
        public async Task Tick_UnexpectedException_IsLogged_AndTheNextPollRuns()
        {
            // The fake wire throws InvalidOperationException for a request the test queued no answer for.
            _scheduler.RequestPoll();
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            await _monitor.TickAsync(CancellationToken.None);

            Assert.That(_scheduler.IsPolling, Is.False);
            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
        }

        /// <summary>The new token is refused too: unauthorized, after exactly one login and one repeat; a later poll may try again.</summary>
        [Test]
        public async Task Poll_WithASecondRefusal_IsUnauthorized_AfterOneLogin()
        {
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"))
                .Answer(200, LoginAnswer)
                .Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"));

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Unauthorized));
            Assert.That(_monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.AuthenticationRequired));
            Assert.That(_transport.Requests, Has.Count.EqualTo(3));
            Assert.That(_changes, Is.EqualTo(1));
        }

        /// <summary>A login the server refuses is unauthorized and no repeat is sent.</summary>
        [Test]
        public async Task Poll_WhenTheLoginIsRefused_IsUnauthorized_WithoutARepeat()
        {
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json")).Answer(404, Fixtures.Read("error-404-NOT_FOUND.json"));

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Unauthorized));
            Assert.That(_monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.NotFound));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
        }

        /// <summary>A login that cannot reach the server is offline, not unauthorized, and no repeat is sent.</summary>
        [Test]
        public async Task Poll_WhenTheLoginGetsNoAnswer_IsOffline()
        {
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json")).Fail("no route");

            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Offline));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
        }

        /// <summary>A rate limit on a poll says nothing about the connection: the state and the error stay as they were, and nothing is raised.</summary>
        [Test]
        public async Task Poll_WhenRateLimited_ChangesNothing()
        {
            _transport.Fail("no route").Answer(429, Fixtures.Read("error-429-RATE_LIMITED.json"), "30");

            await _monitor.PollAsync(CancellationToken.None);
            int changesBefore = _changes;
            await _monitor.PollAsync(CancellationToken.None);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Offline));
            Assert.That(_monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
            Assert.That(_changes, Is.EqualTo(changesBefore));
        }

        /// <summary>The event fires when the state changes or the error code does within a state, and not when the same thing happens again.</summary>
        [Test]
        public async Task StateChanged_IsRaisedOnlyForARealChange()
        {
            _transport.Fail("a").Fail("b").TimeOut().Answer(200, Fixtures.Read("snapshot-city-start.json")).Answer(200, Fixtures.Read("snapshot-city-start.json"));

            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_changes, Is.EqualTo(1));
            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_changes, Is.EqualTo(1));
            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_changes, Is.EqualTo(2));
            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_changes, Is.EqualTo(3));
            await _monitor.PollAsync(CancellationToken.None);
            Assert.That(_changes, Is.EqualTo(3));
        }

        /// <summary>A cancelled poll throws the cancellation and leaves the state alone.</summary>
        [Test]
        public void Poll_WhenCancelled_Throws_AndKeepsTheState()
        {
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();

                Assert.CatchAsync<OperationCanceledException>(async () => await _monitor.PollAsync(cancelled.Token));
            }

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
        }

        /// <summary>Which state each failure means, for a poll and for the new login.</summary>
        [TestCase(ApiErrorCodes.ClientNetworkError, 0, false, ConnectionState.Offline)]
        [TestCase(ApiErrorCodes.ClientTimeout, 0, false, ConnectionState.Offline)]
        [TestCase(ApiErrorCodes.ClientNetworkError, 0, true, ConnectionState.Offline)]
        [TestCase(ApiErrorCodes.InternalError, 500, false, ConnectionState.ServerError)]
        [TestCase(ApiErrorCodes.InternalError, 500, true, ConnectionState.ServerError)]
        [TestCase(ApiErrorCodes.ClientInvalidResponse, 502, false, ConnectionState.ServerError)]
        [TestCase(ApiErrorCodes.ClientInvalidResponse, 200, false, ConnectionState.IncompatibleVersion)]
        [TestCase(ApiErrorCodes.AuthenticationRequired, 401, false, ConnectionState.Unauthorized)]
        [TestCase(ApiErrorCodes.NotFound, 404, false, ConnectionState.ServerError)]
        [TestCase(ApiErrorCodes.NotFound, 404, true, ConnectionState.Unauthorized)]
        [TestCase(ApiErrorCodes.InvalidArgument, 400, true, ConnectionState.Unauthorized)]
        [TestCase(ApiErrorCodes.RateLimited, 429, true, ConnectionState.Unauthorized)]
        public void StateFor_MapsEachFailure(string code, int status, bool duringLogin, ConnectionState expected)
        {
            var error = new ApiError(code, status, "x", null, null, null);

            Assert.That(ConnectionMonitor.StateFor(error, duringLogin), Is.EqualTo(expected));
        }

        /// <summary>A rate limit on a poll maps to no state at all.</summary>
        [Test]
        public void StateFor_ARateLimitedPoll_IsNull()
        {
            var error = new ApiError(ApiErrorCodes.RateLimited, 429, "x", null, null, 30);

            Assert.That(ConnectionMonitor.StateFor(error, false), Is.Null);
        }

        /// <summary>Nothing is due right after the start: a tick sends nothing.</summary>
        [Test]
        public async Task Tick_RightAfterTheStart_SendsNothing()
        {
            await _monitor.TickAsync(CancellationToken.None);

            Assert.That(_transport.Requests, Is.Empty);
        }

        /// <summary>After 120 foreground seconds one tick polls, and the next 120 seconds start from its end.</summary>
        [Test]
        public async Task Tick_EveryInterval_PollsOnce()
        {
            _transport.Answer(200, Fixtures.Read("snapshot-city-start.json")).Answer(200, Fixtures.Read("snapshot-city-start.json"));

            _now += 119;
            await _monitor.TickAsync(CancellationToken.None);
            Assert.That(_transport.Requests, Is.Empty);

            _now += 1;
            await _monitor.TickAsync(CancellationToken.None);
            await _monitor.TickAsync(CancellationToken.None);
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));

            _now += 120;
            await _monitor.TickAsync(CancellationToken.None);
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
        }

        /// <summary>In the background nothing is polled however long it lasts; on return one poll goes out at once, only one.</summary>
        [Test]
        public async Task Pause_StopsPolling_AndResumePollsAtOnce()
        {
            _transport.Answer(200, Fixtures.Read("snapshot-city-start.json"));

            _monitor.SetPaused(true);
            _now += 1000;
            await _monitor.TickAsync(CancellationToken.None);
            Assert.That(_transport.Requests, Is.Empty);

            _monitor.SetPaused(false);
            await _monitor.TickAsync(CancellationToken.None);
            await _monitor.TickAsync(CancellationToken.None);
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
        }

        /// <summary>When the network comes back a poll goes out at once; while it stays away, or stays there, none does.</summary>
        [Test]
        public async Task Tick_WhenTheNetworkComesBack_PollsAtOnce()
        {
            _transport.Answer(200, Fixtures.Read("snapshot-city-start.json"));

            _reachable = false;
            await _monitor.TickAsync(CancellationToken.None);
            await _monitor.TickAsync(CancellationToken.None);
            Assert.That(_transport.Requests, Is.Empty);

            _reachable = true;
            await _monitor.TickAsync(CancellationToken.None);
            await _monitor.TickAsync(CancellationToken.None);
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
        }

        /// <summary>An explicit request polls at once (the way a refused command asks for the login to be redone).</summary>
        [Test]
        public async Task RequestPoll_PollsAtOnce()
        {
            _transport.Answer(200, Fixtures.Read("snapshot-city-start.json"));

            _monitor.RequestPoll();
            await _monitor.TickAsync(CancellationToken.None);

            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
        }

        /// <summary>While a poll is open a second tick sends nothing; a cancel ends the open one quietly and the scheduler is free again.</summary>
        [Test]
        public async Task Tick_WhilePolling_SendsNoSecondRequest_AndACancelEndsItQuietly()
        {
            _transport.NeverAnswer();
            _now += 120;
            using (var cancel = new CancellationTokenSource())
            {
                Task first = _monitor.TickAsync(cancel.Token);
                Assert.That(_scheduler.IsPolling, Is.True);

                await _monitor.TickAsync(cancel.Token);
                Assert.That(_transport.Requests, Has.Count.EqualTo(1));

                cancel.Cancel();
                await first;
            }

            Assert.That(_scheduler.IsPolling, Is.False);
            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Connected));
        }

        /// <summary>The constructor refuses a missing argument.</summary>
        [Test]
        public void Constructor_RefusesNulls()
        {
            var api = new ApiClient("http://localhost:8080", 15, _transport);
            var auth = new AuthService(api, _tokens);

            Assert.Throws<ArgumentNullException>(() => new ConnectionMonitor(null!, auth, _store, "mvp-1", "dev1", _scheduler, () => true));
            Assert.Throws<ArgumentNullException>(() => new ConnectionMonitor(api, null!, _store, "mvp-1", "dev1", _scheduler, () => true));
            Assert.Throws<ArgumentNullException>(() => new ConnectionMonitor(api, auth, null!, "mvp-1", "dev1", _scheduler, () => true));
            Assert.Throws<ArgumentNullException>(() => new ConnectionMonitor(api, auth, _store, null!, "dev1", _scheduler, () => true));
            Assert.Throws<ArgumentNullException>(() => new ConnectionMonitor(api, auth, _store, "mvp-1", null!, _scheduler, () => true));
            Assert.Throws<ArgumentNullException>(() => new ConnectionMonitor(api, auth, _store, "mvp-1", "dev1", null!, () => true));
            Assert.Throws<ArgumentNullException>(() => new ConnectionMonitor(api, auth, _store, "mvp-1", "dev1", _scheduler, null!));
        }
    }
}
