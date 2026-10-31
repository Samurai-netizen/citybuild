#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// <see cref="CommandSender"/> over the real <see cref="ApiClient"/>, a <see cref="FakeTransport"/> and a <see cref="FakeDelay"/>: one request id per
    /// intent and the same one on every retry, which failures are repeated and which are final, exactly one snapshot for the store, the pending
    /// list, and the coalescing of the worker stepper. Waits are the fake's, so nothing here depends on time.
    /// </summary>
    public sealed class CommandSenderTests
    {
        /// <summary>The longest a test waits for a state that is reached on another thread's turn.</summary>
        private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(5);

        /// <summary>A 409 answer with the code <c>CONCURRENCY_CONFLICT</c>, which no fixture holds.</summary>
        private const string ConflictBody = "{\"type\":\"urn:citybuilder:error:concurrency-conflict\",\"title\":\"Conflict\",\"status\":409,"
            + "\"detail\":\"Try again.\",\"code\":\"CONCURRENCY_CONFLICT\",\"traceId\":\"00-4be95066533e95008abe4dc29f10300a-c76e4e06559302a7-00\"}";

        /// <summary>What a test needs together: the sender and everything it is built on.</summary>
        private sealed class Rig
        {
            /// <summary>The number of request ids made so far.</summary>
            private int _ids;

            /// <summary>Builds the rig; <paramref name="finishDelaysAtOnce"/> makes every wait end as soon as it is asked for.</summary>
            public Rig(bool finishDelaysAtOnce)
            {
                Delay.FinishAtOnce = finishDelaysAtOnce;
                Api = new ApiClient("http://localhost:8080", 15, Transport);
                Api.SetAccessToken("tok-1");
                Store = new ClientCityStore(() => 0);
                Store.SnapshotChanged += () => SnapshotsTaken++;
                Sender = new CommandSender(Api, Store, CommandPolicy.Default, Delay.Delay, () => IdNumber(++_ids));
                Sender.PendingChanged += () => PendingChanges++;
            }

            /// <summary>The fake wire.</summary>
            public FakeTransport Transport { get; } = new FakeTransport();

            /// <summary>The fake waits.</summary>
            public FakeDelay Delay { get; } = new FakeDelay();

            /// <summary>The client over the fake wire.</summary>
            public ApiClient Api { get; }

            /// <summary>The store the sender gives snapshots to.</summary>
            public ClientCityStore Store { get; }

            /// <summary>The sender under test.</summary>
            public CommandSender Sender { get; }

            /// <summary>How many times the store took a snapshot.</summary>
            public int SnapshotsTaken { get; private set; }

            /// <summary>How many times the sender said its pending list changed.</summary>
            public int PendingChanges { get; private set; }

            /// <summary>The request id the sender makes as number <paramref name="number"/> (from 1).</summary>
            public static Guid IdNumber(int number)
            {
                return new Guid("6f1c0000-0000-4000-8000-" + number.ToString("D12"));
            }

            /// <summary>The server's real answer to a command that worked.</summary>
            public static string Success()
            {
                return Fixtures.Read("snapshot-city-start.json");
            }
        }

        /// <summary>Waits, a few milliseconds at a time, until <paramref name="condition"/> holds; fails the test if it never does.</summary>
        private static async Task WaitUntil(Func<bool> condition, string what)
        {
            DateTime deadline = DateTime.UtcNow + PatienceLimit;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail("Gave up waiting for: " + what);
                }

                await Task.Delay(1);
            }
        }

        /// <summary>Waits for <paramref name="task"/> and fails the test unless it ended cancelled.</summary>
        private static async Task AssertCancelled(Task<CommandOutcome> task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Assert.Fail("The task should have been cancelled.");
        }

        /// <summary>
        /// A listener of the pending list that throws when the command leaves it does not take the command with it: the caller's task still ends with
        /// the outcome. Before, the task never ended, and a worker lane stayed blocked for good (CR-0004).
        /// </summary>
        [Test]
        public async Task AListenerThatThrows_DoesNotLeaveTheCommandUnfinished()
        {
            var rig = new Rig(true);
            rig.Transport.Answer(200, Rig.Success()).Answer(200, Rig.Success());
            bool throwing = true;
            LogAssert.Expect(LogType.Exception, new Regex("a broken listener"));
            rig.Sender.PendingChanged += () =>
            {
                if (throwing && rig.Sender.Pending.Count == 0)
                {
                    throw new InvalidOperationException("a broken listener");
                }
            };

            Task<CommandOutcome> first = rig.Sender.SendAsync(CommandIntent.AssignWorkers(1, 2), CancellationToken.None);
            await WaitUntil(() => first.IsCompleted, "the command to end although a listener threw");

            Assert.That(first.Result.Kind, Is.EqualTo(CommandOutcomeKind.Succeeded));
            throwing = false;
            CommandOutcome second = await rig.Sender.SendAsync(CommandIntent.AssignWorkers(1, 3), CancellationToken.None);
            Assert.That(second.Kind, Is.EqualTo(CommandOutcomeKind.Succeeded), "The building's lane is free again.");
        }

        /// <summary>An accepted command gives the store its snapshot once, ends as succeeded after one request, and leaves nothing pending.</summary>
        [Test]
        public async Task AnAcceptedCommand_AppliesItsSnapshotOnce()
        {
            var rig = new Rig(true);
            rig.Transport.Answer(200, Rig.Success());

            CommandOutcome outcome = await rig.Sender.SendAsync(CommandIntent.PlaceBuilding("house", 1, 2), CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(CommandOutcomeKind.Succeeded));
            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Attempts, Is.EqualTo(1));
            Assert.That(outcome.Error, Is.Null);
            Assert.That(outcome.RequestId, Is.EqualTo(Rig.IdNumber(1)));
            Assert.That(rig.SnapshotsTaken, Is.EqualTo(1));
            Assert.That(rig.Store.Snapshot, Is.Not.Null);
            Assert.That(rig.Sender.Pending, Is.Empty);
            Assert.That(rig.PendingChanges, Is.GreaterThanOrEqualTo(2), "The intent started and ended.");
        }

        /// <summary>Each of the four commands goes to its own endpoint with its own arguments and the intent's request id.</summary>
        [Test]
        public async Task EachCommand_IsSentToItsEndpoint()
        {
            var rig = new Rig(true);
            rig.Transport.Answer(200, Rig.Success()).Answer(200, Rig.Success()).Answer(200, Rig.Success()).Answer(200, Rig.Success());

            await rig.Sender.SendAsync(CommandIntent.PlaceBuilding("house", 4, 7), CancellationToken.None);
            await rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(3), CancellationToken.None);
            rig.Delay.FinishAtOnce = false;
            Task<CommandOutcome> workers = rig.Sender.SendAsync(CommandIntent.AssignWorkers(3, 2), CancellationToken.None);
            await WaitUntil(() => rig.Delay.OpenCount == 1, "the debounce");
            rig.Delay.CompleteAt(0);
            await workers;
            await rig.Sender.SendAsync(CommandIntent.StartResearch("trade_charter"), CancellationToken.None);

            IReadOnlyList<ApiTransportRequest> sent = rig.Transport.Requests;
            Assert.That(sent[0].Url, Is.EqualTo("http://localhost:8080/api/v1/commands/place-building"));
            Assert.That(sent[0].JsonBody, Is.EqualTo("{\"requestId\":\"" + Rig.IdNumber(1) + "\",\"buildingTypeId\":\"house\",\"x\":4,\"y\":7}"));
            Assert.That(sent[1].Url, Is.EqualTo("http://localhost:8080/api/v1/commands/upgrade-building"));
            Assert.That(sent[1].JsonBody, Is.EqualTo("{\"requestId\":\"" + Rig.IdNumber(2) + "\",\"buildingId\":3}"));
            Assert.That(sent[2].Url, Is.EqualTo("http://localhost:8080/api/v1/commands/assign-workers"));
            Assert.That(sent[2].JsonBody, Is.EqualTo("{\"requestId\":\"" + Rig.IdNumber(3) + "\",\"buildingId\":3,\"workers\":2}"));
            Assert.That(sent[3].Url, Is.EqualTo("http://localhost:8080/api/v1/commands/start-research"));
            Assert.That(sent[3].JsonBody, Is.EqualTo("{\"requestId\":\"" + Rig.IdNumber(4) + "\",\"researchId\":\"trade_charter\"}"));
        }

        /// <summary>A retry sends the very same request id; the waits are 0.5 s and then 1 s; the third request is accepted.</summary>
        [Test]
        public async Task ARetry_KeepsTheRequestId_AndWaitsTheBackoff()
        {
            var rig = new Rig(true);
            rig.Transport.Fail("offline").Answer(503, "<html>Bad gateway</html>").Answer(200, Rig.Success());

            CommandOutcome outcome = await rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(3), CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(CommandOutcomeKind.Succeeded));
            Assert.That(outcome.Attempts, Is.EqualTo(3));
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(3));
            foreach (ApiTransportRequest sent in rig.Transport.Requests)
            {
                Assert.That(sent.JsonBody, Is.EqualTo("{\"requestId\":\"" + Rig.IdNumber(1) + "\",\"buildingId\":3}"));
            }

            Assert.That(rig.Delay.Requested, Is.EqualTo(new[] { TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1) }));
        }

        /// <summary>Success after two timeouts: three requests, one request id, exactly one snapshot for the store.</summary>
        [Test]
        public async Task SuccessAfterTwoTimeouts_AppliesExactlyOneSnapshot()
        {
            var rig = new Rig(true);
            rig.Transport.TimeOut().TimeOut().Answer(200, Rig.Success());

            CommandOutcome outcome = await rig.Sender.SendAsync(CommandIntent.StartResearch("trade_charter"), CancellationToken.None);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Attempts, Is.EqualTo(3));
            Assert.That(rig.SnapshotsTaken, Is.EqualTo(1));
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(3));
            Assert.That(rig.Transport.Requests[0].JsonBody, Is.EqualTo(rig.Transport.Requests[2].JsonBody));
        }

        /// <summary>A server fault or a concurrency conflict is repeated like a lost connection.</summary>
        [TestCase(500)]
        [TestCase(502)]
        [TestCase(503)]
        [TestCase(409)]
        public async Task AServerFaultOrAConflict_IsRepeated(int status)
        {
            var rig = new Rig(true);
            rig.Transport.Answer(status, status == 409 ? ConflictBody : "<html>oops</html>").Answer(200, Rig.Success());

            CommandOutcome outcome = await rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(2));
        }

        /// <summary>A rule's refusal is final: one request, no wait, the server's code in the outcome, and the store untouched.</summary>
        [TestCase("error-422-WORKER_LIMIT.json", 422, "WORKER_LIMIT")]
        [TestCase("error-422-INSUFFICIENT_RESOURCE.json", 422, "INSUFFICIENT_RESOURCE")]
        [TestCase("error-409-TILE_OCCUPIED.json", 409, "TILE_OCCUPIED")]
        [TestCase("error-409-BUILDING_BUSY.json", 409, "BUILDING_BUSY")]
        [TestCase("error-400-INVALID_ARGUMENT.json", 400, "INVALID_ARGUMENT")]
        [TestCase("error-404-BUILDING_NOT_FOUND.json", 404, "BUILDING_NOT_FOUND")]
        [TestCase("error-401-AUTHENTICATION_REQUIRED.json", 401, "AUTHENTICATION_REQUIRED")]
        [TestCase("error-429-RATE_LIMITED.json", 429, "RATE_LIMITED")]
        public async Task ARefusal_IsNotRetried(string fixture, int status, string code)
        {
            var rig = new Rig(true);
            rig.Transport.Answer(status, Fixtures.Read(fixture), status == 429 ? "3" : null);

            CommandOutcome outcome = await rig.Sender.SendAsync(CommandIntent.PlaceBuilding("house", 0, 0), CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(CommandOutcomeKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(code));
            Assert.That(outcome.Attempts, Is.EqualTo(1));
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(1));
            Assert.That(rig.Delay.Requested, Is.Empty);
            Assert.That(rig.Store.Snapshot, Is.Null);
            Assert.That(rig.SnapshotsTaken, Is.EqualTo(0));
            Assert.That(rig.Sender.Pending, Is.Empty);
        }

        /// <summary>A 200 whose body is not the contract's is a failure that is not repeated: asking again would get the same.</summary>
        [Test]
        public async Task AnUnusableSuccessAnswer_IsNotRetried()
        {
            var rig = new Rig(true);
            rig.Transport.Answer(200, "<html>not json</html>");

            CommandOutcome outcome = await rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);

            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(1));
        }

        /// <summary>After the third failed request the sender gives up with the last error; there is no fourth request and the store is untouched.</summary>
        [Test]
        public async Task AfterTheMaximumAttempts_TheLastErrorIsReported()
        {
            var rig = new Rig(true);
            rig.Transport.TimeOut().Answer(503, "<html>x</html>").Fail("offline");

            CommandOutcome outcome = await rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);

            Assert.That(outcome.Kind, Is.EqualTo(CommandOutcomeKind.Failed));
            Assert.That(outcome.Error!.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
            Assert.That(outcome.Attempts, Is.EqualTo(3));
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(3));
            Assert.That(rig.Delay.Requested, Has.Count.EqualTo(2));
            Assert.That(rig.Store.Snapshot, Is.Null);
            Assert.That(rig.Sender.Pending, Is.Empty);
        }

        /// <summary>Every intent gets its own request id, also a second one of the same kind after the first ended.</summary>
        [Test]
        public async Task EachIntent_GetsItsOwnRequestId()
        {
            var rig = new Rig(true);
            rig.Transport.Answer(200, Rig.Success()).Answer(200, Rig.Success());

            CommandOutcome first = await rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);
            CommandOutcome second = await rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);

            Assert.That(first.RequestId, Is.EqualTo(Rig.IdNumber(1)));
            Assert.That(second.RequestId, Is.EqualTo(Rig.IdNumber(2)));
        }

        /// <summary>While a command waits to be repeated it is pending, in the retrying phase, with its attempts so far; a double tap shares its result.</summary>
        [Test]
        public async Task ARetryingCommand_IsPending_AndARepeatOfItIsTheSameIntent()
        {
            var rig = new Rig(false);
            rig.Transport.Fail("offline").Answer(200, Rig.Success());
            CommandIntent intent = CommandIntent.PlaceBuilding("house", 1, 2);

            Task<CommandOutcome> first = rig.Sender.SendAsync(intent, CancellationToken.None);
            await WaitUntil(() => rig.Delay.OpenCount == 1, "the backoff");
            Task<CommandOutcome> second = rig.Sender.SendAsync(CommandIntent.PlaceBuilding("house", 1, 2), CancellationToken.None);

            Assert.That(second, Is.SameAs(first));
            Assert.That(rig.Sender.Pending.Count, Is.EqualTo(1));
            PendingCommand pending = rig.Sender.Pending[0];
            Assert.That(pending.Phase, Is.EqualTo(CommandPhase.Retrying));
            Assert.That(pending.Attempts, Is.EqualTo(1));
            Assert.That(pending.Intent, Is.EqualTo(intent));
            Assert.That(pending.RequestId, Is.EqualTo(Rig.IdNumber(1)));

            rig.Delay.CompleteAt(0);
            CommandOutcome outcome = await first;

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(2));
            Assert.That(rig.Sender.Pending, Is.Empty);
        }

        /// <summary>A different intent of the same kind is not a repeat: it is sent on its own.</summary>
        [Test]
        public async Task ADifferentIntent_IsSentOnItsOwn()
        {
            var rig = new Rig(false);
            rig.Transport.Fail("offline").Answer(200, Rig.Success()).Answer(200, Rig.Success());

            Task<CommandOutcome> first = rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(1), CancellationToken.None);
            await WaitUntil(() => rig.Delay.OpenCount == 1, "the backoff");
            Task<CommandOutcome> other = rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(2), CancellationToken.None);

            Assert.That(other, Is.Not.SameAs(first));
            Assert.That((await other).IsSuccess, Is.True);
            Assert.That(rig.Sender.Pending.Count, Is.EqualTo(1), "Only the first is still waiting to be repeated.");
            Assert.That(rig.Sender.Pending[0].Intent, Is.EqualTo(CommandIntent.UpgradeBuilding(1)));
            rig.Delay.CompleteAt(0);
            Assert.That((await first).IsSuccess, Is.True);
        }

        /// <summary>Five rapid taps on the stepper send one command, with the final value; the four replaced taps end as superseded without a request.</summary>
        [Test]
        public async Task FiveRapidStepperTaps_SendOneCommandWithTheFinalValue()
        {
            var rig = new Rig(false);
            rig.Transport.Answer(200, Rig.Success());

            var taps = new List<Task<CommandOutcome>>();
            for (int workers = 1; workers <= 5; workers++)
            {
                taps.Add(rig.Sender.SendAsync(CommandIntent.AssignWorkers(3, workers), CancellationToken.None));
            }

            Assert.That(rig.Sender.Pending.Count, Is.EqualTo(1), "Only the last target is pending.");
            Assert.That(rig.Sender.Pending[0].Intent.Workers, Is.EqualTo(5));
            Assert.That(rig.Sender.Pending[0].Phase, Is.EqualTo(CommandPhase.Waiting));
            Assert.That(rig.Transport.Requests, Is.Empty, "Nothing is sent before the debounce has passed.");
            Assert.That(rig.Delay.Requested, Is.All.EqualTo(TimeSpan.FromMilliseconds(400)));
            for (int i = 0; i < 4; i++)
            {
                CommandOutcome replaced = await taps[i];
                Assert.That(replaced.Kind, Is.EqualTo(CommandOutcomeKind.Superseded));
                Assert.That(replaced.Attempts, Is.EqualTo(0));
            }

            await WaitUntil(() => rig.Delay.OpenCount == 1, "only the last debounce is still open");
            rig.Delay.CompleteAt(0);
            CommandOutcome last = await taps[4];

            Assert.That(last.Kind, Is.EqualTo(CommandOutcomeKind.Succeeded));
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(1));
            Assert.That(rig.Transport.Requests[0].JsonBody, Is.EqualTo("{\"requestId\":\"" + Rig.IdNumber(5) + "\",\"buildingId\":3,\"workers\":5}"));
            Assert.That(rig.SnapshotsTaken, Is.EqualTo(1));
            Assert.That(rig.Sender.Pending, Is.Empty);
        }

        /// <summary>Targets for different buildings do not replace each other.</summary>
        [Test]
        public async Task StepperTargetsOfDifferentBuildings_AreIndependent()
        {
            var rig = new Rig(false);
            rig.Transport.Answer(200, Rig.Success()).Answer(200, Rig.Success());

            Task<CommandOutcome> a = rig.Sender.SendAsync(CommandIntent.AssignWorkers(1, 2), CancellationToken.None);
            Task<CommandOutcome> b = rig.Sender.SendAsync(CommandIntent.AssignWorkers(2, 2), CancellationToken.None);
            Assert.That(rig.Sender.Pending.Count, Is.EqualTo(2));
            await WaitUntil(() => rig.Delay.OpenCount == 2, "two debounces");
            rig.Delay.CompleteAt(0);
            rig.Delay.CompleteAt(0);

            Assert.That((await a).IsSuccess, Is.True);
            Assert.That((await b).IsSuccess, Is.True);
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(2));
        }

        /// <summary>A target is not sent while the building's earlier target is still on its way (here waiting to be repeated); the server sees them in order.</summary>
        [Test]
        public async Task ANewTarget_WaitsForTheEarlierTargetOfTheSameBuilding()
        {
            var rig = new Rig(false);
            rig.Transport.Fail("offline").Answer(200, Rig.Success()).Answer(200, Rig.Success());

            Task<CommandOutcome> first = rig.Sender.SendAsync(CommandIntent.AssignWorkers(3, 1), CancellationToken.None);
            await WaitUntil(() => rig.Delay.OpenCount == 1, "the first debounce");
            rig.Delay.CompleteAt(0);
            await WaitUntil(() => rig.Delay.OpenCount == 1 && rig.Transport.Requests.Count == 1, "the first target waiting to be repeated");
            Task<CommandOutcome> second = rig.Sender.SendAsync(CommandIntent.AssignWorkers(3, 2), CancellationToken.None);
            await WaitUntil(() => rig.Delay.OpenCount == 2, "the second debounce");
            rig.Delay.CompleteAt(1);
            await Task.Delay(20);

            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(1), "The second target must wait for the first.");
            Assert.That(rig.Sender.Pending.Count, Is.EqualTo(2));

            rig.Delay.CompleteAt(0);
            Assert.That((await first).IsSuccess, Is.True);
            Assert.That((await second).IsSuccess, Is.True);
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(3));
            Assert.That(rig.Transport.Requests[1].JsonBody, Is.EqualTo(rig.Transport.Requests[0].JsonBody), "The repeat of the first.");
            Assert.That(rig.Transport.Requests[2].JsonBody, Does.EndWith("\"workers\":2}"));
            Assert.That(rig.Sender.Pending, Is.Empty);
        }

        /// <summary>A newer target replaces one that is only waiting for the earlier target: it ends at once and is never sent.</summary>
        [Test]
        public async Task ANewTarget_ReplacesOneWaitingBehindAnEarlierTarget()
        {
            var rig = new Rig(false);
            rig.Transport.Fail("offline").Answer(200, Rig.Success()).Answer(200, Rig.Success());

            Task<CommandOutcome> first = rig.Sender.SendAsync(CommandIntent.AssignWorkers(3, 1), CancellationToken.None);
            await WaitUntil(() => rig.Delay.OpenCount == 1, "the first debounce");
            rig.Delay.CompleteAt(0);
            await WaitUntil(() => rig.Delay.OpenCount == 1 && rig.Transport.Requests.Count == 1, "the first target waiting to be repeated");
            Task<CommandOutcome> second = rig.Sender.SendAsync(CommandIntent.AssignWorkers(3, 2), CancellationToken.None);
            await WaitUntil(() => rig.Delay.OpenCount == 2, "the second debounce");
            rig.Delay.CompleteAt(1);
            await Task.Delay(20);
            Task<CommandOutcome> third = rig.Sender.SendAsync(CommandIntent.AssignWorkers(3, 3), CancellationToken.None);

            Assert.That((await second).Kind, Is.EqualTo(CommandOutcomeKind.Superseded));
            await WaitUntil(() => rig.Delay.OpenCount == 2, "the third debounce");
            rig.Delay.CompleteAt(1);
            rig.Delay.CompleteAt(0);
            Assert.That((await first).IsSuccess, Is.True);
            Assert.That((await third).IsSuccess, Is.True);
            Assert.That(rig.Transport.Requests, Has.Count.EqualTo(3));
            Assert.That(rig.Transport.Requests[2].JsonBody, Does.EndWith("\"workers\":3}"));
        }

        /// <summary>A cancelled caller leaves nothing pending: a waiting target is never sent and its task is cancelled.</summary>
        [Test]
        public async Task CancellingAWaitingTarget_RemovesItWithoutARequest()
        {
            var rig = new Rig(false);
            using (var cancellation = new CancellationTokenSource())
            {
                Task<CommandOutcome> target = rig.Sender.SendAsync(CommandIntent.AssignWorkers(3, 1), cancellation.Token);
                await WaitUntil(() => rig.Delay.OpenCount == 1, "the debounce");

                cancellation.Cancel();

                await AssertCancelled(target);
                Assert.That(rig.Sender.Pending, Is.Empty);
                Assert.That(rig.Transport.Requests, Is.Empty);
            }
        }

        /// <summary>A cancelled caller stops a command that waits to be repeated: no further request, nothing pending.</summary>
        [Test]
        public async Task CancellingARetryingCommand_StopsTheRetries()
        {
            var rig = new Rig(false);
            rig.Transport.Fail("offline");
            using (var cancellation = new CancellationTokenSource())
            {
                Task<CommandOutcome> command = rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(1), cancellation.Token);
                await WaitUntil(() => rig.Delay.OpenCount == 1, "the backoff");

                cancellation.Cancel();

                await AssertCancelled(command);
                Assert.That(rig.Sender.Pending, Is.Empty);
                Assert.That(rig.Transport.Requests, Has.Count.EqualTo(1));
            }
        }

        /// <summary>A token that is cancelled before the call throws at once and starts nothing.</summary>
        [Test]
        public void ACancelledToken_ThrowsBeforeAnythingStarts()
        {
            var rig = new Rig(true);

            Assert.That(() => rig.Sender.SendAsync(CommandIntent.UpgradeBuilding(1), new CancellationToken(true)), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(rig.Sender.Pending, Is.Empty);
            Assert.That(rig.Transport.Requests, Is.Empty);
        }

        /// <summary>The constructor and the call refuse null arguments.</summary>
        [Test]
        public void NullArguments_AreRefused()
        {
            var rig = new Rig(true);

            Assert.That(() => new CommandSender(null!, rig.Store, CommandPolicy.Default), Throws.ArgumentNullException);
            Assert.That(() => new CommandSender(rig.Api, null!, CommandPolicy.Default), Throws.ArgumentNullException);
            Assert.That(() => new CommandSender(rig.Api, rig.Store, null!), Throws.ArgumentNullException);
            Assert.That(() => rig.Sender.SendAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
        }
    }
}
