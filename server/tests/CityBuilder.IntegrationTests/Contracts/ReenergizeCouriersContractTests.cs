using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// <c>POST /api/v1/commands/reenergize-couriers</c> and presence (<c>core-instructions/05-api-contract.md</c>, <c>04</c> section 2; CR-0008-T1
/// part H) against the contract on the raw JSON, over a small city in a <see cref="FakeCityStore"/> on the server's content (a courier station
/// level 1 takes 2 couriers with 14 400 s of stamina; the presence grace is 300 s): the real endpoint, pipeline, engine and serializer, no
/// database. It covers the body, the re-energizing, the settlement's courier seconds and tired couriers, and replays. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class ReenergizeCouriersContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/reenergize-couriers";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-000000000021";

    /// <summary>The body of the tests.</summary>
    private const string _body = $$"""{"requestId":"{{_requestId}}"}""";

    /// <summary>The server second the city was last settled and seen at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a91");

    /// <summary>
    /// Re-energizing a resting courier answers 200 with the snapshot as it is saved: courier 4/1 has its 14 400 s again and leaves at once
    /// for stop 1 with the 4 logs it kept; no ledger row is written.
    /// </summary>
    [Fact]
    public async Task Post_RestingCourier_FillsItsStaminaAndSendsItOff()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt), resting: true);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, _body);

        Assert.Equal((HttpStatusCode.OK, "application/json; charset=utf-8"), (response.Status, response.ContentType));
        JsonNode snapshot = response.Json();
        Assert.Equal((_requestId, 1L), ((string)snapshot["requestId"]!, (long)snapshot["stateVersion"]!));
        JsonNode courier = snapshot["city"]!["couriers"]![0]!;
        Assert.Equal(("Walking", 1, 14_400L), ((string)courier["state"]!, (int)courier["toStop"]!, (long)courier["stamina"]!));
        Assert.Equal("""[{"itemId":"logs","fromId":2,"quantity":4}]""", courier["load"]!.ToJsonString());
        Assert.Empty(store.Ledger);
    }

    /// <summary>
    /// A body with a property the contract doesn't have (a stamina, a courier), a missing or malformed request id, or no object is refused
    /// (400) before anything runs: the client never sends stamina.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData($$"""{"requestId":"{{_requestId}}","stamina":14400}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","stationId":4}""")]
    [InlineData("""{}""")]
    [InlineData("""{"requestId":"not-a-uuid"}""")]
    [InlineData("""[]""")]
    public async Task Security_BodyOutsideTheContract_Is400AndRunsNothing(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt), resting: true);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(CourierState.Resting, store.Stored(_playerId).State.Couriers[0].State);
    }

    /// <summary>A retry with the same request id a minute later answers with the stored response byte for byte and runs nothing.</summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytes()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(clock, resting: true);
        await using WebApplicationFactory<Program> _ = server;
        ApiResponse first = await ApiClient.PostAsync(server, _path, _playerId, _body);
        clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await ApiClient.PostAsync(server, _path, _playerId, _body);

        Assert.Equal((HttpStatusCode.OK, first.Body), (retry.Status, retry.Body));
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>
    /// Within the grace (300 s after the last request) the player is present: <c>GET /city</c> shows the walking courier with all its
    /// stamina and the settlement's courier seconds 0.
    /// </summary>
    [Fact]
    public async Task GetCity_WithinTheGrace_SpendsNoStamina()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(clock, resting: false);
        await using WebApplicationFactory<Program> disposeServer = server;
        clock.Advance(TimeSpan.FromSeconds(300));

        JsonNode snapshot = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();

        Assert.Equal(14_400L, (long)snapshot["city"]!["couriers"]![0]!["stamina"]!);
        Assert.Equal(0L, (long)snapshot["settlement"]!["courierSeconds"]!);
    }

    /// <summary>
    /// After a day away courier 4/1 spent all its stamina after the grace and rests: <c>GET /city</c> shows it resting with 0, the
    /// settlement's courier seconds 14 400 and one tired courier <c>{ stationId, slot, at }</c>, at most 300 + 14 400 s and more than 300 s
    /// after the last request plus the walks.
    /// </summary>
    [Fact]
    public async Task GetCity_AfterADayAway_ReportsTheTiredCourier()
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(clock, resting: false);
        await using WebApplicationFactory<Program> disposeServer = server;
        clock.Advance(TimeSpan.FromDays(1));

        JsonNode snapshot = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();

        JsonNode courier = snapshot["city"]!["couriers"]![0]!;
        Assert.Equal(("Resting", 0L), ((string)courier["state"]!, (long)courier["stamina"]!));
        JsonNode settlement = snapshot["settlement"]!;
        Assert.Equal(14_400L, (long)settlement["courierSeconds"]!);
        JsonNode tired = Assert.Single(settlement["tiredCouriers"]!.AsArray())!;
        JsonShape.AssertObject(tired, "stationId:int slot:int at:int", "$.settlement.tiredCouriers[0]");
        Assert.Equal((4, 1), ((int)tired["stationId"]!, (int)tired["slot"]!));
        Assert.InRange((long)tired["at"]!, 300 + 14_400, 300 + 14_400 + 30);
    }

    /// <summary>The OpenAPI document (Development) describes the body as <c>ReenergizeCouriersRequest</c> and the 200 body as the snapshot.</summary>
    [Fact]
    public async Task OpenApi_ReenergizeCouriers_DescribesTheBodyAndTheSnapshot()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/openapi/v1.json", playerId: null);

        JsonNode post = response.Json()["paths"]![_path]!["post"]!;
        Assert.Contains("\"#/components/schemas/ReenergizeCouriersRequest\"", post["requestBody"]!["content"]!["application/json"]!["schema"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.EndsWith("/CitySnapshot", (string)post["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!, StringComparison.Ordinal);
    }

    /// <summary>A server copy with a store holding <see cref="City"/>, settled and seen at <see cref="_settledAt"/>, and the given clock.</summary>
    /// <param name="clock">The server clock.</param>
    /// <param name="resting">Whether courier 4/1 rests (no stamina, 4 logs) or walks its route (full stamina).</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(FakeTimeProvider clock, bool resting)
    {
        var store = new FakeCityStore();
        store.Add(City(ContentOf(factory), resting));
        return (store, store.Serve(factory, clock));
    }

    /// <summary>
    /// The city of the tests at clock 0: 100 coins; house 1 at (0,0) (4 workers); woodcutter 2 at (2,0), idle without workers, with 30 logs in
    /// its store; warehouse 3 at (2,2) with 5 logs; courier station 4 at (0,2) with 1 worker, courier 4/1 on the route woodcutter → warehouse:
    /// resting with no stamina and 4 of the woodcutter's logs (26 left there), or walking to the woodcutter with all of its stamina.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <param name="resting">Whether the courier rests.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content, bool resting)
    {
        var logs = new ItemId("logs");
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(logs, resting ? 26 : 30)]),
            new(3, new BuildingTypeId("warehouse"), 2, 2, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(logs, 5)]),
            new(4, new BuildingTypeId("courier_station"), 0, 2, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
        ];
        Courier courier = resting
            ? new Courier(4, 1, CourierState.Resting, 0, null, null, 0, [new CourierLoad(logs, 2, 4)])
            : new Courier(4, 1, CourierState.Walking, 1, 0, 20, 14_400);
        Route[] routes =
        [
            new Route(4, 1, [
                new RouteStop(2, [new RouteAction(RouteActionKind.Take, logs, Amount.Preset(new AmountPresetId("all")))]),
                new RouteStop(3, [new RouteAction(RouteActionKind.Put, logs, Amount.Preset(new AmountPresetId("all")))])]),
        ];
        var state = new CityState(0, era: 1, nextBuildingId: 5, purse: 100, buildings, [], activeResearch: null, couriers: [courier], routes: routes);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c91"), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), 0, content.ContentVersion);
    }
}
