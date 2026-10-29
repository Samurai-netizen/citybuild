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
/// <c>POST /api/v1/commands/upgrade-building</c> against the contract (<c>core-instructions/05-api-contract.md</c>) on the raw JSON, over a
/// small city in a <see cref="FakeCityStore"/> on the server's content: the real endpoint, pipeline, engine and serializer, no database.
/// It covers what the body may hold and what the answers look like; every rule and the stored rows are checked on PostgreSQL in
/// <c>UpgradeBuildingDatabaseTests</c>. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class UpgradeBuildingContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/upgrade-building";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-00000000000b";

    /// <summary>A valid body: upgrade the idle house 1 (level 2: 120 coins, 30 logs, 90 s).</summary>
    private const string _upgradeHouse = $$"""{"requestId":"{{_requestId}}","buildingId":1}""";

    /// <summary>The server second the city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6b");

    /// <summary>
    /// An upgrade answers 200 with the snapshot of the city as it is saved: the request id, the next version, the cost paid, the house
    /// upgrading for its 90 seconds at level 1 (still providing its 4 workers) and both builders busy. It is the same snapshot
    /// <c>GET /city</c> shows of that city, and the saved ledger holds the cost under <c>COST_UPGRADE</c> with the request id as reference.
    /// </summary>
    [Fact]
    public async Task Post_IdleHouse_ReturnsTheSnapshotOfTheSavedCity()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _upgradeHouse);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        JsonNode snapshot = response.Json();
        JsonShape.AssertObject(snapshot, "requestId:string stateVersion:int serverTimeUtc:string contentVersion:string city:object settlement:object", "$");
        Assert.Equal((_requestId, 1L), ((string)snapshot["requestId"]!, (long)snapshot["stateVersion"]!));
        Assert.Equal("""{"coins":580,"logs":10,"planks":10}""", snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal("""{"slots":2,"busy":2}""", snapshot["city"]!["builders"]!.ToJsonString());
        Assert.Equal("""{"provided":4,"assigned":0,"free":4}""", snapshot["city"]!["workers"]!.ToJsonString());
        Assert.Equal(
            """{"id":1,"typeId":"house","x":0,"y":0,"level":1,"assignedWorkers":0,"maxWorkers":0,"phase":"Upgrading",""" +
            """ "phaseStartedAt":0,"phaseEndsAt":90,"cycleLabor":0,"laborRemaining":0,"segmentStartedAt":0,"stallReason":null}""".TrimStart(),
            snapshot["city"]!["buildings"]![0]!.ToJsonString());

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(
            [("coins", -120L, 580L, LedgerReason.CostUpgrade, _requestId), ("logs", -30L, 10L, LedgerReason.CostUpgrade, _requestId)],
            store.Ledger.Select(entry => (entry.Item.Value, entry.Delta, entry.BalanceAfter, entry.Reason, entry.RefId)));

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        Assert.Equal(city["city"]!.ToJsonString(), snapshot["city"]!.ToJsonString());
        Assert.Equal((long)snapshot["stateVersion"]!, (long)city["stateVersion"]!);
    }

    /// <summary>
    /// A retry with the same request id and body, a minute later, answers with the stored response byte for byte and runs nothing: no settle,
    /// no save, the cost is not paid twice.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndRunsNothing()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;
        ApiResponse first = await ApiClient.PostAsync(server, _path, _playerId, _upgradeHouse);
        clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await ApiClient.PostAsync(server, _path, _playerId, _upgradeHouse);

        Assert.Equal((HttpStatusCode.OK, "application/json; charset=utf-8"), (retry.Status, retry.ContentType));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(2, store.Ledger.Count);
    }

    /// <summary>
    /// The same request id with another building is <c>IDEMPOTENCY_CONFLICT</c> (409), and so is the same request id on another command
    /// (place-building): the command type is part of the payload. Nothing runs.
    /// </summary>
    /// <param name="path">The endpoint of the second request.</param>
    /// <param name="body">The second body.</param>
    [Theory]
    [InlineData(_path, $$"""{"requestId":"{{_requestId}}","buildingId":2}""")]
    [InlineData("/api/v1/commands/place-building", $$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":3}""")]
    public async Task Post_SameRequestIdWithAnotherPayload_Is409IdempotencyConflict(string path, string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;
        await ApiClient.PostAsync(server, _path, _playerId, _upgradeHouse);

        ApiResponse response = await ApiClient.PostAsync(server, path, _playerId, body);

        AssertProblem(response, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT", _requestId);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>
    /// A rule's refusal is Problem Details with the rule's code and status and the request id; with no time elapsed nothing is saved. An id
    /// of 1 or more the city doesn't have is <c>BUILDING_NOT_FOUND</c> (404); the woodcutter still under construction is <c>BUILDING_BUSY</c>
    /// (409).
    /// </summary>
    /// <param name="buildingId">The building id sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(3, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND")]
    [InlineData(int.MaxValue, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND")]
    [InlineData(2, HttpStatusCode.Conflict, "BUILDING_BUSY")]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(int buildingId, HttpStatusCode status, string code)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, $$"""{"requestId":"{{_requestId}}","buildingId":{{buildingId}}}""");

        AssertProblem(response, status, code, _requestId);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// An id below 1, which no city has, is <c>INVALID_ARGUMENT</c> (400) with the request id before the city is locked (owner directive
    /// OD-13): the store was never asked for the city, and nothing is saved.
    /// </summary>
    /// <param name="buildingId">The building id sent.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task Security_BuildingIdBelowOne_Is400BeforeTheCityIsLocked(int buildingId)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, $$"""{"requestId":"{{_requestId}}","buildingId":{{buildingId}}}""");

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", _requestId);
        Assert.Equal(0, store.LockCount);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body with a property the contract doesn't have is refused (400) before anything runs, whatever the property claims: the server
    /// never takes a level, a cost, a time, workers, a player or a type from the client, and never silently ignores one either.
    /// </summary>
    /// <param name="extra">The extra property and its value, as JSON.</param>
    [Theory]
    [InlineData("\"level\":2")]
    [InlineData("\"targetLevel\":5")]
    [InlineData("\"upgradeCost\":{\"coins\":0}")]
    [InlineData("\"cost\":{\"coins\":0}")]
    [InlineData("\"upgradeSeconds\":0")]
    [InlineData("\"phaseEndsAt\":0")]
    [InlineData("\"workers\":4")]
    [InlineData("\"maxWorkers\":99")]
    [InlineData("\"playerId\":\"0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6c\"")]
    [InlineData("\"buildingTypeId\":\"house\"")]
    public async Task Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing(string extra)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _upgradeHouse.Replace("}", "," + extra + "}", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>A body that isn't the contract's shape is refused (400) before anything runs: a missing field, a wrong type, broken JSON.</summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData("""{"buildingId":1}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}"}""")]
    [InlineData("""{"requestId":"not-a-uuid","buildingId":1}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":"1"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":1.5}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":null}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":2147483648}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":[1]}""")]
    [InlineData($$"""[{"requestId":"{{_requestId}}","buildingId":1}]""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingId":1""")]
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
    [InlineData("""{"requestId":"00000000-0000-0000-0000-000000000000","buildingId":1}""")]
    public async Task Post_BodyThatIsNoCommand_Is400InvalidArgument(string? body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId: null);
        AssertNothingRan(store);
    }

    /// <summary>
    /// The OpenAPI document (Development) describes the endpoint for client authors: the request body is <c>UpgradeBuildingRequest</c> and
    /// the 200 body is the snapshot, <c>CitySnapshot</c>, although the handler writes the snapshot as text.
    /// </summary>
    [Fact]
    public async Task OpenApi_UpgradeBuilding_DescribesTheBodyAndTheSnapshot()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/openapi/v1.json", playerId: null);

        JsonNode post = response.Json()["paths"]![_path]!["post"]!;
        // The body parameter is nullable (no body is INVALID_ARGUMENT), so the schema is oneOf null or the request.
        Assert.Contains("\"#/components/schemas/UpgradeBuildingRequest\"", post["requestBody"]!["content"]!["application/json"]!["schema"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.EndsWith("/CitySnapshot", (string)post["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!, StringComparison.Ordinal);
    }

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
    /// level 1 (4 workers), and woodcutter 2 at (1,0), constructing until 60, which keeps one builder busy.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content)
    {
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 0, BuildingPhase.Constructing, 0, 60, 0, 0, 0, 0),
        ];
        var state = new CityState(
            0,
            era: 1,
            nextBuildingId: 3,
            [new(new ItemId("coins"), 700), new(new ItemId("logs"), 40), new(new ItemId("planks"), 10)],
            buildings,
            [],
            activeResearch: null);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c18"), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), 0, content.ContentVersion);
    }

    /// <summary>Asserts that the store saved nothing: the city is the stored one at version 0, and no ledger row was written.</summary>
    /// <param name="store">The store.</param>
    private static void AssertNothingRan(FakeCityStore store)
    {
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Ledger);
        CityAggregate city = store.Stored(_playerId);
        Assert.Equal(0, city.StateVersion);
        Assert.Equal(BuildingPhase.Idle, city.State.Buildings.Single(b => b.Id == 1).Phase);
    }
}
