using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Contracts.ProblemAssert;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// <c>POST /api/v1/commands/start-research</c> against the contract (<c>core-instructions/05-api-contract.md</c>) on the raw JSON, over a
/// small city in a <see cref="FakeCityStore"/> on the server's content: the real endpoint, pipeline, engine and serializer, no database.
/// It covers what the body may hold and what the answers look like; the rules, their completion and the stored rows are checked on
/// PostgreSQL in <c>StartResearchDatabaseTests</c>. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class StartResearchContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/start-research";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-00000000000d";

    /// <summary>A valid body: Sharpened Axes (250 coins, 1800 seconds, no prerequisites).</summary>
    private const string _startSharpAxes = $$"""{"requestId":"{{_requestId}}","researchId":"sharp_axes"}""";

    /// <summary>The server second the city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6e");

    /// <summary>
    /// Starting answers 200 with the snapshot of the city as it is saved: the request id, the next version, the cost paid (700 − 250 coins),
    /// and the research block with the node running from clock 0 to its 1800 seconds next to the completed trade charter. It is the same
    /// snapshot <c>GET /city</c> shows of that city, and the saved ledger holds the one <c>COST_RESEARCH</c> row.
    /// </summary>
    [Fact]
    public async Task Post_SharpAxes_PaysTheCostAndShowsTheResearchBlock()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _startSharpAxes);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        JsonNode snapshot = response.Json();
        JsonShape.AssertObject(snapshot, "requestId:string stateVersion:int serverTimeUtc:string contentVersion:string city:object settlement:object", "$");
        Assert.Equal((_requestId, 1L), ((string)snapshot["requestId"]!, (long)snapshot["stateVersion"]!));
        Assert.Equal("""{"coins":450,"logs":40,"planks":10}""", snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal(
            """{"activeId":"sharp_axes","startedAt":0,"endsAt":1800,"completed":["trade_charter"]}""",
            snapshot["city"]!["research"]!.ToJsonString());

        Assert.Equal(1, store.SaveCount);
        LedgerEntry cost = Assert.Single(store.Ledger);
        Assert.Equal((LedgerReason.CostResearch, "coins", -250L, 450L, _requestId), (cost.Reason, cost.Item.Value, cost.Delta, cost.BalanceAfter, cost.RefId));

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        Assert.Equal(city["city"]!.ToJsonString(), snapshot["city"]!.ToJsonString());
        Assert.Equal((long)snapshot["stateVersion"]!, (long)city["stateVersion"]!);
    }

    /// <summary>
    /// A retry with the same request id and body, a minute later, answers with the stored response byte for byte and runs nothing: no settle,
    /// no save, the cost paid once.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndRunsNothing()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;
        ApiResponse first = await ApiClient.PostAsync(server, _path, _playerId, _startSharpAxes);
        clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await ApiClient.PostAsync(server, _path, _playerId, _startSharpAxes);

        Assert.Equal((HttpStatusCode.OK, "application/json; charset=utf-8"), (retry.Status, retry.ContentType));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(1, store.SaveCount);
        Assert.Single(store.Ledger);
        Assert.Equal(0, store.Stored(_playerId).State.Clock);
    }

    /// <summary>
    /// The same request id with another node, or on another command (place-building), is <c>IDEMPOTENCY_CONFLICT</c> (409): the data and the
    /// command type are the payload. Nothing runs.
    /// </summary>
    /// <param name="path">The endpoint of the second request.</param>
    /// <param name="body">The second body.</param>
    [Theory]
    [InlineData(_path, $$"""{"requestId":"{{_requestId}}","researchId":"frame_saw"}""")]
    [InlineData("/api/v1/commands/place-building", $$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":0}""")]
    public async Task Post_SameRequestIdWithAnotherPayload_Is409IdempotencyConflict(string path, string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;
        await ApiClient.PostAsync(server, _path, _playerId, _startSharpAxes);

        ApiResponse response = await ApiClient.PostAsync(server, path, _playerId, body);

        AssertProblem(response, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT", _requestId);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>
    /// A rule's refusal is Problem Details with the rule's code and status and the request id; with no time elapsed nothing is saved: an id
    /// of the right form the content doesn't have is <c>UNKNOWN_RESEARCH</c> (422); the completed trade charter is <c>RESEARCH_COMPLETED</c>
    /// (409); Frame Saw before Sharpened Axes is <c>RESEARCH_LOCKED</c> (422); Storehouses (its prerequisite done, 300 coins and 50 logs;
    /// the city has 40 logs) is <c>INSUFFICIENT_RESOURCE</c> (422).
    /// </summary>
    /// <param name="researchId">The research id sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData("no_such_research", (HttpStatusCode)422, "UNKNOWN_RESEARCH")]
    [InlineData("trade_charter", HttpStatusCode.Conflict, "RESEARCH_COMPLETED")]
    [InlineData("frame_saw", (HttpStatusCode)422, "RESEARCH_LOCKED")]
    [InlineData("storehouses", (HttpStatusCode)422, "INSUFFICIENT_RESOURCE")]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(string researchId, HttpStatusCode status, string code)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(_requestId, researchId));

        AssertProblem(response, status, code, _requestId);
        AssertNothingRan(store);
    }

    /// <summary>
    /// With a research running, another start, under a new request id, is <c>RESEARCH_IN_PROGRESS</c> (409) with that request id; only the
    /// first start was saved and paid.
    /// </summary>
    [Fact]
    public async Task Post_WhileAResearchRuns_Is409ResearchInProgress()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;
        await ApiClient.PostAsync(server, _path, _playerId, _startSharpAxes);
        const string secondId = "0199a1b2-c3d4-7e5f-8a9b-00000000000e";

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(secondId, "storehouses"));

        AssertProblem(response, HttpStatusCode.Conflict, "RESEARCH_IN_PROGRESS", secondId);
        Assert.Equal(1, store.SaveCount);
        Assert.Single(store.Ledger);
        Assert.Equal(new ResearchId("sharp_axes"), store.Stored(_playerId).State.ActiveResearch!.Id);
    }

    /// <summary>
    /// A research id that can't be a content id (blank, too long, other characters) is <c>INVALID_ARGUMENT</c> (400) with the request id
    /// before the city is locked (owner directive OD-13): the store was never asked for the city, and nothing is saved.
    /// </summary>
    /// <param name="researchId">The research id sent, as a JSON string's content.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Sharp_Axes")]
    [InlineData("sharp-axes")]
    [InlineData("sharp_axes; DROP TABLE cities")]
    [InlineData("../sharp_axes")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Security_ResearchIdThatCantBeAContentId_Is400BeforeTheCityIsLocked(string researchId)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(_requestId, researchId));

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", _requestId);
        Assert.Equal(0, store.LockCount);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body with a property the contract doesn't have is refused (400) before anything runs, whatever the property claims: the server
    /// never takes a cost, a duration, an end time, completed research, a player or a slot from the client, and never silently ignores one.
    /// </summary>
    /// <param name="extra">The extra property and its value, as JSON.</param>
    [Theory]
    [InlineData("\"cost\":{\"coins\":0}")]
    [InlineData("\"durationSeconds\":1")]
    [InlineData("\"endsAt\":0")]
    [InlineData("\"startedAt\":0")]
    [InlineData("\"completed\":[\"sharp_axes\"]")]
    [InlineData("\"prerequisites\":[]")]
    [InlineData("\"era\":2")]
    [InlineData("\"researchSlots\":2")]
    [InlineData("\"playerId\":\"0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6c\"")]
    [InlineData("\"researchIds\":[\"frame_saw\"]")]
    public async Task Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing(string extra)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _startSharpAxes.Replace("}", "," + extra + "}", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body that isn't the contract's shape is refused (400) before anything runs: a missing field, a wrong type, broken JSON.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData("""{"researchId":"sharp_axes"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}"}""")]
    [InlineData("""{"requestId":"not-a-uuid","researchId":"sharp_axes"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","researchId":null}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","researchId":7}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","researchId":["sharp_axes"]}""")]
    [InlineData($$$"""{"requestId":"{{{_requestId}}}","researchId":{"id":"sharp_axes"}}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","researchId":true}""")]
    [InlineData($$"""[{"requestId":"{{_requestId}}","researchId":"sharp_axes"}]""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","researchId":"sharp_axes" """)]
    public async Task Post_BodyOfTheWrongShape_Is400AndRunsNothing(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body that binds but can't be a command is <c>INVALID_ARGUMENT</c> (400) as Problem Details, before anything runs: no body, JSON
    /// <c>null</c>, the all-zeros request id.
    /// </summary>
    /// <param name="body">The body; <see langword="null"/> sends none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("""{"requestId":"00000000-0000-0000-0000-000000000000","researchId":"sharp_axes"}""")]
    public async Task Post_BodyThatIsNoCommand_Is400InvalidArgument(string? body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId: null);
        AssertNothingRan(store);
    }

    /// <summary>
    /// The OpenAPI document (Development) describes the endpoint for client authors: the request body is <c>StartResearchRequest</c> and the
    /// 200 body is the snapshot, <c>CitySnapshot</c>, although the handler writes the snapshot as text.
    /// </summary>
    [Fact]
    public async Task OpenApi_StartResearch_DescribesTheBodyAndTheSnapshot()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/openapi/v1.json", playerId: null);

        JsonNode post = response.Json()["paths"]![_path]!["post"]!;
        // The body parameter is nullable (no body is INVALID_ARGUMENT), so the schema is oneOf null or the request.
        Assert.Contains("\"#/components/schemas/StartResearchRequest\"", post["requestBody"]!["content"]!["application/json"]!["schema"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.EndsWith("/CitySnapshot", (string)post["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!, StringComparison.Ordinal);
    }

    /// <summary>A body with a request id and a research id.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="researchId">The research id, as a JSON string's content.</param>
    /// <returns>The JSON body.</returns>
    private static string Body(string requestId, string researchId) =>
        $$"""{"requestId":"{{requestId}}","researchId":"{{researchId}}"}""";

    /// <summary>A server copy with a store holding <see cref="City"/>, settled at <see cref="_settledAt"/>, and a fixed clock.</summary>
    /// <param name="now">The server time.</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(DateTimeOffset now) => Serve(new FakeTimeProvider(now));

    /// <summary>A server copy with a store holding <see cref="City"/>, settled at <see cref="_settledAt"/>, and the given clock.</summary>
    /// <param name="clock">The server clock.</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(FakeTimeProvider clock)
    {
        var store = new FakeCityStore();
        store.Add(City(ContentOf(factory)));
        return (store, store.Serve(factory, clock));
    }

    /// <summary>
    /// The city of the tests at clock 0, with the starting stock (700 coins, 40 logs, 10 planks), no buildings, the trade charter completed
    /// (at clock 0) and the research slot free.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content)
    {
        var tradeCharter = new ResearchId("trade_charter");
        var state = new CityState(
            0,
            era: 1,
            nextBuildingId: 1,
            [new(new ItemId("coins"), 700), new(new ItemId("logs"), 40), new(new ItemId("planks"), 10)],
            [],
            [tradeCharter],
            activeResearch: null);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c1a"), _playerId, state, [new(tradeCharter, 0)], _settledAt.ToUnixTimeSeconds(), 0,
            content.ContentVersion);
    }

    /// <summary>Asserts that the store saved nothing: the city is the stored one at version 0, with the slot free and the stock untouched.</summary>
    /// <param name="store">The store.</param>
    private static void AssertNothingRan(FakeCityStore store)
    {
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Ledger);
        CityAggregate city = store.Stored(_playerId);
        Assert.Equal(0, city.StateVersion);
        Assert.Null(city.State.ActiveResearch);
        Assert.Equal(700, city.State.GetQuantity(new ItemId("coins")));
    }
}
