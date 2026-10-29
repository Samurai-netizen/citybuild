using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.StartResearch;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// <c>POST /api/v1/commands/start-research</c> end to end on PostgreSQL over HTTP: the server's real content (Trade Charter: 150 coins,
/// 600 s, unlocks the trading post; Sharpened Axes: 250 coins, 1800 s, the woodcutter's labor −200 ‰; Frame Saw: needs Sharpened Axes;
/// Storehouses: 300 coins and 50 logs, 2700 s, needs Trade Charter, +250 logs and planks of storage; a woodcutter makes a log per 60 labor),
/// endpoint, pipeline and repositories, the server clock a <c>FakeTimeProvider</c> at <see cref="PostgresApiFactory.Start"/>. Each test
/// stores a city built by hand at clock 0 with no research (<see cref="HandBuiltCities.StoreCityAsync"/>) and checks what was stored, not
/// only the status: the city row's research slot, the completed research rows, the stock, the ledger and the receipts. A research completes
/// in the settle of a later request (<c>04</c> sections 5 and 6), so the completion tests move the clock and send another request.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class StartResearchDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/start-research";

    /// <summary>The request id most tests send.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000003c1");

    /// <summary>
    /// Starting Sharpened Axes at clock 0 pays its 250 coins up front and runs it from 0 to 1800: the snapshot's research block shows it,
    /// the city row stores it, the ledger holds one <c>COST_RESEARCH</c> row with the request id as reference, the stock is 750 coins, and the
    /// receipt holds the response and the payload's hash. Nothing is completed yet.
    /// </summary>
    [Fact]
    public async Task Post_SharpAxes_PaysTheCostAndRunsTheResearch()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));
        long versionBefore = (await LoadAsync(Factory, player)).StateVersion;

        ApiResponse response = await StartAsync(player, _requestId, "sharp_axes");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode snapshot = response.Json();
        Assert.Equal(versionBefore + 1, (long)snapshot["stateVersion"]!);
        Assert.Equal("""{"activeId":"sharp_axes","startedAt":0,"endsAt":1800,"completed":[]}""", snapshot["city"]!["research"]!.ToJsonString());
        Assert.Equal("""{"coins":750,"logs":100,"planks":50}""", snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal("sharp_axes 0-1800, completed: ", await ResearchAsync(city));
        LedgerEntryRow cost = Assert.Single(await LedgerAsync(Database, city));
        Assert.Equal($"coins -250 750 COST_RESEARCH command {_requestId}", Line(cost));
        Assert.Equal("coins 750, logs 100, planks 50", await StockAsync(Database, city));
        CommandReceiptRow receipt = Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal((_requestId, StartResearchCommand.Type, response.Body), (receipt.RequestId, receipt.CommandType, receipt.ResponseJson));
        Assert.Equal(CommandPayload.Hash(new StartResearchCommand(_requestId, "sharp_axes")), receipt.PayloadHash);
    }

    /// <summary>
    /// Trade Charter, started at 0, ends at 600, and its effect applies in the settle of the first request at that second. At 599 the
    /// trading post is still <c>BUILDING_LOCKED</c> (422); at 600 the place request's settle completes the research before the mutation
    /// (its settlement block names it, the slot is free, the completion is stored at 600) and the trading post is placed, constructing from
    /// 600 to 780, paid with 200 coins and 20 planks.
    /// </summary>
    [Fact]
    public async Task Post_TradeCharter_MakesTheTradingPostPlaceableWhenItCompletes()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(player, _requestId, "trade_charter")).Status);

        Factory.Clock.Advance(TimeSpan.FromSeconds(599));
        ApiResponse early = await PlaceTradingPostAsync(player);
        Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        ApiResponse placed = await PlaceTradingPostAsync(player);

        Assert.Equal(((HttpStatusCode)422, "BUILDING_LOCKED"), (early.Status, (string)early.Json()["code"]!));
        Assert.Equal(HttpStatusCode.OK, placed.Status);
        JsonNode snapshot = placed.Json();
        Assert.Equal("""["trade_charter"]""", snapshot["settlement"]!["completedResearch"]!.ToJsonString());
        Assert.Equal("""{"activeId":null,"startedAt":null,"endsAt":null,"completed":["trade_charter"]}""", snapshot["city"]!["research"]!.ToJsonString());
        Assert.Equal("none, completed: trade_charter 600", await ResearchAsync(city));
        Assert.Equal("2 trading_post L1 Constructing 600-780 target 0 workers 0", await BuildingAsync(Database, city, 2));
        Assert.Equal("coins 650, logs 100, planks 30", await StockAsync(Database, city));
    }

    /// <summary>
    /// Sharpened Axes shortens the woodcutter's cycles that start once it is completed; the running one keeps its labor (<c>04</c> section
    /// 5). A woodcutter produces with 2 workers from 0: 60 labor, a cycle every 30 s. Started at 10, the research ends at 1810. At 1810 the
    /// cycle that began at 1800 still runs on 60 labor (to 1830); the cycle from 1830 needs ceil(60 × 800 / 1000) = 48 labor, so it ends at
    /// 1830 + 48 / 2 = 1854. By 1830, 61 cycles have ended: 161 logs.
    /// </summary>
    [Fact]
    public async Task Post_SharpAxes_ShortensTheNextWoodcutterCycleWhenItCompletes()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory, "researcher", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2));
        Factory.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(player, _requestId, "sharp_axes")).Status);

        Factory.Clock.Advance(TimeSpan.FromSeconds(1800));
        JsonNode completed = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(20));
        JsonNode next = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal("""["sharp_axes"]""", completed["settlement"]!["completedResearch"]!.ToJsonString());
        Assert.Equal("Producing 1800-1830 labor 60/60", Cycle(completed, 2));
        Assert.Equal("Producing 1830-1854 labor 48/48", Cycle(next, 2));
        Assert.Equal("none, completed: sharp_axes 1810", await ResearchAsync(city));
        Assert.Equal("coins 750, logs 161, planks 50", await StockAsync(Database, city));
    }

    /// <summary>
    /// Once a prerequisite is completed its follower can start, and every item of its cost is paid: Storehouses after Trade Charter (600 s)
    /// pays 300 coins and 50 logs as two <c>COST_RESEARCH</c> rows with the request id as reference. When it completes at 600 + 2700 = 3300,
    /// the storage caps of logs and planks rise from 500 to 750.
    /// </summary>
    [Fact]
    public async Task Post_AfterItsPrerequisite_PaysEveryItemAndRaisesTheCapsWhenItCompletes()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));
        await StartAsync(player, Guid.CreateVersion7(), "trade_charter");
        Factory.Clock.Advance(TimeSpan.FromSeconds(600));

        ApiResponse response = await StartAsync(player, _requestId, "storehouses");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(
            """{"activeId":"storehouses","startedAt":600,"endsAt":3300,"completed":["trade_charter"]}""",
            response.Json()["city"]!["research"]!.ToJsonString());
        Assert.Equal("""{"logs":500,"planks":500}""", response.Json()["city"]!["storageCaps"]!.ToJsonString());
        Assert.Equal(
            ["coins -300 550 COST_RESEARCH command " + _requestId, "logs -50 50 COST_RESEARCH command " + _requestId],
            (await LedgerAsync(Database, city)).Where(row => row.RefId == _requestId.ToString()).Select(Line).Order(StringComparer.Ordinal));
        Assert.Equal("coins 550, logs 50, planks 50", await StockAsync(Database, city));

        Factory.Clock.Advance(TimeSpan.FromSeconds(2700));
        JsonNode completed = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        Assert.Equal("""{"logs":750,"planks":750}""", completed["city"]!["storageCaps"]!.ToJsonString());
        Assert.Equal("none, completed: storehouses 3300, trade_charter 600", await ResearchAsync(city));
    }

    /// <summary>A well-formed id the content doesn't have is <c>UNKNOWN_RESEARCH</c> (422) with the request id, and nothing is stored.</summary>
    [Fact]
    public async Task Post_UnknownNode_Is422UnknownResearch()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));

        await AssertRefusedAsync(player, city, "no_such_research", (HttpStatusCode)422, "UNKNOWN_RESEARCH");
    }

    /// <summary>
    /// A node whose prerequisite isn't completed is <c>RESEARCH_LOCKED</c> (422) and nothing is stored, although the city can pay: Frame Saw
    /// before Sharpened Axes, Storehouses before Trade Charter.
    /// </summary>
    /// <param name="researchId">The node sent.</param>
    [Theory]
    [InlineData("frame_saw")]
    [InlineData("storehouses")]
    public async Task Post_PrerequisiteMissing_Is422ResearchLocked(string researchId)
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));

        await AssertRefusedAsync(player, city, researchId, (HttpStatusCode)422, "RESEARCH_LOCKED");
    }

    /// <summary>
    /// With Sharpened Axes running, another node (Trade Charter, unlocked and affordable) is <c>RESEARCH_IN_PROGRESS</c> (409): the one slot
    /// is busy. Nothing more is stored: the slot keeps Sharpened Axes, the ledger its one cost row.
    /// </summary>
    [Fact]
    public async Task Post_WhileAResearchRuns_Is409ResearchInProgress()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));
        await StartAsync(player, Guid.CreateVersion7(), "sharp_axes");

        await AssertRefusedAsync(player, city, "trade_charter", HttpStatusCode.Conflict, "RESEARCH_IN_PROGRESS");

        Assert.Equal("sharp_axes 0-1800, completed: ", await ResearchAsync(city));
    }

    /// <summary>
    /// A completed node is <c>RESEARCH_COMPLETED</c> (409): Trade Charter again after it completed at 600 (the <c>GET /city</c> at 600
    /// settled the completion first), and nothing is stored, no second cost paid.
    /// </summary>
    [Fact]
    public async Task Post_CompletedNode_Is409ResearchCompleted()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));
        await StartAsync(player, Guid.CreateVersion7(), "trade_charter");
        Factory.Clock.Advance(TimeSpan.FromSeconds(600));
        await ApiClient.GetAsync(Factory, "/api/v1/city", player);

        await AssertRefusedAsync(player, city, "trade_charter", HttpStatusCode.Conflict, "RESEARCH_COMPLETED");

        Assert.Equal("coins 850, logs 100, planks 50", await StockAsync(Database, city));
    }

    /// <summary>
    /// A cost the stock doesn't cover is <c>INSUFFICIENT_RESOURCE</c> (422) and nothing is stored: 249 coins for Sharpened Axes' 250, and
    /// Storehouses' 50 logs with 49 in stock (its other item, 300 coins, covered; Trade Charter completed first).
    /// </summary>
    /// <param name="coins">The coins in stock.</param>
    /// <param name="logs">The logs in stock.</param>
    /// <param name="researchId">The node sent.</param>
    [Theory]
    [InlineData(249, 100, "sharp_axes")]
    [InlineData(1000, 49, "storehouses")]
    public async Task Post_CostNotCovered_Is422InsufficientResource(long coins, long logs, string researchId)
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", coins, logs, 50, House(1));
        if (researchId == "storehouses")
        {
            await StartAsync(player, Guid.CreateVersion7(), "trade_charter");
            Factory.Clock.Advance(TimeSpan.FromSeconds(600));
            await ApiClient.GetAsync(Factory, "/api/v1/city", player);
        }

        await AssertRefusedAsync(player, city, researchId, (HttpStatusCode)422, "INSUFFICIENT_RESOURCE");
    }

    /// <summary>
    /// A research id that can't be a content id is <c>INVALID_ARGUMENT</c> (400) on the real server before the city is locked (OD-13): five
    /// minutes have passed, and still nothing is stored, not even the settle.
    /// </summary>
    /// <param name="researchId">The research id sent, as a JSON string's content.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Sharp_Axes")]
    [InlineData("sharp_axes'; DROP TABLE cities; --")]
    public async Task Security_ResearchIdThatCantBeAContentId_Is400AndStoresNothing(string researchId)
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "attacker", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await StartAsync(player, _requestId, researchId);

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_ARGUMENT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A body with a field the contract doesn't have (a duration of 1 second) is refused (400) on the real server too, and nothing is stored,
    /// although time has passed.
    /// </summary>
    [Fact]
    public async Task Security_PostWithADurationField_Is400AndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "attacker", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(
            Factory, _path, player, $$"""{"requestId":"{{_requestId}}","researchId":"sharp_axes","durationSeconds":1}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>A retry with the same request id and body a minute later answers with the stored bytes and writes nothing: one cost, one receipt.</summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));
        ApiResponse first = await StartAsync(player, _requestId, "sharp_axes");
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await StartAsync(player, _requestId, "sharp_axes");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.Status, retry.Status));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>The same request id with another node is <c>IDEMPOTENCY_CONFLICT</c> (409) and writes nothing.</summary>
    [Fact]
    public async Task Post_SameRequestIdWithAnotherNode_Is409AndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));
        await StartAsync(player, _requestId, "sharp_axes");
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await StartAsync(player, _requestId, "trade_charter");

        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Two start-research requests at once with different request ids, for two nodes that are both unlocked and affordable (decision 2):
    /// the city's lock puts them one after the other, so exactly one succeeds and the other is <c>RESEARCH_IN_PROGRESS</c>. The slot holds the
    /// winner, only the winner's cost is paid (one ledger row), and there is one receipt.
    /// </summary>
    [Fact]
    public async Task Security_TwoStartsAtOnce_StartExactlyOneResearch()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "researcher", 1000, 100, 50, House(1));
        (string Node, Guid RequestId, long Cost, long Seconds)[] starts =
            [("sharp_axes", Guid.CreateVersion7(), 250, 1800), ("trade_charter", Guid.CreateVersion7(), 150, 600)];

        ApiResponse[] responses = await Task.WhenAll(starts.Select(start => StartAsync(player, start.RequestId, start.Node)));

        Assert.Equal(
            ["OK", "RESEARCH_IN_PROGRESS"],
            responses.Select(response => response.Status == HttpStatusCode.OK ? "OK" : (string)response.Json()["code"]!).Order(StringComparer.Ordinal));
        (string node, Guid requestId, long cost, long seconds) = starts[Array.FindIndex(responses, response => response.Status == HttpStatusCode.OK)];
        Assert.Equal($"{node} 0-{seconds}, completed: ", await ResearchAsync(city));
        Assert.Equal($"coins {-cost} {1000 - cost} COST_RESEARCH command {requestId}", Line(Assert.Single(await LedgerAsync(Database, city))));
        Assert.Equal($"coins {1000 - cost}, logs 100, planks 50", await StockAsync(Database, city));
        Assert.Equal(requestId, Assert.Single(await ReceiptsAsync(Database, player)).RequestId);
    }

    /// <summary>
    /// Sends a start and asserts the refusal: the status, the code and the request id, and the city stored exactly as before (no time has
    /// elapsed since the last request, so not even a settlement is written).
    /// </summary>
    /// <param name="player">The acting player.</param>
    /// <param name="city">Their city.</param>
    /// <param name="researchId">The node sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    private async Task AssertRefusedAsync(Guid player, Guid city, string researchId, HttpStatusCode status, string code)
    {
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await StartAsync(player, _requestId, researchId);

        Assert.Equal(status, response.Status);
        JsonNode problem = response.Json();
        Assert.Equal((code, _requestId.ToString()), ((string)problem["code"]!, (string)problem["requestId"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>Sends a start over HTTP with the player's token.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="researchId">The node, as a JSON string's content.</param>
    /// <returns>The response.</returns>
    private Task<ApiResponse> StartAsync(Guid player, Guid requestId, string researchId) =>
        ApiClient.PostAsync(Factory, _path, player, $$"""{"requestId":"{{requestId}}","researchId":"{{researchId}}"}""");

    /// <summary>Places a trading post at (3,3), a free tile of every city of the tests, with a new request id.</summary>
    /// <param name="player">The acting player.</param>
    /// <returns>The response.</returns>
    private Task<ApiResponse> PlaceTradingPostAsync(Guid player) => ApiClient.PostAsync(
        Factory, "/api/v1/commands/place-building", player, $$"""{"requestId":"{{Guid.CreateVersion7()}}","buildingTypeId":"trading_post","x":3,"y":3}""");

    /// <summary>
    /// The stored research of a city as text, read by a context of the test's own: the city row's running research (id, start, end) or
    /// <c>none</c>, then the completed rows with their completion second in ordinal id order.
    /// </summary>
    /// <param name="city">The city.</param>
    /// <returns>For example <c>sharp_axes 0-1800, completed: trade_charter 600</c>.</returns>
    private async Task<string> ResearchAsync(Guid city)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        CityRow row = await db.Cities.AsNoTracking().SingleAsync(c => c.Id == city, TestContext.Current.CancellationToken);
        List<CityResearchRow> completed = await db.CityResearch.AsNoTracking().Where(r => r.CityId == city).ToListAsync(TestContext.Current.CancellationToken);
        string running = row.ResearchId is null ? "none" : $"{row.ResearchId} {row.ResearchStartedAt}-{row.ResearchEndsAt}";
        return $"{running}, completed: " + string.Join(
            ", ", completed.OrderBy(r => r.ResearchId, StringComparer.Ordinal).Select(r => $"{r.ResearchId} {r.CompletedAt}"));
    }

    /// <summary>A building's cycle in a snapshot as one line: phase, the phase's start and end, labor remaining of the cycle's labor.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="buildingId">The building's id.</param>
    /// <returns>For example <c>Producing 1830-1854 labor 48/48</c>.</returns>
    private static string Cycle(JsonNode snapshot, int buildingId)
    {
        JsonNode b = snapshot["city"]!["buildings"]!.AsArray().Single(building => (int)building!["id"]! == buildingId)!;
        return $"{b["phase"]} {b["phaseStartedAt"]}-{b["phaseEndsAt"]} labor {b["laborRemaining"]}/{b["cycleLabor"]}";
    }
}
