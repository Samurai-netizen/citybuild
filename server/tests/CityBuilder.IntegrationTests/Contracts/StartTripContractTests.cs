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
/// <c>POST /api/v1/commands/start-trip</c> against the contract (<c>core-instructions/05-api-contract.md</c>, CR-0008) on the raw JSON, over a
/// small city in a <see cref="FakeCityStore"/> on the server's content (trips carry 10; presets all, half, quarter; percentages after Tally
/// Sticks; a tile of open ground takes 5 s): the real endpoint, pipeline, engine and serializer, no database. It covers what the body may hold,
/// the boundary checks before the lock (OD-13), the rule's codes and what the answers look like; PostgreSQL is
/// <c>StartTripDatabaseTests</c>'. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class StartTripContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/start-trip";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-000000000010";

    /// <summary>The server second the city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a80");

    /// <summary>
    /// A trip of all the woodcutter's logs to the warehouse answers 200 with the snapshot of the city as it is saved: the trip takes 10 (the
    /// carry limit) of the 30 logs at once, walks the two free tiles in 10 s, and keeps a worker busy; the city's total of logs is the same
    /// (they are on the way) and no ledger row is written. It is the same snapshot <c>GET /city</c> shows.
    /// </summary>
    [Fact]
    public async Task Post_LogsToTheWarehouse_TakesThemAndShowsTheTrip()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(2, 3, "logs", "all"));

        Assert.Equal((HttpStatusCode.OK, "application/json; charset=utf-8"), (response.Status, response.ContentType));
        JsonNode snapshot = response.Json();
        Assert.Equal((_requestId, 1L), ((string)snapshot["requestId"]!, (long)snapshot["stateVersion"]!));
        Assert.Equal(
            """[{"id":1,"sourceId":2,"targetId":3,"itemId":"logs","quantity":10,"departedAt":0,"arrivesAt":10}]""",
            snapshot["city"]!["trips"]!.ToJsonString());
        Assert.Equal("""{"provided":4,"assigned":0,"onTrips":1,"free":3}""", snapshot["city"]!["workers"]!.ToJsonString());
        Assert.Equal("""{"logs":20}""", snapshot["city"]!["buildings"]![1]!["store"]!.ToJsonString());
        Assert.Equal("""{"coins":100,"logs":35,"planks":0}""", snapshot["city"]!["totals"]!.ToJsonString());
        Assert.Equal(1, store.SaveCount);
        Assert.Empty(store.Ledger);

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        Assert.Equal(city["city"]!.ToJsonString(), snapshot["city"]!.ToJsonString());
    }

    /// <summary>
    /// When the walk is over the request that settles the city delivers the load: the warehouse holds 5 + 10 logs, the settlement says 10 logs
    /// were moved, the trip is gone and its worker free.
    /// </summary>
    [Fact]
    public async Task Get_AfterTheWalk_DeliversTheLoad()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;
        Assert.Equal(HttpStatusCode.OK, (await ApiClient.PostAsync(server, _path, _playerId, Body(2, 3, "logs", "all"))).Status);
        clock.Advance(TimeSpan.FromSeconds(10));

        JsonNode snapshot = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();

        Assert.Equal("""{"logs":15}""", snapshot["city"]!["buildings"]![2]!["store"]!.ToJsonString());
        Assert.Equal("""{"logs":10}""", snapshot["settlement"]!["moved"]!.ToJsonString());
        Assert.Equal("[]", snapshot["city"]!["trips"]!.ToJsonString());
        Assert.Equal(4, (int)snapshot["city"]!["workers"]!["free"]!);
    }

    /// <summary>The amount's forms: <c>half</c> of 30 is 15, at most 10; <c>quarter</c> of 30 is 7 (rounded down).</summary>
    /// <param name="amount">The amount sent.</param>
    /// <param name="quantity">The load expected.</param>
    [Theory]
    [InlineData("half", 10)]
    [InlineData("quarter", 7)]
    public async Task Post_Preset_CarriesItsShareWithinTheCarryLimit(string amount, long quantity)
    {
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(2, 3, "logs", amount));

        Assert.Equal(quantity, (long)response.Json()["city"]!["trips"]![0]!["quantity"]!);
    }

    /// <summary>
    /// A rule's refusal is Problem Details with the rule's code and status and the request id, and with no time elapsed nothing is saved: a
    /// building the city doesn't have, an item the content doesn't have, the currency, a preset the content doesn't have, a percentage before
    /// Tally Sticks and an exact number (no research of the content unlocks it yet), a target that takes no logs (the house) and one that
    /// takes no planks (the woodcutter).
    /// </summary>
    /// <param name="source">The source sent.</param>
    /// <param name="target">The target sent.</param>
    /// <param name="item">The item sent.</param>
    /// <param name="amount">The amount sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(9, 3, "logs", "all", HttpStatusCode.NotFound, "BUILDING_NOT_FOUND")]
    [InlineData(2, 3, "stone", "all", (HttpStatusCode)422, "UNKNOWN_ITEM")]
    [InlineData(2, 3, "coins", "all", HttpStatusCode.BadRequest, "INVALID_ARGUMENT")]
    [InlineData(2, 3, "logs", "eighth", HttpStatusCode.BadRequest, "INVALID_ARGUMENT")]
    [InlineData(2, 3, "logs", "50%", (HttpStatusCode)422, "AMOUNT_MODE_LOCKED")]
    [InlineData(2, 1, "logs", "all", (HttpStatusCode)422, "ITEM_NOT_ACCEPTED")]
    [InlineData(3, 2, "planks", "all", (HttpStatusCode)422, "ITEM_NOT_ACCEPTED")]
    [InlineData(3, 2, "logs", "12", (HttpStatusCode)422, "AMOUNT_MODE_LOCKED")]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(int source, int target, string item, string amount, HttpStatusCode status, string code)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(source, target, item, amount));

        AssertProblem(response, status, code, _requestId);
        AssertNothingRan(store);
    }

    /// <summary>A source with none of the good gives nothing to carry: the warehouse's logs to the woodcutter after the warehouse was emptied.</summary>
    [Fact]
    public async Task Post_SourceWithoutTheGood_Is422NothingToCarry()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt), warehouseLogs: 0);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(3, 2, "logs", "all"));

        AssertProblem(response, (HttpStatusCode)422, "NOTHING_TO_CARRY", _requestId);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body no city could take is <c>INVALID_ARGUMENT</c> (400) with the request id before the city is locked (owner directive OD-13): ids
    /// below 1, the same building twice, an item id that can't be a content id, and an amount of none of the three forms.
    /// </summary>
    /// <param name="source">The source sent.</param>
    /// <param name="target">The target sent.</param>
    /// <param name="item">The item sent.</param>
    /// <param name="amount">The amount sent.</param>
    [Theory]
    [InlineData(0, 3, "logs", "all")]
    [InlineData(2, -1, "logs", "all")]
    [InlineData(2, 2, "logs", "all")]
    [InlineData(2, 3, "Logs", "all")]
    [InlineData(2, 3, "logs'; DROP TABLE cities; --", "all")]
    [InlineData(2, 3, "logs", "")]
    [InlineData(2, 3, "logs", "0%")]
    [InlineData(2, 3, "logs", "101%")]
    [InlineData(2, 3, "logs", "01")]
    [InlineData(2, 3, "logs", "1.5")]
    [InlineData(2, 3, "logs", "1000000000000")]
    public async Task Security_BodyNoCityCouldTake_Is400BeforeTheCityIsLocked(int source, int target, string item, string amount)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(source, target, item, amount));

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", _requestId);
        Assert.Equal(0, store.LockCount);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body with a property the contract doesn't have, a missing field, a number sent as a string or an amount sent as a number is refused
    /// (400) before anything runs: the server never takes a quantity, a time or a worker from the client.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData($$"""{"requestId":"{{_requestId}}","sourceId":2,"targetId":3,"itemId":"logs","amount":"all","quantity":30}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","sourceId":2,"targetId":3,"itemId":"logs","amount":"all","arrivesAt":1}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","sourceId":2,"targetId":3,"itemId":"logs"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","sourceId":"2","targetId":3,"itemId":"logs","amount":"all"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","sourceId":2,"targetId":3,"itemId":"logs","amount":10}""")]
    public async Task Security_BodyOutsideTheContract_Is400AndRunsNothing(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A retry with the same request id and body a minute later answers with the stored response byte for byte and runs nothing; the same
    /// request id with another amount is <c>IDEMPOTENCY_CONFLICT</c> (409).
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndAnotherPayloadIsAConflict()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;
        ApiResponse first = await ApiClient.PostAsync(server, _path, _playerId, Body(2, 3, "logs", "all"));
        clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await ApiClient.PostAsync(server, _path, _playerId, Body(2, 3, "logs", "all"));
        ApiResponse other = await ApiClient.PostAsync(server, _path, _playerId, Body(2, 3, "logs", "half"));

        Assert.Equal((HttpStatusCode.OK, first.Body), (retry.Status, retry.Body));
        AssertProblem(other, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT", _requestId);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>The OpenAPI document (Development) describes the body as <c>StartTripRequest</c> and the 200 body as the snapshot.</summary>
    [Fact]
    public async Task OpenApi_StartTrip_DescribesTheBodyAndTheSnapshot()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/openapi/v1.json", playerId: null);

        JsonNode post = response.Json()["paths"]![_path]!["post"]!;
        Assert.Contains("\"#/components/schemas/StartTripRequest\"", post["requestBody"]!["content"]!["application/json"]!["schema"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.EndsWith("/CitySnapshot", (string)post["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!, StringComparison.Ordinal);
    }

    /// <summary>A body with the test's request id.</summary>
    /// <param name="source">The source id.</param>
    /// <param name="target">The target id.</param>
    /// <param name="item">The item id.</param>
    /// <param name="amount">The amount's text.</param>
    /// <returns>The JSON body.</returns>
    private static string Body(int source, int target, string item, string amount) =>
        $$"""{"requestId":"{{_requestId}}","sourceId":{{source}},"targetId":{{target}},"itemId":"{{item.Replace("\"", "\\\"", StringComparison.Ordinal)}}","amount":"{{amount}}"}""";

    /// <summary>A server copy with a store holding <see cref="City"/>, settled at <see cref="_settledAt"/>, and the given clock.</summary>
    /// <param name="clock">The server clock.</param>
    /// <param name="warehouseLogs">The logs in the warehouse's store.</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(FakeTimeProvider clock, long warehouseLogs = 5)
    {
        var store = new FakeCityStore();
        store.Add(City(ContentOf(factory), warehouseLogs));
        return (store, store.Serve(factory, clock));
    }

    /// <summary>
    /// The city of the tests at clock 0: 100 coins; house 1 at (0,0) (4 workers, none assigned); woodcutter 2 at (2,0), idle without workers,
    /// with 30 logs in its store; warehouse 3 at (2,2) with <paramref name="warehouseLogs"/> logs. The tile between them is free: 10 s.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <param name="warehouseLogs">The logs in the warehouse's store.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content, long warehouseLogs)
    {
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(new ItemId("logs"), 30)]),
            new(3, new BuildingTypeId("warehouse"), 2, 2, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0,
                warehouseLogs > 0 ? [new(new ItemId("logs"), warehouseLogs)] : []),
        ];
        var state = new CityState(0, era: 1, nextBuildingId: 4, purse: 100, buildings, [], activeResearch: null);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c80"), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), 0, content.ContentVersion);
    }

    /// <summary>Asserts that the store saved nothing: the city is the stored one at version 0, with no trip, and no ledger row.</summary>
    /// <param name="store">The store.</param>
    private static void AssertNothingRan(FakeCityStore store)
    {
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Ledger);
        CityAggregate city = store.Stored(_playerId);
        Assert.Equal(0, city.StateVersion);
        Assert.Empty(city.State.Trips);
    }
}
