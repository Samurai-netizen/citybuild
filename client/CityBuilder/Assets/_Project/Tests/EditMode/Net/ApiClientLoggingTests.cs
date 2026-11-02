#nullable enable

using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Tests.EditMode.Core;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// What <see cref="ApiClient"/> logs (M0-57): one Debug line per request that worked, one Warning line with the error code and the
    /// server's trace id per request that did not, and never a token or a body.
    /// </summary>
    public sealed class ApiClientLoggingTests
    {
        /// <summary>A token that must never reach a log line.</summary>
        private const string Token = "secret-token-value";

        /// <summary>A Problem Details answer with a trace id the log line must carry.</summary>
        private const string RefusalAnswer = "{\"status\":409,\"code\":\"CONCURRENCY_CONFLICT\",\"detail\":\"Try again.\",\"traceId\":\"00-4be95066533e95008abe4dc29f10300a-c76e4e06559302a7-00\"}";

        /// <summary>The sink the log writes to in a test.</summary>
        private RecordingLogSink _sink = null!;

        /// <summary>Before each test: a fresh recording sink.</summary>
        [SetUp]
        public void UseARecordingSink()
        {
            _sink = new RecordingLogSink();
        }

        /// <summary>A client with the token set, over <paramref name="transport"/>, logging at <paramref name="level"/> to the recording sink.</summary>
        private ApiClient NewClient(FakeTransport transport, LogLevel level = LogLevel.Debug)
        {
            ApiClient client = new ApiClient("http://localhost:8080", 15, transport, new ClientLogger(level, _sink));
            client.SetAccessToken(Token);
            return client;
        }

        /// <summary>A request that worked is one Debug line with the method, the path, the status and the duration, and nothing of the body or the token.</summary>
        [Test]
        public async Task ASuccessfulRequest_IsOneDebugLine_WithNoTokenAndNoBody()
        {
            string body = Fixtures.Read("snapshot-city-start.json");
            ApiClient client = NewClient(new FakeTransport().Answer(200, body));

            await client.GetCityAsync(CancellationToken.None);

            Assert.That(_sink.Lines, Has.Count.EqualTo(1));
            Assert.That(_sink.Lines[0].Level, Is.EqualTo(LogLevel.Debug));
            Assert.That(_sink.Lines[0].Message, Does.StartWith("Request GET /city -> 200 in "));
            Assert.That(_sink.Lines[0].Message, Does.Not.Contain(Token));
            Assert.That(_sink.Lines[0].Message, Does.Not.Contain("stateVersion"));
        }

        /// <summary>In a release build (level Warning) a request that worked writes nothing.</summary>
        [Test]
        public async Task ASuccessfulRequest_AtTheReleaseLevel_WritesNothing()
        {
            ApiClient client = NewClient(new FakeTransport().Answer(200, Fixtures.Read("snapshot-city-start.json")), ClientLog.DefaultLevelFor(false));

            await client.GetCityAsync(CancellationToken.None);

            Assert.That(_sink.Lines, Is.Empty);
        }

        /// <summary>A refused request is a Warning with the error code, the status and the trace id, even at the release level.</summary>
        [Test]
        public async Task ARefusedRequest_IsAWarning_WithTheCodeAndTheTraceId()
        {
            ApiClient client = NewClient(new FakeTransport().Answer(409, RefusalAnswer), ClientLog.DefaultLevelFor(false));

            await client.GetCityAsync(CancellationToken.None);

            Assert.That(_sink.Lines, Has.Count.EqualTo(1));
            Assert.That(_sink.Lines[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(_sink.Lines[0].Message, Does.Contain("GET /city"));
            Assert.That(_sink.Lines[0].Message, Does.Contain("CONCURRENCY_CONFLICT"));
            Assert.That(_sink.Lines[0].Message, Does.Contain("00-4be95066533e95008abe4dc29f10300a-c76e4e06559302a7-00"));
            Assert.That(_sink.Lines[0].Message, Does.Not.Contain(Token));
        }

        /// <summary>A command's body (its request id and numbers) is not in the line of the request that sent it.</summary>
        [Test]
        public async Task ACommandRequest_LogsThePathNeverTheBody()
        {
            FakeTransport transport = new FakeTransport().Answer(409, RefusalAnswer);
            ApiClient client = NewClient(transport);
            PlaceBuildingRequestDto request = new PlaceBuildingRequestDto(System.Guid.NewGuid(), "hut", 3, 4);

            await client.PlaceBuildingAsync(request, CancellationToken.None);

            Assert.That(transport.Requests[0].JsonBody, Is.Not.Empty);
            Assert.That(_sink.Lines[0].Message, Does.Contain("POST /commands/place-building"));
            Assert.That(_sink.Lines[0].Message, Does.Not.Contain(request.RequestId.ToString()));
        }

        /// <summary>A lost connection is a Warning with <c>CLIENT_NETWORK_ERROR</c>; it has no trace id, so the line has none.</summary>
        [Test]
        public async Task ANetworkFailure_IsAWarning_WithoutATraceId()
        {
            ApiClient client = NewClient(new FakeTransport().Fail("no route"));

            await client.GetCityAsync(CancellationToken.None);

            Assert.That(_sink.Lines[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(_sink.Lines[0].Message, Does.Contain(ApiErrorCodes.ClientNetworkError));
            Assert.That(_sink.Lines[0].Message, Does.Not.Contain("[trace"));
        }

        /// <summary>A timeout is a Warning with <c>CLIENT_TIMEOUT</c>.</summary>
        [Test]
        public async Task ATimeout_IsAWarning()
        {
            ApiClient client = NewClient(new FakeTransport().TimeOut());

            await client.GetCityAsync(CancellationToken.None);

            Assert.That(_sink.Lines[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(_sink.Lines[0].Message, Does.Contain(ApiErrorCodes.ClientTimeout));
        }
    }
}
