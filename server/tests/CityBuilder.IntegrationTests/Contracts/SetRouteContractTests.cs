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
/// <c>POST /api/v1/commands/set-route</c> against the contract (<c>core-instructions/05-api-contract.md</c>, CR-0008-T1 part G) on the raw
/// JSON, over a small city in a <see cref="FakeCityStore"/> on the server's content (a courier station level 1 takes 2 couriers carrying 10;
/// routes of up to 8 stops with up to 4 actions; presets all, half, quarter; percentages after Tally Sticks; a tile of open ground takes 5 s):
/// the real endpoint, pipeline, engine and serializer, no database. It covers what the body may hold, the boundary checks before the lock
/// (OD-13), the rule's codes and what the answers look like; PostgreSQL is <c>SetRouteDatabaseTests</c>'. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class SetRouteContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/set-route";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-000000000011";

    /// <summary>The route of most tests: all the woodcutter's logs (up to the carry limit) into the warehouse.</summary>
    private static readonly string _woodToWarehouse = Stops(Stop(2, Action("take", "logs", "all")), Stop(3, Action("put", "logs", "all")));

    /// <summary>The server second the city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a81");

    /// <summary>
    /// A route answers 200 with the snapshot of the city as it is saved: the slot has the route in the form it was sent, and courier 4/1
    /// leaves the station at once for stop 1 (the woodcutter, 4 tiles of open ground: 20 s), carrying nothing yet; no ledger row is written.
    /// It is the same snapshot <c>GET /city</c> shows.
    /// </summary>
    [Fact]
    public async Task Post_WoodToTheWarehouse_StoresTheRouteAndSendsTheCourier()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(4, 1, _woodToWarehouse));

        Assert.Equal((HttpStatusCode.OK, "application/json; charset=utf-8"), (response.Status, response.ContentType));
        JsonNode snapshot = response.Json();
        Assert.Equal((_requestId, 1L), ((string)snapshot["requestId"]!, (long)snapshot["stateVersion"]!));
        Assert.Equal(
            """[{"stationId":4,"slot":1,"stops":[{"buildingId":2,"actions":[{"kind":"take","itemId":"logs","amount":"all"}]},""" +
            """{"buildingId":3,"actions":[{"kind":"put","itemId":"logs","amount":"all"}]}]}]""",
            snapshot["city"]!["routes"]!.ToJsonString());
        Assert.Equal(
            """[{"stationId":4,"slot":1,"state":"Walking","toStop":1,"departedAt":0,"arrivesAt":20,"stamina":14400,"load":[]}]""",
            snapshot["city"]!["couriers"]!.ToJsonString());
        Assert.Equal(1, store.SaveCount);
        Assert.Empty(store.Ledger);

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        Assert.Equal(city["city"]!.ToJsonString(), snapshot["city"]!.ToJsonString());
    }

    /// <summary>
    /// The courier walks its route in the settle that follows: at 20 s it takes 10 logs (the carry limit) at the woodcutter, at 30 s it puts
    /// them into the warehouse (5 + 10) and walks back to stop 1. The city's total of logs stays 35 and the settlement shows them moved.
    /// </summary>
    [Fact]
    public async Task Get_AfterOneLoop_TheWarehouseHasTheLoad()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;
        Assert.Equal(HttpStatusCode.OK, (await ApiClient.PostAsync(server, _path, _playerId, Body(4, 1, _woodToWarehouse))).Status);
        clock.Advance(TimeSpan.FromSeconds(30));

        JsonNode snapshot = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();

        Assert.Equal(("""{"logs":20}""", """{"logs":15}"""), (snapshot["city"]!["buildings"]![1]!["store"]!.ToJsonString(), snapshot["city"]!["buildings"]![2]!["store"]!.ToJsonString()));
        Assert.Equal(
            """[{"stationId":4,"slot":1,"state":"Walking","toStop":1,"departedAt":30,"arrivesAt":40,"stamina":14400,"load":[]}]""",
            snapshot["city"]!["couriers"]!.ToJsonString());
        Assert.Equal(35, (long)snapshot["city"]!["totals"]!["logs"]!);
        Assert.Equal("""{"logs":10}""", snapshot["settlement"]!["moved"]!.ToJsonString());
    }

    /// <summary>
    /// No stops clear the slot's route: courier 4/1, walking with 4 logs from the woodcutter, gives them back and stands at its station.
    /// </summary>
    [Fact]
    public async Task Post_NoStops_ClearsTheRouteAndStopsTheCourier()
    {
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt), walking: true);
        await using WebApplicationFactory<Program> _ = server;

        JsonNode snapshot = (await ApiClient.PostAsync(server, _path, _playerId, Body(4, 1, "[]"))).Json();

        Assert.Equal("[]", snapshot["city"]!["routes"]!.ToJsonString());
        Assert.Equal(
            """[{"stationId":4,"slot":1,"state":"AtStation","toStop":0,"departedAt":null,"arrivesAt":null,"stamina":14400,"load":[]}]""",
            snapshot["city"]!["couriers"]!.ToJsonString());
        Assert.Equal("""{"logs":30}""", snapshot["city"]!["buildings"]![1]!["store"]!.ToJsonString());
    }

    /// <summary>
    /// A rule's refusal is Problem Details with the rule's code and status and the request id, and with no time elapsed nothing is saved: a
    /// station the city doesn't have, a building that is no courier station, a slot above the level's 2, one stop, 9 stops (above the
    /// content's 8), 5 actions at a stop (above its 4), two stops in a row at one building, a stop at a building the city doesn't have, an item
    /// the content doesn't have, the currency, a preset the content doesn't have, a percentage before Tally Sticks and an exact number.
    /// </summary>
    /// <param name="stationId">The station sent.</param>
    /// <param name="slot">The slot sent.</param>
    /// <param name="stops">The stops sent, as JSON.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [MemberData(nameof(RuleRefusals))]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(int stationId, int slot, string stops, int status, string code)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(stationId, slot, stops));

        AssertProblem(response, (HttpStatusCode)status, code, _requestId);
        AssertNothingRan(store);
    }

    /// <summary>
    /// Without a built Scriptorium a route is <c>ROUTES_LOCKED</c> (CR-0008 D15) and nothing is saved; clearing the route still works, since a
    /// route that is gone needs no Scriptorium.
    /// </summary>
    [Fact]
    public async Task Post_NoScriptorium_IsRoutesLocked()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt), scriptorium: false);
        await using WebApplicationFactory<Program> _ = server;
        string stops = "[" + Stop(2, Action("take", "logs", "all")) + "," + Stop(3, Action("put", "logs", "all")) + "]";

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(4, 1, stops));

        AssertProblem(response, (HttpStatusCode)422, "ROUTES_LOCKED", _requestId);
        AssertNothingRan(store);
    }

    /// <summary>The cases of <see cref="Post_RuleRefuses_IsProblemWithTheRulesCode"/>.</summary>
    /// <returns>Station, slot, stops, status and code.</returns>
    public static TheoryData<int, int, string, int, string> RuleRefusals()
    {
        string take = Action("take", "logs", "all");
        string put = Action("put", "logs", "all");
        return new TheoryData<int, int, string, int, string>
        {
            { 9, 1, _woodToWarehouse, 404, "BUILDING_NOT_FOUND" },
            { 1, 1, _woodToWarehouse, 400, "INVALID_ARGUMENT" },
            { 4, 3, _woodToWarehouse, 422, "INVALID_ROUTE" },
            { 4, 1, Stops(Stop(2, take)), 422, "INVALID_ROUTE" },
            { 4, 1, Stops([.. Enumerable.Range(0, 9).Select(stop => Stop(2 + (stop % 2), take))]), 422, "INVALID_ROUTE" },
            { 4, 1, Stops(Stop(2, take, take, take, take, take), Stop(3, put)), 422, "INVALID_ROUTE" },
            { 4, 1, Stops(Stop(2, take), Stop(2, take), Stop(3, put)), 422, "INVALID_ROUTE" },
            { 4, 1, Stops(Stop(2, take), Stop(9, put)), 404, "BUILDING_NOT_FOUND" },
            { 4, 1, Stops(Stop(2, Action("take", "stone", "all")), Stop(3, put)), 422, "UNKNOWN_ITEM" },
            { 4, 1, Stops(Stop(2, Action("take", "coins", "all")), Stop(3, put)), 400, "INVALID_ARGUMENT" },
            { 4, 1, Stops(Stop(2, Action("take", "logs", "eighth")), Stop(3, put)), 400, "INVALID_ARGUMENT" },
            { 4, 1, Stops(Stop(2, Action("take", "logs", "50%")), Stop(3, put)), 422, "AMOUNT_MODE_LOCKED" },
            { 4, 1, Stops(Stop(2, Action("take", "logs", "12")), Stop(3, put)), 422, "AMOUNT_MODE_LOCKED" },
        };
    }

    /// <summary>
    /// A body no city could take is <c>INVALID_ARGUMENT</c> (400) with the request id before the city is locked (owner directive OD-13): a
    /// station or slot below 1, 17 stops, a missing stop list or stop, a building id below 1, a stop without actions or with 9, a kind other
    /// than take or put, an item id that can't be a content id, and an amount of none of the three forms.
    /// </summary>
    /// <param name="stationId">The station sent.</param>
    /// <param name="slot">The slot sent.</param>
    /// <param name="stops">The stops sent, as JSON.</param>
    [Theory]
    [MemberData(nameof(ShapeRefusals))]
    public async Task Security_BodyNoCityCouldTake_Is400BeforeTheCityIsLocked(int stationId, int slot, string stops)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(stationId, slot, stops));

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", _requestId);
        Assert.Equal(0, store.LockCount);
        AssertNothingRan(store);
    }

    /// <summary>The cases of <see cref="Security_BodyNoCityCouldTake_Is400BeforeTheCityIsLocked"/>.</summary>
    /// <returns>Station, slot and stops.</returns>
    public static TheoryData<int, int, string> ShapeRefusals()
    {
        string take = Action("take", "logs", "all");
        string put = Action("put", "logs", "all");
        return new TheoryData<int, int, string>
        {
            { 0, 1, _woodToWarehouse },
            { 4, 0, _woodToWarehouse },
            { 4, 1, Stops([.. Enumerable.Range(0, 17).Select(stop => Stop(2 + (stop % 2), take))]) },
            { 4, 1, "null" },
            { 4, 1, Stops(Stop(2, take), "null") },
            { 4, 1, Stops(Stop(2, take), Stop(0, put)) },
            { 4, 1, Stops(Stop(2, take), Stop(3)) },
            { 4, 1, Stops(Stop(2, take), Stop(3, put, put, put, put, put, put, put, put, put)) },
            { 4, 1, Stops(Stop(2, Action("drop", "logs", "all")), Stop(3, put)) },
            { 4, 1, Stops(Stop(2, Action("take", "Logs", "all")), Stop(3, put)) },
            { 4, 1, Stops(Stop(2, Action("take", "logs'; DROP TABLE cities; --", "all")), Stop(3, put)) },
            { 4, 1, Stops(Stop(2, Action("take", "logs", "1.5")), Stop(3, put)) },
            { 4, 1, Stops(Stop(2, Action("take", "logs", "0%")), Stop(3, put)) },
        };
    }

    /// <summary>
    /// A body with a property the contract doesn't have (on the body, a stop or an action), a missing field, a number sent as a string or an
    /// amount sent as a number is refused (400) before anything runs: the server never takes a walk time, a load or a stamina from the client.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":4,"slot":1,"stops":[],"stamina":900}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":4,"slot":1,"stops":[{"buildingId":2,"actions":[],"arrivesAt":5}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":4,"slot":1,"stops":[{"buildingId":2,"actions":[{"kind":"take","itemId":"logs","amount":"all","quantity":9}]}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":4,"slot":1}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":4,"slot":1,"stops":[{"buildingId":2}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":4,"slot":1,"stops":[{"buildingId":2,"actions":[{"kind":"take","itemId":"logs"}]}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":"4","slot":1,"stops":[]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":4,"slot":1,"stops":[{"buildingId":2,"actions":[{"kind":"take","itemId":"logs","amount":10}]}]}""")]
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
        ApiResponse first = await ApiClient.PostAsync(server, _path, _playerId, Body(4, 1, _woodToWarehouse));
        clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await ApiClient.PostAsync(server, _path, _playerId, Body(4, 1, _woodToWarehouse));
        ApiResponse other = await ApiClient.PostAsync(
            server, _path, _playerId, Body(4, 1, Stops(Stop(2, Action("take", "logs", "half")), Stop(3, Action("put", "logs", "all")))));

        Assert.Equal((HttpStatusCode.OK, first.Body), (retry.Status, retry.Body));
        AssertProblem(other, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT", _requestId);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>The OpenAPI document (Development) describes the body as <c>SetRouteRequest</c> and the 200 body as the snapshot.</summary>
    [Fact]
    public async Task OpenApi_SetRoute_DescribesTheBodyAndTheSnapshot()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/openapi/v1.json", playerId: null);

        JsonNode post = response.Json()["paths"]![_path]!["post"]!;
        Assert.Contains("\"#/components/schemas/SetRouteRequest\"", post["requestBody"]!["content"]!["application/json"]!["schema"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.EndsWith("/CitySnapshot", (string)post["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!, StringComparison.Ordinal);
    }

    /// <summary>A body with the test's request id.</summary>
    /// <param name="stationId">The station id.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="stops">The stops, as JSON.</param>
    /// <returns>The JSON body.</returns>
    private static string Body(int stationId, int slot, string stops) =>
        $$"""{"requestId":"{{_requestId}}","stationId":{{stationId}},"slot":{{slot}},"stops":{{stops}}}""";

    /// <summary>A JSON list of stops.</summary>
    /// <param name="stops">The stops, as JSON.</param>
    /// <returns>The list.</returns>
    private static string Stops(params string[] stops) => "[" + string.Join(",", stops) + "]";

    /// <summary>A JSON stop.</summary>
    /// <param name="buildingId">The building.</param>
    /// <param name="actions">The actions, as JSON.</param>
    /// <returns>The stop.</returns>
    private static string Stop(int buildingId, params string[] actions) =>
        $$"""{"buildingId":{{buildingId}},"actions":[{{string.Join(",", actions)}}]}""";

    /// <summary>A JSON action.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="item">The item id.</param>
    /// <param name="amount">The amount's text.</param>
    /// <returns>The action.</returns>
    private static string Action(string kind, string item, string amount) =>
        $$"""{"kind":"{{kind}}","itemId":"{{item.Replace("\"", "\\\"", StringComparison.Ordinal)}}","amount":"{{amount}}"}""";

    /// <summary>A server copy with a store holding <see cref="City"/>, settled at <see cref="_settledAt"/>, and the given clock.</summary>
    /// <param name="clock">The server clock.</param>
    /// <param name="walking">Whether courier 4/1 already walks the route with 4 logs.</param>
    /// <param name="scriptorium">Whether the city has its built Scriptorium, which routes need (CR-0008 D15).</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(FakeTimeProvider clock, bool walking = false, bool scriptorium = true)
    {
        var store = new FakeCityStore();
        store.Add(City(ContentOf(factory), walking, scriptorium));
        return (store, store.Serve(factory, clock));
    }

    /// <summary>
    /// The city of the tests at clock 0: 100 coins; house 1 at (0,0) (4 workers); woodcutter 2 at (2,0), idle without workers, with 30 logs in
    /// its store; warehouse 3 at (2,2) with 5 logs; courier station 4 at (0,2) with 1 worker, courier 4/1 at the station with full stamina,
    /// or (<paramref name="walking"/>) on the warehouse route, walking to the warehouse with 4 of the woodcutter's logs (26 left there);
    /// Scriptorium 5 at (5,5), built, unless <paramref name="scriptorium"/> is false.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <param name="walking">Whether the courier walks.</param>
    /// <param name="scriptorium">Whether the city has the Scriptorium.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content, bool walking, bool scriptorium)
    {
        var logs = new ItemId("logs");
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(logs, walking ? 26 : 30)]),
            new(3, new BuildingTypeId("warehouse"), 2, 2, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(logs, 5)]),
            new(4, new BuildingTypeId("courier_station"), 0, 2, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            .. scriptorium ? [new Building(5, new BuildingTypeId("scriptorium"), 5, 5, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0)] : Array.Empty<Building>(),
        ];
        Courier courier = walking
            ? new Courier(4, 1, CourierState.Walking, 2, 0, 10, 14_400, [new CourierLoad(logs, 2, 4)])
            : new Courier(4, 1, CourierState.AtStation, 0, null, null, 14_400);
        Route[] routes = walking
            ?
            [
                new Route(4, 1, [
                    new RouteStop(2, [new RouteAction(RouteActionKind.Take, logs, Amount.Preset(new AmountPresetId("all")))]),
                    new RouteStop(3, [new RouteAction(RouteActionKind.Put, logs, Amount.Preset(new AmountPresetId("all")))])]),
            ]
            : [];
        var state = new CityState(0, era: 1, nextBuildingId: 6, purse: 100, buildings, [], activeResearch: null, couriers: [courier], routes: routes);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c81"), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), 0, content.ContentVersion);
    }

    /// <summary>Asserts that the store saved nothing: the city is the stored one at version 0, with no route, and no ledger row.</summary>
    /// <param name="store">The store.</param>
    private static void AssertNothingRan(FakeCityStore store)
    {
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Ledger);
        CityAggregate city = store.Stored(_playerId);
        Assert.Equal(0, city.StateVersion);
        Assert.Empty(city.State.Routes);
    }
}
