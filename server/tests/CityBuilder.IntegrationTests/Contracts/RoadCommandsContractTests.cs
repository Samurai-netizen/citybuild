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
/// <c>POST /api/v1/commands/place-roads</c>, <c>upgrade-roads</c> and <c>remove-roads</c> against the contract
/// (<c>core-instructions/05-api-contract.md</c>, CR-0008) on the raw JSON, over a small city in a <see cref="FakeCityStore"/> on the server's
/// content (road levels: Dirt path, 5 coins a tile; Log road, 15 coins a tile to upgrade to): the real endpoints, pipeline, engine and
/// serializer, no database. It covers what the body may hold, the boundary checks before the lock (OD-13) and what the answers look like;
/// the stored rows are checked on PostgreSQL in <c>RoadCommandsDatabaseTests</c>. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy with its city and clock.</param>
public sealed class RoadCommandsContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The place endpoint.</summary>
    private const string _place = "/api/v1/commands/place-roads";

    /// <summary>The upgrade endpoint.</summary>
    private const string _upgrade = "/api/v1/commands/upgrade-roads";

    /// <summary>The remove endpoint.</summary>
    private const string _remove = "/api/v1/commands/remove-roads";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-00000000000f";

    /// <summary>The server second the city was last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player of the city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a7e");

    /// <summary>The three endpoints, for the theories that hold for each.</summary>
    public static TheoryData<string> Paths => [_place, _upgrade, _remove];

    /// <summary>
    /// Each endpoint with each tile list that no city could take (OD-13): none, 65 tiles, a tile twice, a negative column or row, a null tile,
    /// and <c>tiles</c> null.
    /// </summary>
    public static TheoryData<string, string> BoundaryRefusals
    {
        get
        {
            string tooMany = "[" + string.Join(",", Enumerable.Range(0, 65).Select(index => $$"""{"x":{{index % 8}},"y":{{index / 8}}}""")) + "]";
            var data = new TheoryData<string, string>();
            foreach (string path in new[] { _place, _upgrade, _remove })
            {
                foreach (string tiles in new[]
                {
                    "[]", tooMany, """[{"x":4,"y":4},{"x":4,"y":4}]""", """[{"x":-1,"y":4}]""", """[{"x":4,"y":-1}]""",
                    """[{"x":4,"y":4},null]""", "null",
                })
                {
                    data.Add(path, tiles);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Placing two roads answers 200 with the snapshot of the city as it is saved: the request id, the next version, the five roads sorted by
    /// column then row with the new ones at level 1, the coins 10 lower (5 a tile), no builder taken. It is the same snapshot <c>GET /city</c>
    /// shows, and the saved ledger holds one <c>COST_ROAD</c> row of −10 coins with the request id.
    /// </summary>
    [Fact]
    public async Task Post_PlaceRoadsOnTwoFreeTiles_PaysAndShowsThemInTheSameResponse()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _place, _playerId, Body("""[{"x":3,"y":2},{"x":0,"y":2}]"""));

        Assert.Equal((HttpStatusCode.OK, "application/json; charset=utf-8"), (response.Status, response.ContentType));
        JsonNode snapshot = response.Json();
        Assert.Equal((_requestId, 1L), ((string)snapshot["requestId"]!, (long)snapshot["stateVersion"]!));
        Assert.Equal(
            """[{"x":0,"y":2,"level":1},{"x":1,"y":1,"level":1},{"x":2,"y":1,"level":2},{"x":3,"y":1,"level":1},{"x":3,"y":2,"level":1}]""",
            snapshot["city"]!["roads"]!.ToJsonString());
        Assert.Equal("""{"coins":10,"logs":40,"planks":10}""", snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal("""{"slots":2,"busy":0}""", snapshot["city"]!["builders"]!.ToJsonString());
        Assert.Equal(1, store.SaveCount);
        LedgerEntry cost = Assert.Single(store.Ledger);
        Assert.Equal((LedgerReason.CostRoad, "coins", -10L, 10L, _requestId), (cost.Reason, cost.Item.Value, cost.Delta, cost.BalanceAfter, cost.RefId));

        JsonNode city = (await ApiClient.GetAsync(server, "/api/v1/city", _playerId)).Json();
        Assert.Equal(city["city"]!.ToJsonString(), snapshot["city"]!.ToJsonString());
    }

    /// <summary>Upgrading the level-1 road at (1, 1) answers 200 with it at level 2 and the coins 15 lower; one <c>COST_ROAD</c> row of −15.</summary>
    [Fact]
    public async Task Post_UpgradeRoadOnALevelOneTile_RaisesItAndPays()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _upgrade, _playerId, Body("""[{"x":1,"y":1}]"""));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode city = response.Json()["city"]!;
        Assert.Equal("""[{"x":1,"y":1,"level":2},{"x":2,"y":1,"level":2},{"x":3,"y":1,"level":1}]""", city["roads"]!.ToJsonString());
        Assert.Equal(5L, (long)city["inventory"]!["coins"]!);
        LedgerEntry cost = Assert.Single(store.Ledger);
        Assert.Equal((LedgerReason.CostRoad, "coins", -15L, 5L), (cost.Reason, cost.Item.Value, cost.Delta, cost.BalanceAfter));
    }

    /// <summary>Removing two roads answers 200 without them; nothing is paid or refunded, so the coins stay and no ledger row is written.</summary>
    [Fact]
    public async Task Post_RemoveTwoRoads_TakesThemOffWithoutLedgerRows()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _remove, _playerId, Body("""[{"x":2,"y":1},{"x":1,"y":1}]"""));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode city = response.Json()["city"]!;
        Assert.Equal("""[{"x":3,"y":1,"level":1}]""", city["roads"]!.ToJsonString());
        Assert.Equal(20L, (long)city["inventory"]!["coins"]!);
        Assert.Equal(1, store.SaveCount);
        Assert.Empty(store.Ledger);
    }

    /// <summary>
    /// A retry with the same request id and body, a minute later, answers with the stored response byte for byte and runs nothing: no settle,
    /// no save, no second payment.
    /// </summary>
    /// <param name="path">The endpoint.</param>
    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndRunsNothing(string path)
    {
        FakeTimeProvider clock = new(_settledAt);
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(clock);
        await using WebApplicationFactory<Program> _ = server;
        string body = Body(path == _place ? """[{"x":4,"y":4}]""" : """[{"x":1,"y":1}]""");
        ApiResponse first = await ApiClient.PostAsync(server, path, _playerId, body);
        clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await ApiClient.PostAsync(server, path, _playerId, body);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal((HttpStatusCode.OK, first.Body), (retry.Status, retry.Body));
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0, store.Stored(_playerId).State.Clock);
    }

    /// <summary>
    /// The same request id with other tiles, the same tiles in another order, or on another road command is <c>IDEMPOTENCY_CONFLICT</c>
    /// (409): the data, its order and the command type are the payload. Nothing runs.
    /// </summary>
    /// <param name="path">The endpoint of the second request.</param>
    /// <param name="tiles">The second request's tiles.</param>
    [Theory]
    [InlineData(_place, """[{"x":4,"y":4}]""")]
    [InlineData(_place, """[{"x":5,"y":4},{"x":4,"y":4}]""")]
    [InlineData(_remove, """[{"x":4,"y":4},{"x":5,"y":4}]""")]
    public async Task Post_SameRequestIdWithAnotherPayload_Is409IdempotencyConflict(string path, string tiles)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;
        Assert.Equal(HttpStatusCode.OK, (await ApiClient.PostAsync(server, _place, _playerId, Body("""[{"x":4,"y":4},{"x":5,"y":4}]"""))).Status);

        ApiResponse response = await ApiClient.PostAsync(server, path, _playerId, Body(tiles));

        AssertProblem(response, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT", _requestId);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>
    /// A rule's refusal is Problem Details with the rule's code and status and the request id, and with no time elapsed nothing is saved. The
    /// checks run over the whole list in the order of <c>04</c> §8: a tile outside the 8 × 8 grid is <c>OUT_OF_BOUNDS</c> also when an
    /// occupied tile comes first; a building or a road in the way is <c>TILE_OCCUPIED</c>; 25 coins for five tiles with 20 is
    /// <c>INSUFFICIENT_RESOURCE</c>; a tile without a road is <c>ROAD_NOT_FOUND</c> also after a road at its last level; a road at its last
    /// level is <c>MAX_LEVEL</c>; 30 coins for two upgrades is <c>INSUFFICIENT_RESOURCE</c>.
    /// </summary>
    /// <param name="path">The endpoint.</param>
    /// <param name="tiles">The tiles sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(_place, """[{"x":8,"y":0}]""", (HttpStatusCode)422, "OUT_OF_BOUNDS")]
    [InlineData(_place, """[{"x":0,"y":0},{"x":0,"y":8}]""", (HttpStatusCode)422, "OUT_OF_BOUNDS")]
    [InlineData(_place, """[{"x":4,"y":4},{"x":0,"y":0}]""", HttpStatusCode.Conflict, "TILE_OCCUPIED")]
    [InlineData(_place, """[{"x":1,"y":1}]""", HttpStatusCode.Conflict, "TILE_OCCUPIED")]
    [InlineData(_place, """[{"x":4,"y":0},{"x":4,"y":1},{"x":4,"y":2},{"x":4,"y":3},{"x":4,"y":4}]""", (HttpStatusCode)422, "INSUFFICIENT_RESOURCE")]
    [InlineData(_upgrade, """[{"x":5,"y":5}]""", HttpStatusCode.NotFound, "ROAD_NOT_FOUND")]
    [InlineData(_upgrade, """[{"x":2,"y":1},{"x":5,"y":5}]""", HttpStatusCode.NotFound, "ROAD_NOT_FOUND")]
    [InlineData(_upgrade, """[{"x":0,"y":0}]""", HttpStatusCode.NotFound, "ROAD_NOT_FOUND")]
    [InlineData(_upgrade, """[{"x":2,"y":1}]""", (HttpStatusCode)422, "MAX_LEVEL")]
    [InlineData(_upgrade, """[{"x":1,"y":1},{"x":3,"y":1}]""", (HttpStatusCode)422, "INSUFFICIENT_RESOURCE")]
    [InlineData(_remove, """[{"x":1,"y":1},{"x":5,"y":5}]""", HttpStatusCode.NotFound, "ROAD_NOT_FOUND")]
    [InlineData(_remove, """[{"x":100,"y":100}]""", HttpStatusCode.NotFound, "ROAD_NOT_FOUND")]
    public async Task Post_RuleRefuses_IsProblemWithTheRulesCode(string path, string tiles, HttpStatusCode status, string code)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, path, _playerId, Body(tiles));

        AssertProblem(response, status, code, _requestId);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A tile list no city could take is <c>INVALID_ARGUMENT</c> (400) with the request id before the city is locked (owner directive OD-13):
    /// the store was never asked for the city, and nothing is saved.
    /// </summary>
    /// <param name="path">The endpoint.</param>
    /// <param name="tiles">The <c>tiles</c> JSON sent.</param>
    [Theory]
    [MemberData(nameof(BoundaryRefusals))]
    public async Task Security_TileListNoCityCouldTake_Is400BeforeTheCityIsLocked(string path, string tiles)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, path, _playerId, Body(tiles));

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", _requestId);
        Assert.Equal(0, store.LockCount);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body or a tile with a property the contract doesn't have is refused (400) before anything runs, whatever it claims: the server never
    /// takes a level, a cost, a player or a single tile outside <c>tiles</c> from the client, and never silently ignores one either.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4,"y":4}],"level":2}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4,"y":4}],"cost":0}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4,"y":4}],"x":4,"y":4}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4,"y":4}],"playerId":"0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6c"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4,"y":4,"level":2}]}""")]
    [InlineData($$$"""{"requestId":"{{{_requestId}}}","tiles":[{"x":4,"y":4,"cost":{"coins":0}}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4,"y":4,"z":0}]}""")]
    public async Task Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _place, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body that isn't the contract's shape is refused (400) before anything runs: a missing field (a tile without <c>y</c> must not read as
    /// row 0), a wrong type, broken JSON.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData("""{"tiles":[{"x":4,"y":4}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"y":4}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":"4","y":4}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4.5,"y":4}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":null,"y":4}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":2147483648,"y":4}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[[4,4]]}""")]
    [InlineData($$$"""{"requestId":"{{{_requestId}}}","tiles":{"x":4,"y":4}}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":"4,4"}""")]
    [InlineData("""{"requestId":"not-a-uuid","tiles":[{"x":4,"y":4}]}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","tiles":[{"x":4,"y":4}]""")]
    public async Task Post_BodyOfTheWrongShape_Is400AndRunsNothing(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _place, _playerId, body);

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
    [InlineData("""{"requestId":"00000000-0000-0000-0000-000000000000","tiles":[{"x":4,"y":4}]}""")]
    public async Task Post_BodyThatIsNoCommand_Is400InvalidArgument(string? body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(_settledAt);
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _place, _playerId, body);

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId: null);
        AssertNothingRan(store);
    }

    /// <summary>
    /// The OpenAPI document (Development) describes each endpoint for client authors: the request body is <c>RoadTilesRequest</c> and the
    /// 200 body is the snapshot, <c>CitySnapshot</c>, although the handler writes the snapshot as text.
    /// </summary>
    /// <param name="path">The endpoint.</param>
    [Theory]
    [MemberData(nameof(Paths))]
    public async Task OpenApi_RoadCommand_DescribesTheBodyAndTheSnapshot(string path)
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/openapi/v1.json", playerId: null);

        JsonNode post = response.Json()["paths"]![path]!["post"]!;
        // The body parameter is nullable (no body is INVALID_ARGUMENT), so the schema is oneOf null or the request.
        Assert.Contains("\"#/components/schemas/RoadTilesRequest\"", post["requestBody"]!["content"]!["application/json"]!["schema"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.EndsWith("/CitySnapshot", (string)post["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!, StringComparison.Ordinal);
    }

    /// <summary>A body with the test's request id and the tiles.</summary>
    /// <param name="tiles">The <c>tiles</c> JSON.</param>
    /// <returns>The JSON body.</returns>
    private static string Body(string tiles) => $$"""{"requestId":"{{_requestId}}","tiles":{{tiles}}}""";

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
    /// The city of the tests at clock 0: 20 coins, 40 logs, 10 planks; house 1 at (0,0) and woodcutter 2 at (2,0), both completed and idle
    /// without workers; roads at (1,1) and (3,1) at level 1 and at (2,1) at level 2, the content's last.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <returns>The city at state version 0.</returns>
    private static CityAggregate City(GameContent content)
    {
        Building[] buildings =
        [
            new(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            new(2, new BuildingTypeId("woodcutter"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
        ];
        var state = new CityState(
            0,
            era: 1,
            nextBuildingId: 3,
            [new(new ItemId("coins"), 20), new(new ItemId("logs"), 40), new(new ItemId("planks"), 10)],
            buildings,
            [],
            activeResearch: null,
            [new RoadTile(1, 1, 1), new RoadTile(3, 1, 1), new RoadTile(2, 1, 2)]);
        return new CityAggregate(
            Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c2a"), _playerId, state, [], _settledAt.ToUnixTimeSeconds(), 0, content.ContentVersion);
    }

    /// <summary>Asserts that the store saved nothing: the city is the stored one at version 0 with its three roads and 20 coins.</summary>
    /// <param name="store">The store.</param>
    private static void AssertNothingRan(FakeCityStore store)
    {
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Ledger);
        CityAggregate city = store.Stored(_playerId);
        Assert.Equal(0, city.StateVersion);
        Assert.Equal([new RoadTile(1, 1, 1), new RoadTile(2, 1, 2), new RoadTile(3, 1, 1)], city.State.Roads);
        Assert.Equal(20, city.State.GetQuantity(new ItemId("coins")));
    }
}
