using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Contracts.ProblemAssert;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// <c>POST /api/v1/commands/assign-workers</c> against the contract (<c>core-instructions/05-api-contract.md</c>) on the raw JSON, over a
/// small city in a <see cref="FakeCityStore"/> on the server's content: the real endpoint, pipeline, engine and serializer, no database.
/// It covers what the body may hold and what the answers look like; every rule and the stored rows are checked on PostgreSQL in
/// <c>AssignWorkersDatabaseTests</c>. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class AssignWorkersContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/assign-workers";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-00000000000c";

    /// <summary>A valid body: 2 workers on the idle woodcutter 3 (60 labor a log, level 1 takes at most 2).</summary>
    private const string _staffWoodcutter = $$"""{"requestId":"{{_requestId}}","buildingId":3,"workers":2}""";

    /// <summary>The server second the city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6d");

    /// <summary>
    /// Assigning answers 200 with the snapshot of the city as it is saved: the request id, the next version, the workers block counting the
    /// 2, and the woodcutter producing in the same response: a cycle of 60 labor from clock 0, ending at 30 with 2 workers, no stall. It is
    /// the same snapshot <c>GET /city</c> shows of that city, the stock is untouched, and the saved ledger holds no row: workers cost nothing.
    /// </summary>
    [Fact]
    public async Task Post_IdleWoodcutter_StartsProducingInTheSameResponse()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _staffWoodcutter);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        JsonNode snapshot = response.Json();
        JsonShape.AssertObject(snapshot, "requestId:string stateVersion:int serverTimeUtc:string contentVersion:string city:object settlement:object", "$");
        Assert.Equal((_requestId, 1L), ((string)snapshot["requestId"]!, (long)snapshot["stateVersion"]!));
        Assert.Equal("""{"coins":700,"logs":40,"planks":10}""", snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal("""{"provided":4,"assigned":2,"free":2}""", snapshot["city"]!["workers"]!.ToJsonString());
        Assert.Equal(
            """{"id":3,"typeId":"woodcutter","x":2,"y":0,"level":1,"assignedWorkers":2,"maxWorkers":2,"phase":"Producing",""" +
            """ "phaseStartedAt":0,"phaseEndsAt":30,"cycleLabor":60,"laborRemaining":60,"segmentStartedAt":0,"stallReason":null}""".TrimStart(),
            snapshot["city"]!["buildings"]![2]!.ToJsonString());

        Assert.Equal(1, store.SaveCount);
        Assert.Empty(store.Ledger);

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        Assert.Equal(city["city"]!.ToJsonString(), snapshot["city"]!.ToJsonString());
        Assert.Equal((long)snapshot["stateVersion"]!, (long)city["stateVersion"]!);
    }

    /// <summary>
    /// A retry with the same request id and body, a minute later, answers with the stored response byte for byte and runs nothing: no settle,
    /// no save.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndRunsNothing()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;
        ApiResponse first = await ApiClient.PostAsync(server, _path, _playerId, _staffWoodcutter);
        clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await ApiClient.PostAsync(server, _path, _playerId, _staffWoodcutter);

        Assert.Equal((HttpStatusCode.OK, "application/json; charset=utf-8"), (retry.Status, retry.ContentType));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0, store.Stored(_playerId).State.Clock);
    }

    /// <summary>
    /// The same request id with another count, another building, or on another command (upgrade-building) is <c>IDEMPOTENCY_CONFLICT</c>
    /// (409): the data and the command type are the payload. Nothing runs.
    /// </summary>
    /// <param name="path">The endpoint of the second request.</param>
    /// <param name="body">The second body.</param>
    [Theory]
    [InlineData(_path, $$"""{"requestId":"{{_requestId}}","buildingId":3,"workers":1}""")]
    [InlineData(_path, $$"""{"requestId":"{{_requestId}}","buildingId":4,"workers":2}""")]
    [InlineData("/api/v1/commands/upgrade-building", $$"""{"requestId":"{{_requestId}}","buildingId":3}""")]
    public async Task Post_SameRequestIdWithAnotherPayload_Is409IdempotencyConflict(string path, string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;
        await ApiClient.PostAsync(server, _path, _playerId, _staffWoodcutter);

        ApiResponse response = await ApiClient.PostAsync(server, path, _playerId, body);

        AssertProblem(response, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT", _requestId);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>
    /// A rule's refusal is Problem Details with the rule's code and status and the request id; with no time elapsed nothing is saved: an id
    /// of 1 or more the city doesn't have is <c>BUILDING_NOT_FOUND</c> (404); the house takes no workers, <c>INVALID_ARGUMENT</c> (400); the
    /// woodcutter still under construction is <c>BUILDING_BUSY</c> (409); 3 workers on a level-1 woodcutter (max 2) is <c>WORKER_LIMIT</c>
    /// (422).
    /// </summary>
    /// <param name="buildingId">The building id sent.</param>
    /// <param name="workers">The count sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(5, 1, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND")]
    [InlineData(int.MaxValue, 1, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND")]
    [InlineData(1, 1, HttpStatusCode.BadRequest, "INVALID_ARGUMENT")]
    [InlineData(2, 1, HttpStatusCode.Conflict, "BUILDING_BUSY")]
    [InlineData(3, 3, (HttpStatusCode)422, "WORKER_LIMIT")]
    [InlineData(3, int.MaxValue, (HttpStatusCode)422, "WORKER_LIMIT")]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(int buildingId, int workers, HttpStatusCode status, string code)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(buildingId, workers));

        AssertProblem(response, status, code, _requestId);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// An id below 1 (no city has one) and a negative count (no building can have one) are <c>INVALID_ARGUMENT</c> (400) with the request id
    /// before the city is locked (owner directive OD-13): the store was never asked for the city, and nothing is saved.
    /// </summary>
    /// <param name="buildingId">The building id sent.</param>
    /// <param name="workers">The count sent.</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(int.MinValue, 1)]
    [InlineData(3, -1)]
    [InlineData(3, int.MinValue)]
    public async Task Security_IdBelowOneOrNegativeWorkers_Is400BeforeTheCityIsLocked(int buildingId, int workers)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(buildingId, workers));

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", _requestId);
        Assert.Equal(0, store.LockCount);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body with a property the contract doesn't have is refused (400) before anything runs, whatever the property claims: the server
    /// never takes free workers, a max, a time, labor, a player or a type from the client, and never silently ignores one either.
    /// </summary>
    /// <param name="extra">The extra property and its value, as JSON.</param>
    [Theory]
    [InlineData("\"maxWorkers\":99")]
    [InlineData("\"freeWorkers\":99")]
    [InlineData("\"provided\":99")]
    [InlineData("\"assignedWorkers\":0")]
    [InlineData("\"laborRemaining\":0")]
    [InlineData("\"phaseEndsAt\":0")]
    [InlineData("\"segmentStartedAt\":0")]
    [InlineData("\"level\":2")]
    [InlineData("\"playerId\":\"0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6c\"")]
    [InlineData("\"buildingTypeId\":\"woodcutter\"")]
    public async Task Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing(string extra)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _staffWoodcutter.Replace("}", "," + extra + "}", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body that isn't the contract's shape is refused (400) before anything runs: a missing field (a missing <c>workers</c> must not read
    /// as 0 and pause the building), a wrong type, broken JSON.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData("""{"buildingId":3,"workers":2}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","workers":2}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":3}""")]
    [InlineData("""{"requestId":"not-a-uuid","buildingId":3,"workers":2}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":3,"workers":"2"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":3,"workers":1.5}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":3,"workers":null}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":3,"workers":2147483648}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":3,"workers":[2]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":"3","workers":2}""")]
    [InlineData($$"""[{"requestId":"{{_requestId}}","buildingId":3,"workers":2}]""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":3,"workers":2""")]
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
    [InlineData("""{"requestId":"00000000-0000-0000-0000-000000000000","buildingId":3,"workers":2}""")]
    public async Task Post_BodyThatIsNoCommand_Is400InvalidArgument(string? body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId: null);
        AssertNothingRan(store);
    }

    /// <summary>
    /// The OpenAPI document (Development) describes the endpoint for client authors: the request body is <c>AssignWorkersRequest</c> and the
    /// 200 body is the snapshot, <c>CitySnapshot</c>, although the handler writes the snapshot as text.
    /// </summary>
    [Fact]
    public async Task OpenApi_AssignWorkers_DescribesTheBodyAndTheSnapshot()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/openapi/v1.json", playerId: null);

        JsonNode post = response.Json()["paths"]![_path]!["post"]!;
        // The body parameter is nullable (no body is INVALID_ARGUMENT), so the schema is oneOf null or the request.
        Assert.Contains("\"#/components/schemas/AssignWorkersRequest\"", post["requestBody"]!["content"]!["application/json"]!["schema"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.EndsWith("/CitySnapshot", (string)post["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!, StringComparison.Ordinal);
    }

    /// <summary>A body with the test's request id, the building and the count.</summary>
    /// <param name="buildingId">The building id.</param>
    /// <param name="workers">The count.</param>
    /// <returns>The JSON body.</returns>
    private static string Body(int buildingId, int workers) =>
        $$"""{"requestId":"{{_requestId}}","buildingId":{{buildingId}},"workers":{{workers}}}""";

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
    /// The city of the tests at clock 0, with the starting stock (700 coins, 40 logs, 10 planks): house 1 at (0,0), completed and idle at
    /// level 1 (4 workers, none assigned); woodcutter 2 at (1,0), constructing until 60; woodcutter 3 at (2,0), completed, idle, no workers.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content)
    {
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 0, BuildingPhase.Constructing, 0, 60, 0, 0, 0, 0),
            new(3, new BuildingTypeId("woodcutter"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
        ];
        var state = new CityState(
            0,
            era: 1,
            nextBuildingId: 4,
            [new(new ItemId("coins"), 700), new(new ItemId("logs"), 40), new(new ItemId("planks"), 10)],
            buildings,
            [],
            activeResearch: null);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c19"), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), 0, content.ContentVersion);
    }

    /// <summary>Asserts that the store saved nothing: the city is the stored one at version 0, and woodcutter 3 is still idle without workers.</summary>
    /// <param name="store">The store.</param>
    private static void AssertNothingRan(FakeCityStore store)
    {
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Ledger);
        CityAggregate city = store.Stored(_playerId);
        Assert.Equal(0, city.StateVersion);
        Building woodcutter = city.State.Buildings.Single(b => b.Id == 3);
        Assert.Equal((BuildingPhase.Idle, 0), (woodcutter.Phase, woodcutter.AssignedWorkers));
    }
}
