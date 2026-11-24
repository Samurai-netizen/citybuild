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
/// Courier deliveries over HTTP (CR-0008 amendment 1, <c>core-instructions/05-api-contract.md</c>): <c>start-trip</c> in a city with a
/// courier station gives the courier delivery lines, and <c>POST /api/v1/commands/cancel-delivery</c> cancels one or all of them, on the raw
/// JSON over a small city in a <see cref="FakeCityStore"/> on the server's content (a courier station level 1 carries 10; a tile of open
/// ground takes 5 s): the real endpoints, pipeline, engine and serializer, no database. It covers the lines in the snapshot, the body's
/// boundary checks (OD-13) and the rule's codes. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class CancelDeliveryContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/cancel-delivery";

    /// <summary>The request id of the cancels.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-000000000021";

    /// <summary>The request id of the delivery sent first.</summary>
    private const string _tripRequestId = "0199a1b2-c3d4-7e5f-8a9b-000000000020";

    /// <summary>The server second the city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a90");

    /// <summary>
    /// With a built courier station, a delivery of all 30 logs becomes three lines of 10 for courier 4/1, the logs stay in the woodcutter's
    /// store, no worker leaves, and the courier walks to the first line's source at once (4 tiles: 20 s).
    /// </summary>
    [Fact]
    public async Task StartTrip_WithACourierStation_QueuesLinesForTheCourier()
    {
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        JsonNode snapshot = (await SendAll(server)).Json();

        Assert.Equal(
            """[{"id":1,"stationId":4,"slot":1,"sourceId":2,"targetId":3,"itemId":"logs","quantity":10},""" +
            """{"id":2,"stationId":4,"slot":1,"sourceId":2,"targetId":3,"itemId":"logs","quantity":10},""" +
            """{"id":3,"stationId":4,"slot":1,"sourceId":2,"targetId":3,"itemId":"logs","quantity":10}]""",
            snapshot["city"]!["deliveryLines"]!.ToJsonString());
        Assert.Equal("[]", snapshot["city"]!["trips"]!.ToJsonString());
        Assert.Equal("""{"logs":30}""", snapshot["city"]!["buildings"]![1]!["store"]!.ToJsonString());
        Assert.Equal(
            """[{"stationId":4,"slot":1,"state":"Walking","toStop":1,"departedAt":0,"arrivesAt":20,"stamina":14400,"load":[]}]""",
            snapshot["city"]!["couriers"]!.ToJsonString());
    }

    /// <summary>With the only courier on a route, a delivery is <c>NO_FREE_COURIER</c>: workers don't carry once a station is built (D14).</summary>
    [Fact]
    public async Task StartTrip_EveryCourierOnARoute_IsNoFreeCourier()
    {
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt), withRoute: true);
        await using WebApplicationFactory<Program> _ = server;

        AssertProblem(await SendAll(server), (HttpStatusCode)422, "NO_FREE_COURIER", _tripRequestId);
    }

    /// <summary>
    /// A delivery whose lines don't fit in the courier's queue is <c>DELIVERY_QUEUE_FULL</c> and queues none of them (invariant 15, D17; only the
    /// settle is saved, which sends the courier off on the lines it had): the 30 logs
    /// make three lines, so they fit behind <c>maxDeliveryLines − 3</c> queued lines and not behind one more (M0-81).
    /// </summary>
    /// <param name="belowTheLimit">How many lines fewer than the content's maximum the courier has queued already.</param>
    /// <param name="fits">Whether the three new lines fit.</param>
    [Theory]
    [InlineData(3, true)]
    [InlineData(2, false)]
    public async Task StartTrip_LinesBeyondTheQueue_IsDeliveryQueueFull(int belowTheLimit, bool fits)
    {
        GameContent content = ContentOf(factory);
        int queued = content.Rules.MaxDeliveryLines - belowTheLimit;
        var store = new FakeCityStore();
        store.Add(City(content, withRoute: false, queuedLines: queued));
        await using WebApplicationFactory<Program> server = store.Serve(factory, new FakeTimeProvider(_settledAt));

        ApiResponse response = await SendAll(server);

        if (fits)
        {
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal(content.Rules.MaxDeliveryLines, response.Json()["city"]!["deliveryLines"]!.AsArray().Count);
            return;
        }

        AssertProblem(response, (HttpStatusCode)422, "DELIVERY_QUEUE_FULL", _tripRequestId);
        Assert.Equal(queued, store.Stored(_playerId).State.DeliveryLines.Count);
        Assert.Empty(store.Ledger);
    }

    /// <summary>
    /// A route for a courier that still has delivery lines is <c>COURIER_BUSY</c> (invariant 15, D19): the lines stay, no route is stored, and
    /// only the delivery was saved (M0-81).
    /// </summary>
    [Fact]
    public async Task SetRoute_CourierWithDeliveryLines_IsCourierBusy()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;
        Assert.Equal(HttpStatusCode.OK, (await SendAll(server)).Status);

        ApiResponse response = await ApiClient.PostAsync(
            server,
            "/api/v1/commands/set-route",
            _playerId,
            $$"""{"requestId":"{{_requestId}}","stationId":4,"slot":1,"stops":[{"buildingId":2,"actions":[{"kind":"take","itemId":"logs","amount":"all"}]},{"buildingId":3,"actions":[{"kind":"put","itemId":"logs","amount":"all"}]}]}""");

        AssertProblem(response, (HttpStatusCode)422, "COURIER_BUSY", _requestId);
        Assert.Equal(1, store.SaveCount);
        CityState city = store.Stored(_playerId).State;
        Assert.Equal((3, 0), (city.DeliveryLines.Count, city.Routes.Count));
    }

    /// <summary>Cancelling the second of three queued lines leaves lines 1 and 3; the courier keeps walking its first line.</summary>
    [Fact]
    public async Task Post_OneLine_RemovesThatLine()
    {
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;
        Assert.Equal(HttpStatusCode.OK, (await SendAll(server)).Status);

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(4, 1, "2"));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode city = response.Json()["city"]!;
        Assert.Equal([1, 3], city["deliveryLines"]!.AsArray().Select(line => (int)line!["id"]!));
        Assert.Equal("Walking", (string)city["couriers"]![0]!["state"]!);
    }

    /// <summary>"Cancel all" (<c>lineId</c> null) removes every line and the route, and the courier stands at its station.</summary>
    [Fact]
    public async Task Post_All_RemovesEveryLineAndTheRoute()
    {
        (FakeCityStore _, WebApplicationFactory<Program> lined) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = lined;
        (FakeCityStore _, WebApplicationFactory<Program> routed) = Serve(new FakeTimeProvider(_settledAt), withRoute: true);
        await using WebApplicationFactory<Program> __ = routed;
        Assert.Equal(HttpStatusCode.OK, (await SendAll(lined)).Status);

        JsonNode afterLines = (await ApiClient.PostAsync(lined, _path, _playerId, Body(4, 1, "null"))).Json()["city"]!;
        JsonNode afterRoute = (await ApiClient.PostAsync(routed, _path, _playerId, Body(4, 1, "null"))).Json()["city"]!;

        Assert.Equal("[]", afterLines["deliveryLines"]!.ToJsonString());
        Assert.Equal("AtStation", (string)afterLines["couriers"]![0]!["state"]!);
        Assert.Equal("[]", afterRoute["routes"]!.ToJsonString());
    }

    /// <summary>
    /// The rule's refusals: a station the city doesn't have (404), a building that is no courier station and a slot without a courier (400),
    /// and a line that is not that courier's (422).
    /// </summary>
    /// <param name="stationId">The station sent.</param>
    /// <param name="slot">The slot sent.</param>
    /// <param name="lineId">The line sent, as JSON.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(9, 1, "null", 404, "BUILDING_NOT_FOUND")]
    [InlineData(2, 1, "null", 400, "INVALID_ARGUMENT")]
    [InlineData(4, 2, "null", 400, "INVALID_ARGUMENT")]
    [InlineData(4, 1, "7", 422, "DELIVERY_NOT_FOUND")]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(int stationId, int slot, string lineId, int status, string code)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(stationId, slot, lineId));

        AssertProblem(response, (HttpStatusCode)status, code, _requestId);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// A body no city could take is <c>INVALID_ARGUMENT</c> (400) before the city is locked (OD-13): a station, a slot or a line below 1, a
    /// missing <c>lineId</c>, a property the contract doesn't have, and a line id that is no whole number.
    /// </summary>
    /// <param name="body">The body sent.</param>
    [Theory]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000021","stationId":0,"slot":1,"lineId":null}""")]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000021","stationId":4,"slot":0,"lineId":null}""")]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000021","stationId":4,"slot":1,"lineId":0}""")]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000021","stationId":4,"slot":1}""")]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000021","stationId":4,"slot":1,"lineId":null,"quantity":5}""")]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000021","stationId":4,"slot":1,"lineId":"1"}""")]
    public async Task Post_ShapeNoCityCouldTake_IsInvalidArgumentBeforeTheLock(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("INVALID_ARGUMENT", (string)response.Json()["code"]!);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>Sends a delivery of all the woodcutter's logs to the warehouse.</summary>
    /// <param name="server">The server.</param>
    /// <returns>The answer.</returns>
    private static Task<ApiResponse> SendAll(WebApplicationFactory<Program> server) =>
        ApiClient.PostAsync(
            server,
            "/api/v1/commands/start-trip",
            _playerId,
            $$"""{"requestId":"{{_tripRequestId}}","sourceId":2,"targetId":3,"itemId":"logs","amount":"all"}""");

    /// <summary>The JSON body of a cancel.</summary>
    /// <param name="stationId">The station id.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="lineId">The line id, as JSON (<c>null</c> for all).</param>
    /// <returns>The body.</returns>
    private static string Body(int stationId, int slot, string lineId) =>
        $$"""{"requestId":"{{_requestId}}","stationId":{{stationId}},"slot":{{slot}},"lineId":{{lineId}}}""";

    /// <summary>A server copy with a store holding <see cref="City"/>, settled at <see cref="_settledAt"/>, and the given clock.</summary>
    /// <param name="clock">The server clock.</param>
    /// <param name="withRoute">Whether courier 4/1 has a route.</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(FakeTimeProvider clock, bool withRoute = false)
    {
        var store = new FakeCityStore();
        store.Add(City(ContentOf(factory), withRoute));
        return (store, store.Serve(factory, clock));
    }

    /// <summary>
    /// The city of the tests at clock 0, on open grass of the start area: 100 coins; house 1 at (4,4); woodcutter 2 at (6,4) with 30 logs; warehouse 3 at (6,6) with 5 logs;
    /// courier station 4 at (4,6) with 1 worker, courier 4/1 at the station with full stamina, and (<paramref name="withRoute"/>) a route
    /// woodcutter → warehouse; Scriptorium 5 at (9,9).
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <param name="withRoute">Whether the courier has a route.</param>
    /// <param name="queuedLines">How many lines of 1 log, woodcutter → warehouse, courier 4/1 has queued already.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content, bool withRoute, int queuedLines = 0)
    {
        var logs = new ItemId("logs");
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 4, 4, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 6, 4, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(logs, 30)]),
            new(3, new BuildingTypeId("warehouse"), 6, 6, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(logs, 5)]),
            new(4, new BuildingTypeId("courier_station"), 4, 6, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(5, new BuildingTypeId("scriptorium"), 9, 9, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
        ];
        Amount all = Amount.Preset(new AmountPresetId("all"));
        Route[] routes = withRoute
            ? [new Route(4, 1, [new RouteStop(2, [new RouteAction(RouteActionKind.Take, logs, all)]), new RouteStop(3, [new RouteAction(RouteActionKind.Put, logs, all)])])]
            : [];
        var state = new CityState(
            0, era: 1, nextBuildingId: 6, purse: 100, buildings, [], activeResearch: null,
            couriers: [new Courier(4, 1, CourierState.AtStation, 0, null, null, 14_400)], routes: routes,
            deliveryLines: Enumerable.Range(1, queuedLines).Select(id => new DeliveryLine(id, 4, 1, 2, 3, logs, 1)),
            nextLineId: queuedLines + 1);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c90"), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), 0, content.ContentVersion);
    }
}
