using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.Roads;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// <c>POST /api/v1/commands/place-roads</c>, <c>upgrade-roads</c> and <c>remove-roads</c> end to end on PostgreSQL over HTTP (CR-0008-T1 part
/// D): the server's real content (an 8 × 8 grid; Dirt path, 5 coins a tile; Log road, 15 coins a tile to upgrade to, the last level),
/// endpoints, pipeline and repositories, the server clock a <c>FakeTimeProvider</c> at <see cref="PostgresApiFactory.Start"/>. Each test
/// stores a city built by hand at clock 0 (a house at (0,0), a woodcutter at (1,1); <see cref="HandBuiltCities.StoreCityWithRoadsAsync"/>)
/// and checks what was stored, not only the status: the <c>city_roads</c> rows, the stock, the ledger and the receipts.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class RoadCommandsDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The place endpoint.</summary>
    private const string _place = "/api/v1/commands/place-roads";

    /// <summary>The upgrade endpoint.</summary>
    private const string _upgrade = "/api/v1/commands/upgrade-roads";

    /// <summary>The remove endpoint.</summary>
    private const string _remove = "/api/v1/commands/remove-roads";

    /// <summary>The request id most tests send.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000004c1");

    /// <summary>The roads of the refusal tests' city: a Dirt path at (2,2) and a Log road (the last level) at (4,2).</summary>
    private static readonly RoadTile[] _twoRoads = [new RoadTile(2, 2, 1), new RoadTile(4, 2, 2)];

    /// <summary>
    /// Placing three roads stores three <c>city_roads</c> rows at level 1, pays 15 coins as one <c>COST_ROAD</c> row with the request id as
    /// reference, and stores the receipt with the response and the payload's hash. A later <c>GET /city</c> shows the roads, sorted.
    /// </summary>
    [Fact]
    public async Task Post_PlaceThreeRoads_StoresTheRowsTheLedgerAndTheReceipt()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "builder", 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));

        ApiResponse response = await PostAsync(_place, player, _requestId, """[{"x":4,"y":2},{"x":2,"y":2},{"x":3,"y":2}]""");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("(2,2) L1, (3,2) L1, (4,2) L1", await RoadsAsync(city));
        Assert.Equal([$"coins -15 985 COST_ROAD command {_requestId}"], (await LedgerAsync(Database, city)).Select(Line));
        Assert.Equal("coins 985, logs 100, planks 50", await StockAsync(Database, city));
        CommandReceiptRow receipt = Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal((_requestId, PlaceRoadsCommand.Type, response.Body), (receipt.RequestId, receipt.CommandType, receipt.ResponseJson));
        Assert.Equal(
            CommandPayload.Hash(new PlaceRoadsCommand(_requestId, [new CommandTile(4, 2), new CommandTile(2, 2), new CommandTile(3, 2)])),
            receipt.PayloadHash);

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        JsonNode later = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        Assert.Equal("""[{"x":2,"y":2,"level":1},{"x":3,"y":2,"level":1},{"x":4,"y":2,"level":1}]""", later["city"]!["roads"]!.ToJsonString());
    }

    /// <summary>Upgrading two Dirt paths stores them at level 2 and pays 30 coins (15 a tile) as one <c>COST_ROAD</c> row.</summary>
    [Fact]
    public async Task Post_UpgradeTwoRoads_RaisesTheStoredLevelsAndPays()
    {
        (Guid player, Guid city) = await StoreCityWithRoadsAsync(
            Factory, "builder", 1000, [new RoadTile(2, 2, 1), new RoadTile(3, 2, 1), new RoadTile(4, 2, 2)], House(1));

        ApiResponse response = await PostAsync(_upgrade, player, _requestId, """[{"x":3,"y":2},{"x":2,"y":2}]""");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("(2,2) L2, (3,2) L2, (4,2) L2", await RoadsAsync(city));
        Assert.Equal([$"coins -30 970 COST_ROAD command {_requestId}"], (await LedgerAsync(Database, city)).Select(Line));
        Assert.Equal(UpgradeRoadsCommand.Type, Assert.Single(await ReceiptsAsync(Database, player)).CommandType);
    }

    /// <summary>
    /// Removing a road deletes its row and nothing else: no coins back (CR-0008 D6), so no ledger row; the receipt is stored. The freed tile
    /// then takes a building.
    /// </summary>
    [Fact]
    public async Task Post_RemoveARoad_DeletesItsRowAndFreesTheTile()
    {
        (Guid player, Guid city) = await StoreCityWithRoadsAsync(Factory, "builder", 1000, _twoRoads, House(1));

        ApiResponse response = await PostAsync(_remove, player, _requestId, """[{"x":4,"y":2}]""");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("(2,2) L1", await RoadsAsync(city));
        Assert.Empty(await LedgerAsync(Database, city));
        Assert.Equal("coins 1000, logs 100, planks 50", await StockAsync(Database, city));
        Assert.Equal(RemoveRoadsCommand.Type, Assert.Single(await ReceiptsAsync(Database, player)).CommandType);

        ApiResponse place = await ApiClient.PostAsync(
            Factory, "/api/v1/commands/place-building", player, $$"""{"requestId":"{{Guid.CreateVersion7()}}","buildingTypeId":"house","x":4,"y":2}""");
        Assert.Equal(HttpStatusCode.OK, place.Status);
    }

    /// <summary>
    /// A building can't be placed on a road (<c>04</c> §8, CR-0008): <c>place-building</c> on the Dirt path at (2,2) is <c>TILE_OCCUPIED</c>
    /// (409), and nothing is stored.
    /// </summary>
    [Fact]
    public async Task Post_PlaceBuildingOnARoad_Is409TileOccupiedAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityWithRoadsAsync(Factory, "builder", 1000, _twoRoads, House(1));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(
            Factory, "/api/v1/commands/place-building", player, $$"""{"requestId":"{{_requestId}}","buildingTypeId":"house","x":2,"y":2}""");

        Assert.Equal((HttpStatusCode.Conflict, "TILE_OCCUPIED"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Every rule's refusal on the real server: the code, the status and the request id, and the city stored exactly as before (no time has
    /// elapsed, so not even a settlement is written). A house or a road in the way, a tile past the grid's edge (also after a free one), 15
    /// coins for three tiles with 14, a tile without a road, a road at its last level, 15 coins for an upgrade with 14, and a remove list with a
    /// tile without a road.
    /// </summary>
    /// <param name="path">The endpoint.</param>
    /// <param name="tiles">The tiles sent.</param>
    /// <param name="coins">The city's coins.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(_place, """[{"x":0,"y":0}]""", 1000, HttpStatusCode.Conflict, "TILE_OCCUPIED")]
    [InlineData(_place, """[{"x":5,"y":5},{"x":2,"y":2}]""", 1000, HttpStatusCode.Conflict, "TILE_OCCUPIED")]
    [InlineData(_place, """[{"x":5,"y":5},{"x":8,"y":5}]""", 1000, (HttpStatusCode)422, "OUT_OF_BOUNDS")]
    [InlineData(_place, """[{"x":5,"y":5},{"x":5,"y":6},{"x":5,"y":7}]""", 14, (HttpStatusCode)422, "INSUFFICIENT_RESOURCE")]
    [InlineData(_upgrade, """[{"x":5,"y":5}]""", 1000, HttpStatusCode.NotFound, "ROAD_NOT_FOUND")]
    [InlineData(_upgrade, """[{"x":4,"y":2}]""", 1000, (HttpStatusCode)422, "MAX_LEVEL")]
    [InlineData(_upgrade, """[{"x":2,"y":2}]""", 14, (HttpStatusCode)422, "INSUFFICIENT_RESOURCE")]
    [InlineData(_remove, """[{"x":2,"y":2},{"x":5,"y":5}]""", 1000, HttpStatusCode.NotFound, "ROAD_NOT_FOUND")]
    public async Task Post_RuleRefuses_IsProblemAndStoresNothing(string path, string tiles, long coins, HttpStatusCode status, string code)
    {
        (Guid player, Guid city) = await StoreCityWithRoadsAsync(
            Factory, "builder", coins, _twoRoads, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await PostAsync(path, player, _requestId, tiles);

        Assert.Equal(status, response.Status);
        JsonNode problem = response.Json();
        Assert.Equal((code, _requestId.ToString()), ((string)problem["code"]!, (string)problem["requestId"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A tile list no city could take is <c>INVALID_ARGUMENT</c> (400) on the real server before the city is locked (OD-13): five minutes have
    /// passed, and still nothing is stored, not even the settle.
    /// </summary>
    /// <param name="path">The endpoint.</param>
    /// <param name="tiles">The <c>tiles</c> JSON sent.</param>
    [Theory]
    [InlineData(_place, "[]")]
    [InlineData(_place, """[{"x":5,"y":5},{"x":5,"y":5}]""")]
    [InlineData(_place, """[{"x":-1,"y":5}]""")]
    [InlineData(_upgrade, "[]")]
    [InlineData(_upgrade, """[{"x":2,"y":2},{"x":2,"y":2}]""")]
    [InlineData(_upgrade, """[{"x":2,"y":-2}]""")]
    [InlineData(_remove, "[]")]
    [InlineData(_remove, """[{"x":2,"y":2},{"x":2,"y":2}]""")]
    [InlineData(_remove, """[{"x":-2,"y":2}]""")]
    public async Task Security_TileListNoCityCouldTake_Is400AndStoresNothing(string path, string tiles)
    {
        (Guid player, Guid city) = await StoreCityWithRoadsAsync(
            Factory, "attacker", 1000, _twoRoads, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await PostAsync(path, player, _requestId, tiles);

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_ARGUMENT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A tile with a level of the client's choosing is refused (400) on the real server too, and nothing is stored, although time has passed:
    /// a road's level comes only from its upgrades.
    /// </summary>
    [Fact]
    public async Task Security_UpgradeWithALevelField_Is400AndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityWithRoadsAsync(Factory, "attacker", 1000, _twoRoads, House(1));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await PostAsync(_upgrade, player, _requestId, """[{"x":2,"y":2,"level":9}]""");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>A retry with the same request id and body a minute later answers with the stored bytes and writes nothing: no second payment.</summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "builder", 1000, House(1));
        ApiResponse first = await PostAsync(_place, player, _requestId, """[{"x":5,"y":5}]""");
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await PostAsync(_place, player, _requestId, """[{"x":5,"y":5}]""");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.Status, retry.Status));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>The same request id with other tiles is <c>IDEMPOTENCY_CONFLICT</c> (409) and writes nothing.</summary>
    [Fact]
    public async Task Post_SameRequestIdWithOtherTiles_Is409AndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "builder", 1000, House(1));
        await PostAsync(_place, player, _requestId, """[{"x":5,"y":5}]""");
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await PostAsync(_place, player, _requestId, """[{"x":6,"y":5}]""");

        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Two identical requests at once (one request id, one body): the city's lock puts them one after the other, the second finds the first's
    /// receipt and replays it, so both answer 200 with one text; one row, one <c>COST_ROAD</c> row, one receipt.
    /// </summary>
    [Fact]
    public async Task Post_TwoIdenticalRequestsAtOnce_PayOnce()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "builder", 1000, House(1));

        ApiResponse[] responses = await Task.WhenAll(
            PostAsync(_place, player, _requestId, """[{"x":5,"y":5}]"""), PostAsync(_place, player, _requestId, """[{"x":5,"y":5}]"""));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.Status));
        Assert.Equal(responses[0].Body, responses[1].Body);
        Assert.Equal("(5,5) L1", await RoadsAsync(city));
        Assert.Equal([$"coins -5 995 COST_ROAD command {_requestId}"], (await LedgerAsync(Database, city)).Select(Line));
        Assert.Single(await ReceiptsAsync(Database, player));
    }

    /// <summary>
    /// Two requests for the same tile at once (two request ids): the lock puts them one after the other, so exactly one places the road and
    /// the other is <c>TILE_OCCUPIED</c>; one row, one payment, one receipt.
    /// </summary>
    [Fact]
    public async Task Security_TwoRequestsForOneTileAtOnce_PlaceItOnce()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "builder", 1000, House(1));

        ApiResponse[] responses = await Task.WhenAll(
            PostAsync(_place, player, Guid.CreateVersion7(), """[{"x":5,"y":5}]"""), PostAsync(_place, player, Guid.CreateVersion7(), """[{"x":5,"y":5}]"""));

        Assert.Equal(
            ["OK", "TILE_OCCUPIED"],
            responses.Select(response => response.Status == HttpStatusCode.OK ? "OK" : (string)response.Json()["code"]!).Order(StringComparer.Ordinal));
        Assert.Equal("(5,5) L1", await RoadsAsync(city));
        Assert.Single(await LedgerAsync(Database, city));
        Assert.Single(await ReceiptsAsync(Database, player));
    }

    /// <summary>
    /// A request id names a receipt within its own player only: another player's command with the same request id runs for the caller, in the
    /// caller's city, and the first player's city and receipt stay as they were.
    /// </summary>
    [Fact]
    public async Task Security_AnotherPlayersRequestId_RunsForTheCallerOnly()
    {
        (Guid owner, Guid ownerCity) = await StoreCityAsync(Factory, "owner", 1000, House(1));
        (Guid other, Guid otherCity) = await StoreCityAsync(Factory, "other", 1000, House(1));
        await PostAsync(_place, owner, _requestId, """[{"x":5,"y":5}]""");
        string ownerStored = await StoredAsync(Factory, Database, owner, ownerCity);

        ApiResponse response = await PostAsync(_place, other, _requestId, """[{"x":6,"y":6},{"x":6,"y":7}]""");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("(6,6) L1, (6,7) L1", await RoadsAsync(otherCity));
        Assert.Equal(ownerStored, await StoredAsync(Factory, Database, owner, ownerCity));
    }

    /// <summary>
    /// The settle runs before the mutation, in the same transaction (<c>02</c> rule 4): a woodcutter with 2 workers has been producing since
    /// clock 0 (60 labor, end 30); 30 s later a road is placed. The ledger holds the settlement's log first, then the road's coins (decision
    /// 042), and the city's clock is 30.
    /// </summary>
    [Fact]
    public async Task Post_AfterTimePassed_SettlesBeforeThePlacement()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "builder", 1000, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2));
        Factory.Clock.Advance(TimeSpan.FromSeconds(30));

        ApiResponse response = await PostAsync(_place, player, _requestId, """[{"x":5,"y":5}]""");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(30L, (long)response.Json()["city"]!["clock"]!);
        Assert.Equal(
            ["logs 1 101 SETTLEMENT_PRODUCED settlement", $"coins -5 995 COST_ROAD command {_requestId}"],
            (await LedgerAsync(Database, city)).Select(row => row.RefType == "settlement" ? Line(row)[..^(row.RefId!.Length + 1)] : Line(row)));
    }

    /// <summary>Sends a road command over HTTP with the player's token.</summary>
    /// <param name="path">The endpoint.</param>
    /// <param name="player">The acting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="tiles">The <c>tiles</c> JSON.</param>
    /// <returns>The response.</returns>
    private Task<ApiResponse> PostAsync(string path, Guid player, Guid requestId, string tiles) =>
        ApiClient.PostAsync(Factory, path, player, $$"""{"requestId":"{{requestId}}","tiles":{{tiles}}}""");

    /// <summary>A city's stored <c>city_roads</c> rows as text, by column then row, read by a context of the test's own.</summary>
    /// <param name="city">The city.</param>
    /// <returns>For example <c>(2,2) L1, (4,2) L2</c>.</returns>
    private async Task<string> RoadsAsync(Guid city)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityRoadRow> rows = await db.CityRoads.AsNoTracking().Where(row => row.CityId == city).ToListAsync(TestContext.Current.CancellationToken);
        return string.Join(", ", rows.OrderBy(row => row.X).ThenBy(row => row.Y).Select(row => $"({row.X},{row.Y}) L{row.Level}"));
    }
}
