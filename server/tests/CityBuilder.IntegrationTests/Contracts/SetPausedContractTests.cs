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
/// <c>POST /api/v1/commands/set-paused</c> over HTTP (CR-0008 amendment 2, D23 and D24; <c>core-instructions/05-api-contract.md</c>), on the raw
/// JSON over a small city in a <see cref="FakeCityStore"/> on the server's content: the real endpoint, pipeline, engine and serializer, no
/// database. It covers <c>paused</c> and the <c>Paused</c> stall reason in the snapshot, a paused station's courier staying home, the
/// body's boundary checks (OD-13) and the rule's codes. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class SetPausedContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/set-paused";

    /// <summary>The request id of the first command.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-000000000031";

    /// <summary>The request id of a second command.</summary>
    private const string _secondRequestId = "0199a1b2-c3d4-7e5f-8a9b-000000000032";

    /// <summary>The server second the city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a91");

    /// <summary>
    /// Pausing the courier station: the snapshot says <c>paused</c> true and the stall reason <c>Paused</c>. Its courier had left for the
    /// woodcutter in the settle before the command; ten minutes later it has done that stop, given the logs back and is home, where it stays.
    /// </summary>
    [Fact]
    public async Task Post_PauseAStation_IsPausedAndItsCourierComesHomeEmpty()
    {
        var clock = new FakeTimeProvider(_settledAt);
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(_requestId, 4, paused: true));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode station = response.Json()["city"]!["buildings"]![3]!;
        Assert.True((bool)station["paused"]!);
        Assert.Equal("Paused", (string?)station["stallReason"]);
        Assert.Equal("Walking", (string)response.Json()["city"]!["couriers"]![0]!["state"]!);

        clock.Advance(TimeSpan.FromMinutes(10));
        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json()["city"]!;
        Assert.Equal("[]", city["couriers"]![0]!["load"]!.ToJsonString());
        Assert.Equal("AtStation", (string)city["couriers"]![0]!["state"]!);
        Assert.Equal("""{"logs":30}""", city["buildings"]![1]!["store"]!.ToJsonString());
    }

    /// <summary>Resuming the paused station: <c>paused</c> false, and its courier leaves for the route's first stop at once.</summary>
    [Fact]
    public async Task Post_ResumeAStation_ItsCourierLeaves()
    {
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;
        Assert.Equal(HttpStatusCode.OK, (await ApiClient.PostAsync(server, _path, _playerId, Body(_requestId, 4, paused: true))).Status);

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(_secondRequestId, 4, paused: false));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.False((bool)response.Json()["city"]!["buildings"]![3]!["paused"]!);
        Assert.Equal("Walking", (string)response.Json()["city"]!["couriers"]![0]!["state"]!);
    }

    /// <summary>Every building of a fresh city has <c>paused</c> false, also those that can't be paused.</summary>
    [Fact]
    public async Task GetCity_EveryBuilding_HasPausedFalse()
    {
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        Assert.All(response.Json()["city"]!["buildings"]!.AsArray(), building => Assert.False((bool)building!["paused"]!));
    }

    /// <summary>
    /// The rule's refusals: a building the city doesn't have (404), one that does no work (a house, a warehouse: 400), and one under
    /// construction (409); no building is paused afterwards (the settle before the command is still saved: the courier left).
    /// </summary>
    /// <param name="buildingId">The building sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(9, 404, "BUILDING_NOT_FOUND")]
    [InlineData(1, 400, "INVALID_ARGUMENT")]
    [InlineData(3, 400, "INVALID_ARGUMENT")]
    [InlineData(6, 409, "BUILDING_BUSY")]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(int buildingId, int status, string code)
    {
        (FakeCityStore _, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, Body(_requestId, buildingId, paused: true));

        AssertProblem(response, (HttpStatusCode)status, code, _requestId);
        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json()["city"]!;
        Assert.All(city["buildings"]!.AsArray(), building => Assert.False((bool)building!["paused"]!));
    }

    /// <summary>
    /// A body no city could take is <c>INVALID_ARGUMENT</c> (400) before the city is locked (OD-13): a building id below 1, a missing
    /// <c>paused</c>, a property the contract doesn't have, and a flag that is no JSON boolean.
    /// </summary>
    /// <param name="body">The body sent.</param>
    [Theory]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000031","buildingId":0,"paused":true}""")]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000031","buildingId":4}""")]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000031","buildingId":4,"paused":true,"until":60}""")]
    [InlineData("""{"requestId":"0199a1b2-c3d4-7e5f-8a9b-000000000031","buildingId":4,"paused":"true"}""")]
    public async Task Post_ShapeNoCityCouldTake_IsInvalidArgumentBeforeTheLock(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(new FakeTimeProvider(_settledAt));
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _path, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("INVALID_ARGUMENT", (string)response.Json()["code"]!);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>The JSON body of a set-paused.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="buildingId">The building id.</param>
    /// <param name="paused">The flag.</param>
    /// <returns>The body.</returns>
    private static string Body(string requestId, int buildingId, bool paused) =>
        $$"""{"requestId":"{{requestId}}","buildingId":{{buildingId}},"paused":{{(paused ? "true" : "false")}}}""";

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
    /// The city of the tests at clock 0: 100 coins; house 1 at (0,0); woodcutter 2 at (2,0) with 30 logs; warehouse 3 at (2,2); courier
    /// station 4 at (0,2) with 1 worker, courier 4/1 at the station with a route woodcutter → warehouse; Scriptorium 5 at (5,5); a woodcutter
    /// 6 under construction at (6,6).
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content)
    {
        var logs = new ItemId("logs");
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0, [new(logs, 30)]),
            new(3, new BuildingTypeId("warehouse"), 2, 2, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(4, new BuildingTypeId("courier_station"), 0, 2, 1, 1, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(5, new BuildingTypeId("scriptorium"), 5, 5, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(6, new BuildingTypeId("woodcutter"), 6, 6, 1, 0, BuildingPhase.Constructing, 0, 1000, 0, 0, 0, 0),
        ];
        Amount all = Amount.Preset(new AmountPresetId("all"));
        Route[] routes =
            [new Route(4, 1, [new RouteStop(2, [new RouteAction(RouteActionKind.Take, logs, all)]), new RouteStop(3, [new RouteAction(RouteActionKind.Put, logs, all)])])];
        var state = new CityState(
            0, era: 1, nextBuildingId: 7, purse: 100, buildings, [], activeResearch: null,
            couriers: [new Courier(4, 1, CourierState.AtStation, 0, null, null, 14_400)], routes: routes);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c91"), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), 0, content.ContentVersion);
    }
}
