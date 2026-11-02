#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Tests.EditMode.Core;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// The server applied a command and its answer never arrived (M0-56; <c>05</c> Idempotency rules): <see cref="CommandSender"/> over the real
    /// <see cref="ApiClient"/> and a <see cref="ReceiptServer"/>, a wire that keeps a receipt the way the server does. The retry carries the same request
    /// id, gets the stored answer and the store ends with exactly one new building. The answers are real ones from <c>Tests/Fixtures</c>: the
    /// city with no building, and the placement of one house.
    /// </summary>
    public sealed class LostResponseTests
    {
        /// <summary>The city after the placement, one version later and a minute on, as a poll would see it.</summary>
        private static string PolledCity()
        {
            return Fixtures.Read("snapshot-command-place-building.json")
                .Replace("\"stateVersion\":7", "\"stateVersion\":8")
                .Replace("\"clock\":9", "\"clock\":69");
        }

        /// <summary>A sender over <paramref name="server"/> on a store that holds the city with no building; <paramref name="delay"/> stands for its waits.</summary>
        private static (CommandSender Sender, ClientCityStore Store, ApiClient Api) Build(ReceiptServer server, Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            var api = new ApiClient("http://localhost:8080", 15, server, allowInsecureHttp: true);
            api.SetAccessToken("tok-1");
            var store = new ClientCityStore(() => 0);
            SnapshotMapper.Apply(store, ApiResult<CitySnapshotDto>.Success(CityTestData.Dto("snapshot-city-start.json")));
            return (new CommandSender(api, store, CommandPolicy.Default, delay ?? ((_, _) => Task.CompletedTask), Guid.NewGuid), store, api);
        }

        /// <summary>The buildings of the city the store holds.</summary>
        private static IReadOnlyList<Building> BuildingsIn(ClientCityStore store)
        {
            return store.Snapshot!.CopyCity().Buildings;
        }

        /// <summary>
        /// The first request is applied and then times out: the retry sends the same body with the same request id, the server runs nothing and
        /// answers from its receipt, and the store takes that answer. One new building, paid once, and the command ends as succeeded.
        /// </summary>
        [Test]
        public async Task ATimeoutAfterTheServerApplied_TheRetryGetsTheStoredAnswer_AndOneNewBuilding()
        {
            var server = new ReceiptServer(Fixtures.Read("snapshot-command-place-building.json"), lostAnswers: 1);
            (CommandSender sender, ClientCityStore store, _) = Build(server);
            Assert.That(BuildingsIn(store), Is.Empty);

            CommandOutcome outcome = await sender.SendAsync(CommandIntent.PlaceBuilding("house", 0, 0), CancellationToken.None);

            Assert.That((outcome.IsSuccess, outcome.Attempts), Is.EqualTo((true, 2)));
            Assert.That(server.Commands, Has.Count.EqualTo(2));
            Assert.That(server.Commands[1], Is.EqualTo(server.Commands[0]), "The retry is the same body, so the same request id.");
            Assert.That((server.TimesApplied, server.Replays), Is.EqualTo((1, 1)));
            Assert.That(BuildingsIn(store), Has.Count.EqualTo(1));
            Assert.That(store.Snapshot!.StateVersion, Is.EqualTo(7));
            Assert.That(sender.Pending, Is.Empty);
        }

        /// <summary>The first two answers are lost: the third request gets the stored answer, and the command still ran once.</summary>
        [Test]
        public async Task TwoLostAnswers_StillApplyOnce()
        {
            var server = new ReceiptServer(Fixtures.Read("snapshot-command-place-building.json"), lostAnswers: 2);
            (CommandSender sender, ClientCityStore store, _) = Build(server);

            CommandOutcome outcome = await sender.SendAsync(CommandIntent.PlaceBuilding("house", 0, 0), CancellationToken.None);

            Assert.That((outcome.IsSuccess, outcome.Attempts), Is.EqualTo((true, 3)));
            Assert.That((server.TimesApplied, server.Replays), Is.EqualTo((1, 2)));
            Assert.That(BuildingsIn(store), Has.Count.EqualTo(1));
        }

        /// <summary>
        /// A poll during the wait before the retry has already shown the building and a newer city. The stored answer of the retry is older, so the
        /// store keeps what it holds: the replay does not take the city back, and the command still ends as succeeded.
        /// </summary>
        [Test]
        public async Task APollBeforeTheRetry_IsNotTakenBackByTheOlderStoredAnswer()
        {
            var server = new ReceiptServer(Fixtures.Read("snapshot-command-place-building.json"), lostAnswers: 1) { CityAnswer = PolledCity() };
            ApiClient? api = null;
            ClientCityStore? held = null;
            Func<TimeSpan, CancellationToken, Task> pollWhileWaiting = async (_, token) => SnapshotMapper.Apply(held!, await api!.GetCityAsync(token));
            (CommandSender sender, ClientCityStore store, ApiClient client) = Build(server, pollWhileWaiting);
            (api, held) = (client, store);

            CommandOutcome outcome = await sender.SendAsync(CommandIntent.PlaceBuilding("house", 0, 0), CancellationToken.None);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That((server.TimesApplied, server.Replays), Is.EqualTo((1, 1)));
            Assert.That((store.Snapshot!.StateVersion, store.Snapshot.Clock), Is.EqualTo((8L, 69L)));
            Assert.That(BuildingsIn(store), Has.Count.EqualTo(1));
        }

        /// <summary>
        /// A wire that behaves like the server's idempotency: the first request of a request id is applied and its answer kept; a later one runs
        /// nothing and gets the kept answer. The first <c>lostAnswers</c> commands end with the cancellation of a timeout instead of their answer,
        /// after the server's work is done.
        /// </summary>
        private sealed class ReceiptServer : IApiTransport
        {
            /// <summary>The request id of a command body.</summary>
            private static readonly Regex RequestId = new Regex("\"requestId\":\"([^\"]+)\"");

            /// <summary>The answers kept, by request id.</summary>
            private readonly Dictionary<string, string> _receipts = new Dictionary<string, string>();

            /// <summary>The answer of a command.</summary>
            private readonly string _commandAnswer;

            /// <summary>How many more answers get lost.</summary>
            private int _lostAnswers;

            /// <summary>Creates the wire.</summary>
            /// <param name="commandAnswer">What the first request of a request id answers.</param>
            /// <param name="lostAnswers">How many answers get lost, from the first.</param>
            public ReceiptServer(string commandAnswer, int lostAnswers)
            {
                _commandAnswer = commandAnswer;
                _lostAnswers = lostAnswers;
            }

            /// <summary>What a <c>GET</c> answers.</summary>
            public string CityAnswer { get; set; } = string.Empty;

            /// <summary>The body of every command received, oldest first.</summary>
            public List<string> Commands { get; } = new List<string>();

            /// <summary>How many commands were run.</summary>
            public int TimesApplied { get; private set; }

            /// <summary>How many commands were answered from a receipt.</summary>
            public int Replays { get; private set; }

            /// <inheritdoc/>
            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request, CancellationToken cancellationToken)
            {
                if (request.JsonBody == null)
                {
                    return Task.FromResult(ApiTransportResponse.Http(200, CityAnswer, null));
                }

                Commands.Add(request.JsonBody);
                string requestId = RequestId.Match(request.JsonBody).Groups[1].Value;
                if (_receipts.TryGetValue(requestId, out string answer))
                {
                    Replays++;
                }
                else
                {
                    TimesApplied++;
                    answer = _commandAnswer;
                    _receipts.Add(requestId, answer);
                }

                if (_lostAnswers > 0)
                {
                    _lostAnswers--;
                    throw new OperationCanceledException();
                }

                return Task.FromResult(ApiTransportResponse.Http(200, answer, null));
            }
        }
    }
}
