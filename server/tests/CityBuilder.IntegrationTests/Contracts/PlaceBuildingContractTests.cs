using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.Domain.Ledger;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Contracts.ProblemAssert;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// <c>POST /api/v1/commands/place-building</c> against the contract (<c>core-instructions/05-api-contract.md</c>) on the raw JSON, over a
/// starter city in a <see cref="FakeCityStore"/> on the server's content: the real endpoint, pipeline, engine and serializer, no database.
/// It covers what the body may hold and what the answers look like; every rule and the stored rows are checked on PostgreSQL in
/// <c>PlaceBuildingDatabaseTests</c>. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class PlaceBuildingContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/place-building";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-00000000000a";

    /// <summary>A valid body: a woodcutter (80 coins, 60 s) on a free tile of the starter city.</summary>
    private const string _placeWoodcutter = $$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":3}""";

    /// <summary>The server second the starter city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the starter city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>
    /// A placement answers 200 with the snapshot of the city as it is saved: the request id, the next version, the cost paid, the building
    /// constructing for the type's 60 seconds and a builder busy. It is the same snapshot <c>GET /city</c> shows of that city, and the
    /// saved ledger holds the cost under <c>COST_PLACE</c> with the request id as its reference.
    /// </summary>
    [Fact]
    public async Task Post_Woodcutter_ReturnsTheSnapshotOfTheSavedCity()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _placeWoodcutter);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        JsonNode snapshot = response.Json();
        JsonShape.AssertObject(snapshot, "requestId:string stateVersion:int serverTimeUtc:string contentVersion:string city:object settlement:object", "$");
        Assert.Equal((_requestId, 1L), ((string)snapshot["requestId"]!, (long)snapshot["stateVersion"]!));
        Assert.Equal("""{"coins":620,"logs":40,"planks":10}""", snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal("""{"slots":2,"busy":1}""", snapshot["city"]!["builders"]!.ToJsonString());
        Assert.Equal(
            """[{"id":1,"typeId":"woodcutter","x":2,"y":3,"level":1,"assignedWorkers":0,"maxWorkers":2,"phase":"Constructing",""" +
            """ "phaseStartedAt":0,"phaseEndsAt":60,"cycleLabor":0,"laborRemaining":0,"segmentStartedAt":0,"stallReason":null}]""".TrimStart(),
            snapshot["city"]!["buildings"]!.ToJsonString());

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, store.Stored(_playerId).StateVersion);
        LedgerEntry cost = Assert.Single(store.Ledger);
        Assert.Equal(("coins", -80L, 620L, LedgerReason.CostPlace, _requestId), (cost.Item.Value, cost.Delta, cost.BalanceAfter, cost.Reason, cost.RefId));

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        Assert.Equal(city["city"]!.ToJsonString(), snapshot["city"]!.ToJsonString());
        Assert.Equal((long)snapshot["stateVersion"]!, (long)city["stateVersion"]!);
    }

    /// <summary>
    /// A retry with the same request id and body, a minute later, answers with the stored response byte for byte and runs nothing: no settle,
    /// no save, the building is not placed twice.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndRunsNothing()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;
        ApiResponse first = await ApiClient.PostAsync(server, _path, _playerId, _placeWoodcutter);
        clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await ApiClient.PostAsync(server, _path, _playerId, _placeWoodcutter);

        Assert.Equal((HttpStatusCode.OK, "application/json; charset=utf-8"), (retry.Status, retry.ContentType));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(1, store.SaveCount);
        Assert.Single(store.Stored(_playerId).State.Buildings);
    }

    /// <summary>
    /// The same request id with another tile is <c>IDEMPOTENCY_CONFLICT</c> (409) as Problem Details with exactly the contract's members
    /// (<c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>, <c>code</c>, <c>requestId</c>, <c>traceId</c>), and nothing runs.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestIdWithAnotherTile_Is409IdempotencyConflict()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;
        await ApiClient.PostAsync(server, _path, _playerId, _placeWoodcutter);

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _placeWoodcutter.Replace("\"x\":2", "\"x\":4", StringComparison.Ordinal));

        AssertProblem(response, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT", _requestId);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>
    /// A rule's refusal is Problem Details with the rule's code and status and the request id; with no time elapsed nothing is saved.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"castle","x":2,"y":3}""", HttpStatusCode.UnprocessableEntity, "UNKNOWN_BUILDING_TYPE")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"trading_post","x":2,"y":3}""", HttpStatusCode.UnprocessableEntity, "BUILDING_LOCKED")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":8,"y":3}""", HttpStatusCode.UnprocessableEntity, "OUT_OF_BOUNDS")]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(string body, HttpStatusCode status, string code)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        AssertProblem(response, status, code, _requestId);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// A body with a property the contract doesn't have is refused (400) before anything runs, whatever the property claims: the server
    /// never takes a cost, a level, a time, workers, a player or a building id from the client, and never silently ignores one either.
    /// </summary>
    /// <param name="extra">The extra property and its value, as JSON.</param>
    [Theory]
    [InlineData("\"cost\":{\"coins\":0}")]
    [InlineData("\"coins\":100000")]
    [InlineData("\"level\":2")]
    [InlineData("\"buildSeconds\":0")]
    [InlineData("\"phaseEndsAt\":0")]
    [InlineData("\"workers\":2")]
    [InlineData("\"assignedWorkers\":2")]
    [InlineData("\"playerId\":\"0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c\"")]
    [InlineData("\"buildingId\":1")]
    public async Task Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing(string extra)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _placeWoodcutter.Replace("}", "," + extra + "}", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>A body that isn't the contract's shape is refused (400) before anything runs: a missing field, a wrong type, broken JSON.</summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData("""{"buildingTypeId":"woodcutter","x":2,"y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","x":2,"y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2}""")]
    [InlineData("""{"requestId":"not-a-uuid","buildingTypeId":"woodcutter","x":2,"y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":7,"x":2,"y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":"2","y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2.5,"y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2147483648,"y":3}""")]
    [InlineData($$"""[{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":3}]""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":3""")]
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
    /// <c>null</c>, the all-zeros request id, a blank type, a negative coordinate (OD-13).
    /// </summary>
    /// <param name="body">The body; <see langword="null"/> sends none.</param>
    /// <param name="requestId">The request id the answer echoes, or <see langword="null"/> when it has none.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("null", null)]
    [InlineData("""{"requestId":"00000000-0000-0000-0000-000000000000","buildingTypeId":"woodcutter","x":2,"y":3}""", null)]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":" ","x":2,"y":3}""", _requestId)]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":null,"x":2,"y":3}""", _requestId)]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":-1,"y":3}""", _requestId)]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":-1}""", _requestId)]
    public async Task Post_BodyThatIsNoCommand_Is400InvalidArgument(string? body, string? requestId)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId);
        AssertNothingRan(store);
    }

    /// <summary>
    /// The OpenAPI document (Development) describes the endpoint for client authors: the request body is <c>PlaceBuildingRequest</c> and the
    /// 200 body is the snapshot, <c>CitySnapshot</c>, although the handler writes the snapshot as text.
    /// </summary>
    [Fact]
    public async Task OpenApi_PlaceBuilding_DescribesTheBodyAndTheSnapshot()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/openapi/v1.json", playerId: null);

        JsonNode post = response.Json()["paths"]![_path]!["post"]!;
        // The body parameter is nullable (no body is INVALID_ARGUMENT), so the schema is oneOf null or the request.
        Assert.Contains("\"#/components/schemas/PlaceBuildingRequest\"", post["requestBody"]!["content"]!["application/json"]!["schema"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.EndsWith("/CitySnapshot", (string)post["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!, StringComparison.Ordinal);
    }

    /// <summary>A server copy with a store holding the starter city, settled at <see cref="_settledAt"/>, and a fixed clock.</summary>
    /// <param name="now">The server time.</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(DateTimeOffset now) => Serve(new FakeTimeProvider(now));

    /// <summary>A server copy with a store holding the starter city, settled at <see cref="_settledAt"/>, and the given clock.</summary>
    /// <param name="clock">The server clock.</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(FakeTimeProvider clock)
    {
        var store = new FakeCityStore();
        store.Add(Starter(ContentOf(factory), Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c17"), _playerId, _settledAt.ToUnixTimeSeconds()));
        return (store, store.Serve(factory, clock));
    }

    /// <summary>Asserts that the store saved nothing: the city is the stored one at version 0, and no ledger row was written.</summary>
    /// <param name="store">The store.</param>
    private static void AssertNothingRan(FakeCityStore store)
    {
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Ledger);
        CityAggregate city = store.Stored(_playerId);
        Assert.Equal(0, city.StateVersion);
        Assert.Empty(city.State.Buildings);
    }
}
