using System.Net;
using CityBuilder.IntegrationTests.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Logging.RequestLogTests;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Logging;

/// <summary>
/// The log lines of commands and settlements (core-instructions/07-security-model.md, logging policy; docs/api/README.md): a command
/// accepted, rejected with its code, replayed or in a concurrency conflict, each with the command, its request id, the player and the
/// duration; a settlement line for every settle, from <c>GET /city</c> or a command, with the elapsed and production seconds, the events and
/// the research completed; the warning for a clock behind a city. Over HTTP on the real endpoints and pipeline with a
/// <see cref="FakeCityStore"/>. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its store, clock and logger.</param>
public sealed class CommandLogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The player of the tests.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>The request id of the tests' commands.</summary>
    private static readonly Guid _requestId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a60");

    /// <summary>The instant the fake clock starts at, which is also when the cities were last settled.</summary>
    private static readonly DateTimeOffset _start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A woodcutter on a free tile of the starter city: accepted.</summary>
    private static readonly string _placeWoodcutter = $$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":3}""";

    /// <summary>The server's clock in a test; it starts at <see cref="_start"/>.</summary>
    private readonly FakeTimeProvider _clock = new(_start);

    /// <summary>The server of a test with the player's city in a store.</summary>
    /// <param name="logs">Collects what the server logs.</param>
    /// <param name="store">The store; <see langword="null"/> for one with the player's starter city settled at <see cref="_start"/>.</param>
    /// <returns>The server; the caller disposes it.</returns>
    private WebApplicationFactory<Program> Serve(CapturingLogger logs, FakeCityStore? store = null)
    {
        if (store is null)
        {
            store = new FakeCityStore();
            store.Add(Starter(ContentOf(factory), Guid.NewGuid(), _playerId, _start.ToUnixTimeSeconds()));
        }

        return logs.AddTo(store.Serve(factory, _clock));
    }

    /// <summary>
    /// An accepted command writes the settlement line (its trigger is the command type) and then the command line with the command, the
    /// request id, the player and a duration, both with the request's trace id; the body's values are in neither.
    /// </summary>
    [Fact]
    public async Task Post_Accepted_LogsTheSettlementThenTheCommand()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);
        _clock.Advance(TimeSpan.FromSeconds(100));

        ApiResponse response = await ApiClient.PostAsync(server, "/api/v1/commands/place-building", _playerId, _placeWoodcutter);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        CapturingLogger.Entry settled = Assert.Single(logs.Events(6000));
        CapturingLogger.Entry accepted = Assert.Single(logs.Events(5000));
        Assert.True(logs.Entries.ToList().IndexOf(settled) < logs.Entries.ToList().IndexOf(accepted));
        Assert.Equal(("place-building", (object?)100L, (object?)_playerId), (settled.Values["Trigger"], settled.Values["ElapsedSeconds"], settled.Values["PlayerId"]));
        Assert.Equal(LogLevel.Information, accepted.Level);
        Assert.Equal(("place-building", (object?)_requestId, (object?)_playerId), (accepted.Values["Command"], accepted.Values["RequestId"], accepted.Values["PlayerId"]));
        Assert.True((double)accepted.Values["DurationMs"]! >= 0);
        Assert.NotNull(accepted.Scope("TraceId"));
        Assert.Equal(accepted.Scope("TraceId"), settled.Scope("TraceId"));
        Assert.All(new[] { settled, accepted }, entry => Assert.DoesNotContain("woodcutter", entry.AllText(), StringComparison.Ordinal));
    }

    /// <summary>A rule refusal writes the settlement line (the settle is committed) and the command line with the error code.</summary>
    [Fact]
    public async Task Post_RuleRefuses_LogsTheSettlementAndTheCode()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);

        ApiResponse response = await ApiClient.PostAsync(
            server, "/api/v1/commands/start-research", _playerId, $$"""{"requestId":"{{_requestId}}","researchId":"no_such_node"}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.Single(logs.Events(6000));
        CapturingLogger.Entry rejected = Assert.Single(logs.Events(5001));
        Assert.Equal(("start-research", (object?)"UNKNOWN_RESEARCH", (object?)_requestId), (rejected.Values["Command"], rejected.Values["Code"], rejected.Values["RequestId"]));
        Assert.Empty(logs.Events(5000));
    }

    /// <summary>A request id of all zeros is refused before the settle: the code <c>INVALID_ARGUMENT</c> and no settlement line.</summary>
    [Fact]
    public async Task Post_InvalidArgument_LogsTheCodeAndNoSettlement()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);

        await ApiClient.PostAsync(
            server, "/api/v1/commands/place-building", _playerId, $$"""{"requestId":"{{Guid.Empty}}","buildingTypeId":"woodcutter","x":2,"y":3}""");

        Assert.Equal("INVALID_ARGUMENT", Assert.Single(logs.Events(5001)).Values["Code"]);
        Assert.Empty(logs.Events(6000));
    }

    /// <summary>
    /// The same request again is an idempotent replay: a replay line and no settlement line; the same request id with another body is
    /// <c>IDEMPOTENCY_CONFLICT</c>.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_LogsTheReplayAndTheConflict()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);
        await ApiClient.PostAsync(server, "/api/v1/commands/place-building", _playerId, _placeWoodcutter);
        _clock.Advance(TimeSpan.FromMinutes(1));

        await ApiClient.PostAsync(server, "/api/v1/commands/place-building", _playerId, _placeWoodcutter);
        await ApiClient.PostAsync(
            server, "/api/v1/commands/place-building", _playerId, $$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":3,"y":3}""");

        CapturingLogger.Entry replayed = Assert.Single(logs.Events(5002));
        Assert.Equal(("place-building", (object?)_requestId, (object?)_playerId), (replayed.Values["Command"], replayed.Values["RequestId"], replayed.Values["PlayerId"]));
        Assert.Equal("IDEMPOTENCY_CONFLICT", Assert.Single(logs.Events(5001)).Values["Code"]);
        Assert.Single(logs.Events(6000));
    }

    /// <summary>A command that can't get its city's lock logs the conflict as a warning, with its request id; the handler logs nothing more.</summary>
    [Fact]
    public async Task Post_ConcurrencyConflict_LogsAWarningWithTheRequestId()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs, new FakeCityStore { ConflictOnLock = true });

        ApiResponse response = await ApiClient.PostAsync(server, "/api/v1/commands/place-building", _playerId, _placeWoodcutter);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        CapturingLogger.Entry conflict = Assert.Single(logs.Events(5003));
        Assert.Equal((LogLevel.Warning, (object?)_requestId, (object?)_playerId), (conflict.Level, conflict.Values["RequestId"], conflict.Values["PlayerId"]));
        Assert.Empty(logs.Events(3002));
    }

    /// <summary>
    /// <c>GET /city</c> logs its settlement with the trigger <c>city</c>: 100 seconds elapsed, all of them production seconds, no events, no
    /// research.
    /// </summary>
    [Fact]
    public async Task GetCity_After100Seconds_LogsTheSettlementsSize()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);
        _clock.Advance(TimeSpan.FromSeconds(100));

        await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        CapturingLogger.Entry settled = Assert.Single(logs.Events(6000));
        Assert.Equal(LogLevel.Information, settled.Level);
        Assert.Equal(
            ("city", (object?)100L, (object?)100L, (object?)0, (object?)string.Empty),
            (settled.Values["Trigger"], settled.Values["ElapsedSeconds"], settled.Values["ProductionSeconds"], settled.Values["Events"], settled.Values["ResearchCompleted"]));
        Assert.True((double)settled.Values["DurationMs"]! >= 0);
        Assert.Empty(logs.Events(6001));
    }

    /// <summary>
    /// A settle that completes research names it and counts it as an event; after 9 hours away only the 8-hour cap is production time
    /// (<c>04</c> §2).
    /// </summary>
    [Fact]
    public async Task GetCity_ResearchCompletesAfterNineHours_LogsItAndTheCappedProduction()
    {
        var logs = new CapturingLogger();
        var store = new FakeCityStore();
        store.Add(WithFrameSawRunning(ContentOf(factory), Guid.NewGuid(), _playerId, _start.ToUnixTimeSeconds()));
        await using WebApplicationFactory<Program> server = Serve(logs, store);
        _clock.Advance(TimeSpan.FromHours(9));

        await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        CapturingLogger.Entry settled = Assert.Single(logs.Events(6000));
        Assert.Equal(
            ((object?)32_400L, (object?)28_800L, (object?)1, (object?)"frame_saw"),
            (settled.Values["ElapsedSeconds"], settled.Values["ProductionSeconds"], settled.Values["Events"], settled.Values["ResearchCompleted"]));
    }

    /// <summary>
    /// A server clock behind the city's last settled second (it was set back a minute) is a warning with how far behind it is (<c>04</c> §1),
    /// beside the settlement line, which shows nothing elapsed.
    /// </summary>
    [Fact]
    public async Task GetCity_ClockBehindTheCity_LogsAWarningWithTheSeconds()
    {
        var logs = new CapturingLogger();
        var store = new FakeCityStore();
        store.Add(Starter(ContentOf(factory), Guid.NewGuid(), _playerId, _start.AddSeconds(60).ToUnixTimeSeconds()));
        await using WebApplicationFactory<Program> server = Serve(logs, store);

        await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        CapturingLogger.Entry behind = Assert.Single(logs.Events(6001));
        Assert.Equal((LogLevel.Warning, (object?)60L, (object?)_playerId), (behind.Level, behind.Values["SecondsBehind"], behind.Values["PlayerId"]));
        Assert.Equal(0L, Assert.Single(logs.Events(6000)).Values["ElapsedSeconds"]);
    }

    /// <summary>No line of a command's request holds its body: not the type id it names, nor its property names (the token: <c>RequestLogTests</c>).</summary>
    [Fact]
    public async Task Security_CommandRequest_LogsNoBody()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);
        string body = $$"""{"requestId":"{{_requestId}}","buildingTypeId":"zz_secret_kind","x":2,"y":3}""";

        await ApiClient.PostAsync(server, "/api/v1/commands/place-building", _playerId, body);

        Assert.NotEmpty(logs.Events(5001));
        AssertNowhere(logs, "zz_secret_kind", "buildingTypeId", "\"x\":2");
    }
}
