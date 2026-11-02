#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// <see cref="ContentSync"/> and the two classes that use it (M0-58): a snapshot of other content is applied only after <c>GET /content</c> was
    /// fetched, validated and found to be that version, in the order fetch, replacing, apply, replaced; content that cannot be had applies nothing and is
    /// incompatible; one fetch serves answers that arrive together. Over the real <see cref="ApiClient"/> and a <see cref="FakeTransport"/>.
    /// </summary>
    public sealed class ContentSyncTests
    {
        /// <summary>The content version of <c>content.json</c> and of the fixture snapshots.</summary>
        private const string HeldVersion = "mvp-1";

        /// <summary>The version the server moves to in these tests.</summary>
        private const string NewVersion = "mvp-2";

        /// <summary>A 426 answer, which no fixture holds.</summary>
        private const string UnsupportedBody = "{\"type\":\"urn:citybuilder:error:unsupported-client-version\",\"title\":\"Client version unsupported\","
            + "\"status\":426,\"detail\":\"Update the game.\",\"code\":\"UNSUPPORTED_CLIENT_VERSION\"}";

        /// <summary>A 500 answer.</summary>
        private const string ServerFault = "{\"type\":\"urn:citybuilder:error:internal-error\",\"title\":\"Internal error\",\"status\":500,\"detail\":\"x\",\"code\":\"INTERNAL_ERROR\"}";

        /// <summary>
        /// Wraps a <see cref="FakeTransport"/> and calls a hook before it answers, so a test can see the world at the moment a request goes out
        /// (is the snapshot already in the store?) or hold an answer back.
        /// </summary>
        private sealed class HookedTransport : IApiTransport
        {
            /// <summary>The wire underneath.</summary>
            private readonly FakeTransport _inner;

            /// <summary>Called with each request before the wire answers; may return a task to wait for.</summary>
            private readonly Func<ApiTransportRequest, Task?> _before;

            /// <summary>Wraps <paramref name="inner"/>.</summary>
            public HookedTransport(FakeTransport inner, Func<ApiTransportRequest, Task?> before)
            {
                _inner = inner;
                _before = before;
            }

            /// <inheritdoc/>
            public async Task<ApiTransportResponse> SendAsync(ApiTransportRequest request, CancellationToken cancellationToken)
            {
                Task? wait = _before(request);
                if (wait != null)
                {
                    await wait;
                }

                return await _inner.SendAsync(request, cancellationToken);
            }
        }

        /// <summary>The wire of the test.</summary>
        private FakeTransport _wire = null!;

        /// <summary>What happened, in order, as short words.</summary>
        private List<string> _events = null!;

        /// <summary>The city store.</summary>
        private ClientCityStore _store = null!;

        /// <summary>The client over the wire (with the hook).</summary>
        private ApiClient _api = null!;

        /// <summary>The sync under test, holding <see cref="HeldVersion"/>.</summary>
        private ContentSync _sync = null!;

        /// <summary>Held back by the hook until a test lets the content request go on; null when nothing is held.</summary>
        private TaskCompletionSource<bool>? _hold;

        /// <summary>Before each test: a client with a token, an empty store and a sync that records its events and the store's changes.</summary>
        [SetUp]
        public void CreateSync()
        {
            _wire = new FakeTransport();
            _events = new List<string>();
            _hold = null;
            _api = new ApiClient("http://localhost:8080", 15, new HookedTransport(_wire, request =>
            {
                _events.Add("request " + request.Url.Substring(request.Url.IndexOf("/api/v1", StringComparison.Ordinal) + 7)
                    + (_store.Snapshot == null ? " (store empty)" : " (store has " + _store.Snapshot.ContentVersion + ")"));
                return request.Url.EndsWith("/content", StringComparison.Ordinal) ? _hold?.Task : null;
            }), allowInsecureHttp: true);
            _api.SetAccessToken("tok");
            _store = new ClientCityStore(() => 0);
            _store.SnapshotChanged += () => _events.Add("store takes " + _store.Snapshot!.ContentVersion);
            _sync = new ContentSync(new ContentService(_api), HeldVersion);
            _sync.ContentReplacing += () => _events.Add("replacing");
            _sync.ContentReplaced += (content, timeScale) => _events.Add("replaced " + content.ContentVersion + " x" + timeScale);
        }

        /// <summary>A <c>GET /city</c> answer of the version <paramref name="version"/>.</summary>
        private static string CityOf(string version)
        {
            return Fixtures.Read("snapshot-city-producing.json").Replace("\"contentVersion\":\"" + HeldVersion + "\"", "\"contentVersion\":\"" + version + "\"");
        }

        /// <summary>A <c>GET /content</c> answer of the version <paramref name="version"/>.</summary>
        private static string ContentOf(string version)
        {
            return Fixtures.Read("content.json").Replace("\"contentVersion\":\"" + HeldVersion + "\"", "\"contentVersion\":\"" + version + "\"");
        }

        /// <summary>Gives the sync the answer of <c>GET /city</c> that the wire holds next, the way the monitor does.</summary>
        private async Task<ApiError?> ApplyNextCityAsync()
        {
            ApiResult<CitySnapshotDto> answer = await _api.GetCityAsync(CancellationToken.None);
            return await _sync.ApplyAsync(answer, () => SnapshotMapper.Apply(_store, answer), CancellationToken.None);
        }

        /// <summary>A snapshot of the held content is applied at once and nothing is fetched.</summary>
        [Test]
        public async Task Apply_OfTheHeldContent_AppliesAtOnce_WithoutAFetch()
        {
            _wire.Answer(200, CityOf(HeldVersion));

            ApiError? failure = await ApplyNextCityAsync();

            Assert.That(failure, Is.Null);
            Assert.That(_events, Is.EqualTo(new[] { "request /city (store empty)", "store takes mvp-1" }));
        }

        /// <summary>
        /// The order of M0-58: the content is requested while the store does not hold the new snapshot; then the old parts are told to let go, the
        /// store takes the snapshot, and the new parts are told to build on the new content, with the server's clock speed.
        /// </summary>
        [Test]
        public async Task Apply_OfOtherContent_FetchesTheContentBeforeTheSnapshotIsApplied()
        {
            _wire.Answer(200, CityOf(NewVersion)).Answer(200, ContentOf(NewVersion));

            ApiError? failure = await ApplyNextCityAsync();

            Assert.That(failure, Is.Null);
            Assert.That(
                _events,
                Is.EqualTo(new[]
                {
                    "request /city (store empty)",
                    "request /content (store empty)",
                    "replacing",
                    "store takes mvp-2",
                    "replaced mvp-2 x1",
                }));
            Assert.That(_sync.LoadedVersion, Is.EqualTo(NewVersion));
        }

        /// <summary>After the replacement, the next snapshot of the new version is applied at once.</summary>
        [Test]
        public async Task Apply_AfterTheReplacement_TheNewVersionNeedsNoFetch()
        {
            _wire.Answer(200, CityOf(NewVersion)).Answer(200, ContentOf(NewVersion)).Answer(200, CityOf(NewVersion));
            await ApplyNextCityAsync();
            _events.Clear();

            ApiError? failure = await ApplyNextCityAsync();

            Assert.That(failure, Is.Null);
            Assert.That(_events, Is.EqualTo(new[] { "request /city (store has mvp-2)" }), "no /content request, no replacing, and the store ignored the same state version");
        }

        /// <summary>Content that cannot be fetched applies nothing: no teardown, no snapshot, the version stays, the error is the incompatible-answer one.</summary>
        [Test]
        public async Task Apply_WhenTheContentCannotBeFetched_AppliesNothing_AndIsIncompatible()
        {
            _wire.Answer(200, CityOf(NewVersion)).Answer(500, ServerFault);

            ApiError? failure = await ApplyNextCityAsync();

            Assert.That(failure!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(failure.Status, Is.EqualTo(200));
            Assert.That(ConnectionMonitor.StateFor(failure, false), Is.EqualTo(ConnectionState.IncompatibleVersion));
            Assert.That(_events, Is.EqualTo(new[] { "request /city (store empty)", "request /content (store empty)" }));
            Assert.That(_store.Snapshot, Is.Null);
            Assert.That(_sync.LoadedVersion, Is.EqualTo(HeldVersion));
        }

        /// <summary>Content of another version than the snapshot's (the server moved on again, or a stale cache) applies nothing.</summary>
        [Test]
        public async Task Apply_WhenTheContentIsNotTheSnapshotsVersion_AppliesNothing()
        {
            _wire.Answer(200, CityOf(NewVersion)).Answer(200, ContentOf("mvp-3"));

            ApiError? failure = await ApplyNextCityAsync();

            Assert.That(failure!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(failure.Detail, Does.Contain("mvp-3").And.Contain(NewVersion));
            Assert.That(_events, Is.EqualTo(new[] { "request /city (store empty)", "request /content (store empty)" }));
            Assert.That(_sync.LoadedVersion, Is.EqualTo(HeldVersion));
        }

        /// <summary>A failed answer (a refusal, no connection) is just given to the apply step, which reports it; nothing is fetched.</summary>
        [Test]
        public async Task Apply_OfAFailedAnswer_IsPassedOn_WithoutAFetch()
        {
            _wire.Answer(500, ServerFault);

            ApiError? failure = await ApplyNextCityAsync();

            Assert.That(failure!.Code, Is.EqualTo(ApiErrorCodes.InternalError));
            Assert.That(_events, Is.EqualTo(new[] { "request /city (store empty)" }));
        }

        /// <summary>Two answers of the new content that arrive together fetch it once, and the new parts are built once.</summary>
        [Test]
        public async Task Apply_OfTwoAnswersOfTheNewContent_FetchesItOnce()
        {
            _wire.Answer(200, CityOf(NewVersion)).Answer(200, CityOf(NewVersion)).Answer(200, ContentOf(NewVersion));
            ApiResult<CitySnapshotDto> first = await _api.GetCityAsync(CancellationToken.None);
            ApiResult<CitySnapshotDto> second = await _api.GetCityAsync(CancellationToken.None);
            _hold = new TaskCompletionSource<bool>();

            Task<ApiError?> a = _sync.ApplyAsync(first, () => SnapshotMapper.Apply(_store, first), CancellationToken.None);
            Task<ApiError?> b = _sync.ApplyAsync(second, () => SnapshotMapper.Apply(_store, second), CancellationToken.None);
            _hold.SetResult(true);
            ApiError?[] failures = await Task.WhenAll(a, b);

            Assert.That(failures, Is.All.Null);
            Assert.That(_events.FindAll(e => e.StartsWith("request /content", StringComparison.Ordinal)), Has.Count.EqualTo(1));
            Assert.That(_events.FindAll(e => e == "replacing"), Has.Count.EqualTo(1));
            Assert.That(_events.FindAll(e => e.StartsWith("replaced", StringComparison.Ordinal)), Has.Count.EqualTo(1));
        }

        /// <summary>A listener that throws is logged and does not stop the apply: the snapshot still goes in and the other listener still hears.</summary>
        [Test]
        public async Task Apply_WhenAListenerThrows_StillAppliesTheSnapshot()
        {
            _sync.ContentReplacing += () => throw new InvalidOperationException("a broken replacing listener");
            _wire.Answer(200, CityOf(NewVersion)).Answer(200, ContentOf(NewVersion));
            LogAssert.Expect(LogType.Exception, new Regex("a broken replacing listener"));

            ApiError? failure = await ApplyNextCityAsync();

            Assert.That(failure, Is.Null);
            Assert.That(_store.Snapshot!.ContentVersion, Is.EqualTo(NewVersion));
            Assert.That(_events, Does.Contain("replaced mvp-2 x1"));
        }

        /// <summary>The monitor with a sync: a poll that finds other content fetches it first, then applies the snapshot, and ends Connected.</summary>
        [Test]
        public async Task Poll_WithOtherContent_FetchesTheContentThenAppliesTheSnapshot()
        {
            ConnectionMonitor monitor = NewMonitor();
            _wire.Answer(200, CityOf(NewVersion)).Answer(200, ContentOf(NewVersion));

            await monitor.PollAsync(CancellationToken.None);

            Assert.That(monitor.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(monitor.LastError, Is.Null);
            Assert.That(_store.Snapshot!.ContentVersion, Is.EqualTo(NewVersion));
            Assert.That(_events.IndexOf("request /content (store empty)"), Is.LessThan(_events.IndexOf("store takes mvp-2")));
            Assert.That(_events.IndexOf("store takes mvp-2"), Is.LessThan(_events.FindIndex(e => e.StartsWith("replaced", StringComparison.Ordinal))));
        }

        /// <summary>A poll that finds other content it cannot fetch is incompatible with the store untouched, and a later poll that can fetch it recovers.</summary>
        [Test]
        public async Task Poll_WithOtherContentThatCannotBeFetched_IsIncompatible_AndALaterPollRecovers()
        {
            ConnectionMonitor monitor = NewMonitor();
            _wire.Answer(200, CityOf(NewVersion)).Answer(500, ServerFault);

            await monitor.PollAsync(CancellationToken.None);

            Assert.That(monitor.State, Is.EqualTo(ConnectionState.IncompatibleVersion));
            Assert.That(_store.Snapshot, Is.Null);

            _wire.Answer(200, CityOf(NewVersion)).Answer(200, ContentOf(NewVersion));
            await monitor.PollAsync(CancellationToken.None);

            Assert.That(monitor.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(_store.Snapshot!.ContentVersion, Is.EqualTo(NewVersion));
        }

        /// <summary>The API reporting an unsupported client build is <c>IncompatibleVersion</c>, from a poll and from the login.</summary>
        [Test]
        public async Task Poll_WithUnsupportedClientVersion_IsIncompatible()
        {
            ConnectionMonitor monitor = NewMonitor();
            _wire.Answer(426, UnsupportedBody);

            await monitor.PollAsync(CancellationToken.None);

            Assert.That(monitor.State, Is.EqualTo(ConnectionState.IncompatibleVersion));
            Assert.That(monitor.LastError!.Code, Is.EqualTo(ApiErrorCodes.UnsupportedClientVersion));
            Assert.That(ConnectionMonitor.StateFor(monitor.LastError, true), Is.EqualTo(ConnectionState.IncompatibleVersion));
        }

        /// <summary>A command whose answer shows the client cannot follow the content, or that the build is unsupported, makes a poll due.</summary>
        [TestCase(ApiErrorCodes.UnsupportedClientVersion, 426)]
        [TestCase(ApiErrorCodes.ClientInvalidResponse, 200)]
        public void ReportCommandError_AnIncompatibleAnswer_MakesAPollDue(string code, int status)
        {
            var scheduler = new PollScheduler(() => 1000, PollScheduler.DefaultIntervalSeconds);
            var monitor = new ConnectionMonitor(_api, new AuthService(_api, new MemoryAccessTokenStore()), _store, HeldVersion, "dev1", scheduler, () => true);

            monitor.ReportCommandError(new ApiError(code, status, "x", null, null, null));

            Assert.That(scheduler.IsDue, Is.True);
        }

        /// <summary>A command whose accepted answer is of other content fetches it first; the store takes the answer after, and the command succeeded.</summary>
        [Test]
        public async Task Command_WithAnAnswerOfOtherContent_FetchesTheContentBeforeApplyingIt()
        {
            var sender = new CommandSender(_api, _store, CommandPolicy.Default, _sync);
            _wire.Answer(200, CityOf(NewVersion)).Answer(200, ContentOf(NewVersion));

            CommandOutcome outcome = await sender.SendAsync(CommandIntent.UpgradeBuilding(3), CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(CommandOutcomeKind.Succeeded));
            Assert.That(_events.IndexOf("request /content (store empty)"), Is.LessThan(_events.IndexOf("store takes mvp-2")));
            Assert.That(_sync.LoadedVersion, Is.EqualTo(NewVersion));
        }

        /// <summary>A command answer of content that cannot be fetched is not applied: the command is reported failed with the incompatible-answer error.</summary>
        [Test]
        public async Task Command_WithAnAnswerOfContentThatCannotBeFetched_AppliesNothing()
        {
            var sender = new CommandSender(_api, _store, CommandPolicy.Default, _sync);
            _wire.Answer(200, CityOf(NewVersion)).Answer(500, ServerFault);

            CommandOutcome outcome = await sender.SendAsync(CommandIntent.UpgradeBuilding(3), CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(CommandOutcomeKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(_store.Snapshot, Is.Null);
        }

        /// <summary>A monitor over the test's client, store and sync, for the dev player <c>dev1</c> with a kept token.</summary>
        private ConnectionMonitor NewMonitor()
        {
            var tokens = new MemoryAccessTokenStore { Kept = new StoredLogin("dev1", "kept-token") };
            var auth = new AuthService(_api, tokens);
            Assert.That(auth.TryRestore("dev1"), Is.True);
            return new ConnectionMonitor(_api, auth, _store, HeldVersion, "dev1", new PollScheduler(() => 1000, PollScheduler.DefaultIntervalSeconds), () => true, _sync);
        }
    }
}
