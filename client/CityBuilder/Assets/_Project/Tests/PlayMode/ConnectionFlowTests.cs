#nullable enable

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Connection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The connection banner's wiring in a running scene (M0-55): a real <see cref="ConnectionMonitor"/> over a transport the test controls, the real
    /// banner and the flow between them. A lost connection shows the banner, the action shows "Trying…" until the poll it asked for ends, and a
    /// connection that comes back hides the banner again.
    /// </summary>
    public sealed class ConnectionFlowTests
    {
        /// <summary>The object the banner is made under.</summary>
        private GameObject _parent = null!;

        /// <summary>The wire of the monitor: every request waits for the test to answer it.</summary>
        private ControllableTransport _transport = null!;

        /// <summary>The banner under test.</summary>
        private ConnectionBanner _banner = null!;

        /// <summary>The monitor whose state the flow shows.</summary>
        private ConnectionMonitor _monitor = null!;

        /// <summary>The flow under test.</summary>
        private ConnectionFlow _flow = null!;

        /// <summary>Where the fake real-time clock of the scheduler stands, in seconds.</summary>
        private double _now;

        /// <summary>Before each test: a connected monitor, a banner and the flow; waits a frame for the document's tree.</summary>
        [UnitySetUp]
        public IEnumerator CreateFlow()
        {
            _parent = new GameObject("Test connection flow parent");
            _transport = new ControllableTransport();
            var api = new ApiClient("http://localhost:8080", 15, _transport);
            var auth = new AuthService(api, new NullAccessTokenStore());
            var store = new ClientCityStore(() => _now);
            _now = 1000;
            _monitor = new ConnectionMonitor(
                api, auth, store, "mvp-1", "dev1", new PollScheduler(() => _now, PollScheduler.DefaultIntervalSeconds), () => true);
            _banner = ConnectionBanner.Create(_parent.transform)!;
            yield return null;
            _flow = new ConnectionFlow(_monitor, store, _banner);
        }

        /// <summary>After each test: stops the flow and destroys everything.</summary>
        [TearDown]
        public void Destroy()
        {
            _flow.Dispose();
            Object.DestroyImmediate(_parent);
        }

        /// <summary>Waits until the transport holds an open request, or fails after a few seconds.</summary>
        private IEnumerator WaitForARequest()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_transport.OpenCount == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(_transport.OpenCount, Is.GreaterThan(0), "no request was sent");
        }

        /// <summary>Waits until a task is done, or fails after a few seconds.</summary>
        private static IEnumerator WaitFor(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "the task did not end");
        }

        /// <summary>A connected client shows neither the banner nor the blocking screen.</summary>
        [UnityTest]
        public IEnumerator Connected_ShowsNothing()
        {
            yield return null;

            Assert.That(_banner.Model!.IsAnythingShown, Is.False);
        }

        /// <summary>A poll that gets no answer turns the state Offline and shows the banner with the last sync and "Try again".</summary>
        [UnityTest]
        public IEnumerator ALostConnection_ShowsTheBanner()
        {
            Task poll = _monitor.PollAsync(CancellationToken.None);
            yield return WaitForARequest();
            _transport.FailWithNetworkError();
            yield return WaitFor(poll);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Offline));
            Assert.That(_banner.Model!.IsBannerShown, Is.True);
            Assert.That(_banner.Model.Message, Is.EqualTo(ConnectionBannerModel.OfflineMessage));
            Assert.That(_banner.Model.SyncedText, Does.StartWith("Last synced "));
            Assert.That(_banner.Model.ActionText, Is.EqualTo("Try again"));
        }

        /// <summary>The action shows "Trying…" and asks for a poll; when that poll ends, whatever it found, the button is back.</summary>
        [UnityTest]
        public IEnumerator TheAction_ShowsTrying_UntilTheAskedPollEnds()
        {
            Task poll = _monitor.PollAsync(CancellationToken.None);
            yield return WaitForARequest();
            _transport.FailWithNetworkError();
            yield return WaitFor(poll);

            _banner.PressAction();
            Assert.That(_banner.Model!.ActionText, Is.EqualTo("Trying…"));
            Assert.That(_banner.Model.ActionEnabled, Is.False);

            Task tick = _monitor.TickAsync(CancellationToken.None);
            yield return WaitForARequest();
            Assert.That(_banner.Model.ActionText, Is.EqualTo("Trying…"), "still trying while the poll is open");
            _transport.FailWithNetworkError();
            yield return WaitFor(tick);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.Offline));
            Assert.That(_banner.Model.ActionText, Is.EqualTo("Try again"));
            Assert.That(_banner.Model.ActionEnabled, Is.True);
        }

        /// <summary>A server error shows its own line; the banner is for that state too.</summary>
        [UnityTest]
        public IEnumerator AServerError_ShowsItsOwnLine()
        {
            Task poll = _monitor.PollAsync(CancellationToken.None);
            yield return WaitForARequest();
            _transport.Respond(500, "{\"type\":\"urn:citybuilder:error:internal-error\",\"title\":\"Internal error\",\"status\":500,\"detail\":\"x\",\"code\":\"INTERNAL_ERROR\"}");
            yield return WaitFor(poll);

            Assert.That(_monitor.State, Is.EqualTo(ConnectionState.ServerError));
            Assert.That(_banner.Model!.Message, Is.EqualTo(ConnectionBannerModel.ServerErrorMessage));
        }
    }
}
