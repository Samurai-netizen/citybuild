#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Feedback;
using CityBuilder.Client.Tests.EditMode.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Feedback
{
    /// <summary>
    /// <see cref="CommandFeedback"/> over a real <see cref="CommandSender"/> and <see cref="ApiClient"/> with a fake wire (M0-47): every way a
    /// command ends gives the toast the table promises, and nothing else does. The waits are the fake's, so no test depends on time.
    /// </summary>
    public sealed class CommandFeedbackTests
    {
        /// <summary>The fake wire.</summary>
        private FakeTransport _transport = null!;

        /// <summary>The sender whose results are shown.</summary>
        private CommandSender _sender = null!;

        /// <summary>The toasts the feedback fills.</summary>
        private ToastQueue _queue = null!;

        /// <summary>The feedback under test.</summary>
        private CommandFeedback _feedback = null!;

        /// <summary>The fake waits; <c>FinishAtOnce</c> unless a test holds a debounce open.</summary>
        private FakeDelay _delay = null!;

        /// <summary>Before each test: a sender over the fake wire whose waits end at once, a queue and the feedback.</summary>
        [SetUp]
        public void CreateFeedback()
        {
            _transport = new FakeTransport();
            _delay = new FakeDelay { FinishAtOnce = true };
            var api = new ApiClient("http://localhost:8080", 15, _transport);
            api.SetAccessToken("tok-1");
            _sender = new CommandSender(api, new ClientCityStore(() => 0), CommandPolicy.Default, _delay.Delay, Guid.NewGuid);
            _queue = new ToastQueue();
            _feedback = new CommandFeedback(_sender, _queue, () => 0);
        }

        /// <summary>After each test: the feedback stops listening.</summary>
        [TearDown]
        public void Destroy()
        {
            _feedback.Dispose();
        }

        /// <summary>The server's real answer to a command that worked.</summary>
        private static string Success()
        {
            return Fixtures.Read("snapshot-city-start.json");
        }

        /// <summary>Each of the four commands that works gives its own success toast.</summary>
        [Test]
        public async Task EachAcceptedCommand_ShowsItsSuccessToast()
        {
            _transport.Answer(200, Success()).Answer(200, Success()).Answer(200, Success()).Answer(200, Success());

            await _sender.SendAsync(CommandIntent.PlaceBuilding("house", 1, 2), CancellationToken.None);
            await _sender.SendAsync(CommandIntent.UpgradeBuilding(3), CancellationToken.None);
            await _sender.SendAsync(CommandIntent.AssignWorkers(3, 2), CancellationToken.None);
            await _sender.SendAsync(CommandIntent.StartResearch("trade_charter"), CancellationToken.None);

            Assert.That(_queue.Visible.Count, Is.EqualTo(3), "the queue shows three at once; the oldest was pushed out");
            Assert.That(_queue.Visible[0].Text, Is.EqualTo("Upgrade started"));
            Assert.That(_queue.Visible[1].Text, Is.EqualTo("Workers updated"));
            Assert.That(_queue.Visible[2].Text, Is.EqualTo("Research started"));
            Assert.That(_queue.Visible[2].Kind, Is.EqualTo(ToastKind.Success));
        }

        /// <summary>A placement that works says so.</summary>
        [Test]
        public async Task AcceptedPlacement_ShowsConstructionStarted()
        {
            _transport.Answer(200, Success());

            await _sender.SendAsync(CommandIntent.PlaceBuilding("house", 1, 2), CancellationToken.None);

            Assert.That(_queue.Visible.Count, Is.EqualTo(1));
            Assert.That(_queue.Visible[0].Text, Is.EqualTo("Construction started"));
        }

        /// <summary>A refusal shows the table's sentence as an error toast, never the code or the server's text, and logs the code for a bug report.</summary>
        [Test]
        public async Task ARefusal_ShowsTheHumanMessage_AndLogsTheCode()
        {
            _transport.Answer(409, Fixtures.Read("error-409-NO_FREE_BUILDER.json"));
            LogAssert.Expect(LogType.Warning, new Regex("NO_FREE_BUILDER"));

            await _sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);

            Assert.That(_queue.Visible.Count, Is.EqualTo(1));
            Assert.That(_queue.Visible[0].Kind, Is.EqualTo(ToastKind.Error));
            Assert.That(_queue.Visible[0].Text, Is.EqualTo("All builders are busy"));
        }

        /// <summary>A command that never got an answer shows the retry hint, not a transport message.</summary>
        [Test]
        public async Task ALostConnection_ShowsTheRetryHint()
        {
            _transport.Fail("Cannot resolve destination host").Fail("Cannot resolve destination host").Fail("Cannot resolve destination host");
            LogAssert.Expect(LogType.Warning, new Regex("CLIENT_NETWORK_ERROR"));

            await _sender.SendAsync(CommandIntent.PlaceBuilding("house", 1, 2), CancellationToken.None);

            Assert.That(_queue.Visible.Count, Is.EqualTo(1));
            Assert.That(_queue.Visible[0].Text, Is.EqualTo("No connection. Check your network and try again"));
            Assert.That(_queue.Visible[0].Text, Does.Not.Contain("resolve"));
        }

        /// <summary>A rate limit tells the player how long the server asked to wait.</summary>
        [Test]
        public async Task ARateLimit_NamesTheWait()
        {
            _transport.Answer(429, Fixtures.Read("error-429-RATE_LIMITED.json"), "3");
            LogAssert.Expect(LogType.Warning, new Regex("RATE_LIMITED"));

            await _sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);

            Assert.That(_queue.Visible[0].Text, Is.EqualTo("Too many requests. Wait 3 s"));
        }

        /// <summary>Five rapid stepper taps show one toast: the replaced targets never reached the server.</summary>
        [Test]
        public async Task SupersededWorkerTargets_ShowNothing()
        {
            _delay.FinishAtOnce = false;
            _transport.Answer(200, Success());

            var taps = new System.Collections.Generic.List<Task<CommandOutcome>>();
            for (int workers = 1; workers <= 5; workers++)
            {
                taps.Add(_sender.SendAsync(CommandIntent.AssignWorkers(3, workers), CancellationToken.None));
            }

            for (int i = 0; i < 4; i++)
            {
                await taps[i];
            }

            Assert.That(_queue.Visible, Is.Empty, "a superseded target is silent");
            while (_delay.OpenCount != 1)
            {
                await Task.Delay(1);
            }

            _delay.CompleteAt(0);
            await taps[4];

            Assert.That(_queue.Visible.Count, Is.EqualTo(1));
            Assert.That(_queue.Visible[0].Text, Is.EqualTo("Workers updated"));
        }

        /// <summary>After <c>Dispose</c> a result shows nothing.</summary>
        [Test]
        public async Task AfterDispose_NothingIsShown()
        {
            _transport.Answer(200, Success());
            _feedback.Dispose();

            await _sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);

            Assert.That(_queue.Visible, Is.Empty);
        }

        /// <summary>The constructor refuses null arguments.</summary>
        [Test]
        public void NullArguments_AreRefused()
        {
            Assert.That(() => new CommandFeedback(null!, _queue, () => 0), Throws.ArgumentNullException);
            Assert.That(() => new CommandFeedback(_sender, null!, () => 0), Throws.ArgumentNullException);
            Assert.That(() => new CommandFeedback(_sender, _queue, null!), Throws.ArgumentNullException);
        }
    }
}
