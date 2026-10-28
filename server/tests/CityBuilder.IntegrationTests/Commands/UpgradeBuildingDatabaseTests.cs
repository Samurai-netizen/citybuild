using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.UpgradeBuilding;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// <c>POST /api/v1/commands/upgrade-building</c> end to end on PostgreSQL over HTTP: the server's real content (level 2 of a house: 120 coins,
/// 30 logs, 90 s, 4 → 6 workers provided; of a woodcutter: 150 coins, 20 planks, 120 s, max workers 2 → 4; of a sawmill: 250 coins,
/// 30 planks, 180 s; of a trading post: 400 coins, 40 planks, 300 s; every type's max level is 2; 2 builders; log and plank caps 500),
/// endpoint, pipeline and repositories, the server clock a <c>FakeTimeProvider</c> at <see cref="PostgresApiFactory.Start"/>. Each test stores
/// a city built by hand at clock 0 (<see cref="StoreCityAsync"/>) and checks what was stored, not only the status: the building rows, the
/// stock, the ledger rows and the receipts.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class UpgradeBuildingDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/upgrade-building";

    /// <summary>The request id most tests send.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000003a1");

    /// <summary>
    /// An idle woodcutter without workers: the stock pays the level-2 cost (1000 → 850 coins, 50 → 30 planks); the building row is
    /// <c>Upgrading</c> from clock 0 to its 120 seconds, still level 1, target level 2; the ledger holds one <c>COST_UPGRADE</c> row per item
    /// and no refund, referenced by the request id; the receipt holds the response text and the payload's hash; the response shows the stored
    /// version and a builder busy.
    /// </summary>
    [Fact]
    public async Task Post_IdleWoodcutter_PaysStartsTheUpgradeAndStoresTheReceipt()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        long versionBefore = (await LoadAsync(Factory, player)).StateVersion;

        ApiResponse response = await UpgradeAsync(player, _requestId, 2);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode snapshot = response.Json();
        await using CityBuilderDbContext db = Database.CreateContext();
        CityRow row = await db.Cities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((versionBefore + 1, 0L), (row.StateVersion, row.ClockSeconds));
        Assert.Equal(row.StateVersion, (long)snapshot["stateVersion"]!);
        Assert.Equal("coins 850, logs 100, planks 30", await StockAsync(city));
        Assert.Equal("""{"slots":2,"busy":1}""", snapshot["city"]!["builders"]!.ToJsonString());
        Assert.Equal("2 woodcutter L1 Upgrading 0-120 target 2 workers 0", await BuildingAsync(city, 2));

        Assert.Equal(
            ["coins -150 850 COST_UPGRADE command " + _requestId, "planks -20 30 COST_UPGRADE command " + _requestId],
            (await LedgerAsync(Database, city)).Select(Line));

        CommandReceiptRow receipt = Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal((_requestId, UpgradeBuildingCommand.Type, response.Body), (receipt.RequestId, receipt.CommandType, receipt.ResponseJson));
        Assert.Equal(CommandPayload.Hash(new UpgradeBuildingCommand(_requestId, 2)), receipt.PayloadHash);
    }

    /// <summary>
    /// A producing sawmill whose cycle took 2 logs while the stock sat at its 500-log cap: the upgrade ends the cycle without output and
    /// gives the 2 logs back, above the cap (502), as <c>REFUND_UPGRADE</c>; then it pays 250 coins and 30 planks as <c>COST_UPGRADE</c>. The
    /// rows follow that order with their balances, and the snapshot shows the stock above the cap.
    /// </summary>
    [Fact]
    public async Task Post_ProducingSawmill_RefundsItsInputsAboveTheCapThenPays()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 500, 50, House(1), Sawmill(2));

        ApiResponse response = await UpgradeAsync(player, _requestId, 2);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode snapshot = response.Json();
        Assert.Equal("""{"coins":750,"logs":502,"planks":20}""", snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal(500L, (long)snapshot["city"]!["storageCaps"]!["logs"]!);
        Assert.Equal("coins 750, logs 502, planks 20", await StockAsync(city));
        Assert.Equal("2 sawmill L1 Upgrading 0-180 target 2 workers 2", await BuildingAsync(city, 2));
        Assert.Equal(
            [
                "logs 2 502 REFUND_UPGRADE command " + _requestId,
                "coins -250 750 COST_UPGRADE command " + _requestId,
                "planks -30 20 COST_UPGRADE command " + _requestId,
            ],
            (await LedgerAsync(Database, city)).Select(Line));
    }

    /// <summary>
    /// The workers stay assigned during the upgrade and the max rises after it: a woodcutter with its 2 workers (the level-1 max) producing
    /// since clock 0 is upgraded at second 10. Its cycle ends without a log; at second 60 it is <c>Upgrading</c> with the 2 workers kept
    /// (<c>maxWorkers</c> still level 1's 2, the workers block still counting them); at second 130 it is level 2 with the level's max of 4, the same
    /// 2 workers, and a new cycle running from 130. No log was made in all that time.
    /// </summary>
    [Fact]
    public async Task Post_StaffedWoodcutter_KeepsItsWorkersAndHasTheHigherMaxAfter()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2));
        Factory.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.OK, (await UpgradeAsync(player, _requestId, 2)).Status);
        Factory.Clock.Advance(TimeSpan.FromSeconds(50));

        JsonNode during = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(70));
        JsonNode after = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal(("Upgrading", 1, 2, 2), Seen(during));
        Assert.Equal("""{"provided":4,"assigned":2,"free":2}""", during["city"]!["workers"]!.ToJsonString());
        Assert.Equal(("Producing", 2, 2, 4), Seen(after));
        Assert.Equal("""{"provided":4,"assigned":2,"free":2}""", after["city"]!["workers"]!.ToJsonString());
        Assert.Equal("2 woodcutter L2 Producing 130-160 target 0 workers 2", await BuildingAsync(city, 2));
        Assert.Equal("coins 850, logs 100, planks 30", await StockAsync(city));

        // The woodcutter in a snapshot: its phase, level, workers and max workers.
        static (string, int, int, int) Seen(JsonNode snapshot)
        {
            JsonNode building = snapshot["city"]!["buildings"]![1]!;
            return ((string)building["phase"]!, (int)building["level"]!, (int)building["assignedWorkers"]!, (int)building["maxWorkers"]!);
        }
    }

    /// <summary>
    /// A house upgrade raises the workers provided on completion, not before: upgraded at clock 0 (90 s), the house still provides its
    /// level-1 4 workers at second 89 and provides level 2's 6 at second 90, when it is level 2 and <c>Idle</c>, also in its stored row.
    /// </summary>
    [Fact]
    public async Task Post_House_RaisesTheWorkersProvidedWhenTheUpgradeCompletes()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1));

        JsonNode started = (await UpgradeAsync(player, _requestId, 1)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(89));
        JsonNode before = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        JsonNode completed = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal(4, (int)started["city"]!["workers"]!["provided"]!);
        Assert.Equal((4, "Upgrading"), ((int)before["city"]!["workers"]!["provided"]!, (string)before["city"]!["buildings"]![0]!["phase"]!));
        Assert.Equal((6, "Idle", 2), ((int)completed["city"]!["workers"]!["provided"]!, (string)completed["city"]!["buildings"]![0]!["phase"]!, (int)completed["city"]!["buildings"]![0]!["level"]!));
        Assert.Equal("[1]", completed["settlement"]!["completedBuildings"]!.ToJsonString());
        Assert.Equal("1 house L2 Idle - target 0 workers 0", await BuildingAsync(city, 1));
    }

    /// <summary>An id the city doesn't have, whatever its value, is <c>BUILDING_NOT_FOUND</c> (404) and stores nothing.</summary>
    /// <param name="buildingId">The id sent.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task Post_IdNotInTheCity_Is404AndStoresNothing(int buildingId)
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));

        await AssertRefusedAsync(player, city, buildingId, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND");
    }

    /// <summary>
    /// Another player's building id is <c>BUILDING_NOT_FOUND</c> (404), never a sign that it exists: the second player has buildings 1 and 2,
    /// the first player's woodcutter is building 3. The second player's upgrade of 3 is refused and stores nothing, and the first player's
    /// city is untouched (its woodcutter still idle at level 1, its stock, ledger and receipts as they were).
    /// </summary>
    [Fact]
    public async Task Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity()
    {
        (Guid owner, Guid ownerCity) = await StoreCityAsync("owner", 1000, 100, 50, House(1), House(2, x: 1), Woodcutter(3, BuildingPhase.Idle, workers: 0));
        (Guid attacker, Guid attackerCity) = await StoreCityAsync("attacker", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        string ownerStored = await StoredAsync(Factory, Database, owner, ownerCity);

        await AssertRefusedAsync(attacker, attackerCity, 3, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND");

        Assert.Equal(ownerStored, await StoredAsync(Factory, Database, owner, ownerCity));
        Assert.Equal("3 woodcutter L1 Idle - target 0 workers 0", await BuildingAsync(ownerCity, 3));
    }

    /// <summary>
    /// A building under construction is <c>BUILDING_BUSY</c> (409) until the settle completes it: the woodcutter is built until second 60; at
    /// second 59 the upgrade is refused and stores nothing but the settlement, and at second 60 the settle completes the construction first,
    /// so the same upgrade starts, from clock 60.
    /// </summary>
    [Fact]
    public async Task Post_WhileConstructing_IsBusyUntilTheSettleCompletesTheConstruction()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Constructing, workers: 0));
        Factory.Clock.Advance(TimeSpan.FromSeconds(59));

        ApiResponse tooEarly = await UpgradeAsync(player, Guid.CreateVersion7(), 2);
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        ApiResponse response = await UpgradeAsync(player, _requestId, 2);

        Assert.Equal((HttpStatusCode.Conflict, "BUILDING_BUSY"), (tooEarly.Status, (string)tooEarly.Json()["code"]!));
        Assert.Contains("#2 woodcutter (1,1) L1 w0 Constructing 0..60", stored, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("[2]", response.Json()["settlement"]!["completedBuildings"]!.ToJsonString());
        Assert.Equal("2 woodcutter L1 Upgrading 60-180 target 2 workers 2", await BuildingAsync(city, 2));
    }

    /// <summary>A building that is already upgrading is <c>BUILDING_BUSY</c> (409) for a second upgrade, which stores nothing.</summary>
    [Fact]
    public async Task Post_WhileUpgrading_Is409BuildingBusyAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1));
        Assert.Equal(HttpStatusCode.OK, (await UpgradeAsync(player, Guid.CreateVersion7(), 1)).Status);

        await AssertRefusedAsync(player, city, 1, HttpStatusCode.Conflict, "BUILDING_BUSY");
    }

    /// <summary>A building at its type's max level (a level-2 house) is <c>MAX_LEVEL</c> (422) and stores nothing.</summary>
    [Fact]
    public async Task Post_AtMaxLevel_Is422MaxLevelAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1, level: 2));

        await AssertRefusedAsync(player, city, 1, HttpStatusCode.UnprocessableEntity, "MAX_LEVEL");
    }

    /// <summary>With both builders on constructions, an upgrade of the idle house is <c>NO_FREE_BUILDER</c> (409) and stores nothing.</summary>
    [Fact]
    public async Task Post_EveryBuilderBusy_Is409NoFreeBuilderAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            "upgrader", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Constructing, workers: 0), Woodcutter(3, BuildingPhase.Constructing, workers: 0, x: 2));

        await AssertRefusedAsync(player, city, 1, HttpStatusCode.Conflict, "NO_FREE_BUILDER");
    }

    /// <summary>
    /// The cost is checked against the stock before the refund: a producing trading post holds 2 planks in its cycle, and the level-2 cost is
    /// 40 planks. With 38 in stock the refund would cover it, but the upgrade is <c>INSUFFICIENT_RESOURCE</c> (422) and stores nothing; the
    /// cycle keeps running with its planks.
    /// </summary>
    [Fact]
    public async Task Post_CostCoveredOnlyWithTheRefund_Is422InsufficientResourceAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 38, House(1), TradingPost(2));

        await AssertRefusedAsync(player, city, 2, HttpStatusCode.UnprocessableEntity, "INSUFFICIENT_RESOURCE");
        Assert.Equal("2 trading_post L1 Producing 0-60 target 0 workers 2", await BuildingAsync(city, 2));
    }

    /// <summary>
    /// The same trading post with exactly the 40 planks of the cost in stock is upgraded: the refund comes first (40 → 42 planks), then the
    /// cost (42 → 2), in that order in the ledger.
    /// </summary>
    [Fact]
    public async Task Post_CostCoveredByTheStock_RefundsThenPays()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 40, House(1), TradingPost(2));

        ApiResponse response = await UpgradeAsync(player, _requestId, 2);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(
            [
                "planks 2 42 REFUND_UPGRADE command " + _requestId,
                "coins -400 600 COST_UPGRADE command " + _requestId,
                "planks -40 2 COST_UPGRADE command " + _requestId,
            ],
            (await LedgerAsync(Database, city)).Select(Line));
        Assert.Equal("coins 600, logs 100, planks 2", await StockAsync(city));
    }

    /// <summary>
    /// A retry with the same request id and body, a minute later, answers with the first response byte for byte and writes nothing: the
    /// minute is not settled, the cost is not paid twice, no ledger row or receipt is added.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1));
        ApiResponse first = await UpgradeAsync(player, _requestId, 1);
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await UpgradeAsync(player, _requestId, 1);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.Status, retry.Status));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>The same request id on another building is <c>IDEMPOTENCY_CONFLICT</c> (409) and writes nothing.</summary>
    [Fact]
    public async Task Post_SameRequestIdOnAnotherBuilding_Is409AndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        await UpgradeAsync(player, _requestId, 1);
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await UpgradeAsync(player, _requestId, 2);

        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Ten identical requests at once (a client's retries racing each other) upgrade once: all answer 200 with the same text, and the city
    /// holds one receipt and one cost, paid once.
    /// </summary>
    [Fact]
    public async Task Post_TenIdenticalRequestsAtOnce_UpgradeOnce()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1));

        ApiResponse[] responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => UpgradeAsync(player, _requestId, 1)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.Status));
        Assert.Single(responses.Select(response => response.Body).Distinct(StringComparer.Ordinal));
        Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal("coins 880, logs 70, planks 50", await StockAsync(city));
        Assert.Equal(2, (await LedgerAsync(Database, city)).Count);
    }

    /// <summary>
    /// Two different upgrades at once with one builder free (the other on a construction): the city's lock puts them one after the other, so
    /// exactly one starts and the other is <c>NO_FREE_BUILDER</c>; one upgrade, one cost and one receipt are stored.
    /// </summary>
    [Fact]
    public async Task Post_TwoUpgradesAtOnceWithOneFreeBuilder_ExactlyOneStarts()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            "upgrader", 1000, 100, 50, House(1), House(2, x: 1), Woodcutter(3, BuildingPhase.Constructing, workers: 0, x: 2));

        ApiResponse[] responses = await Task.WhenAll(UpgradeAsync(player, Guid.CreateVersion7(), 1), UpgradeAsync(player, Guid.CreateVersion7(), 2));

        Assert.Equal(
            ["NO_FREE_BUILDER", "OK"],
            responses.Select(response => response.Status == HttpStatusCode.OK ? "OK" : (string)response.Json()["code"]!).Order(StringComparer.Ordinal));
        Assert.Single((await LoadAsync(Factory, player)).State.Buildings, building => building.Phase == BuildingPhase.Upgrading);
        Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal("coins 880, logs 70, planks 50", await StockAsync(city));
    }

    /// <summary>
    /// A body with a field the contract doesn't have (a target level) is refused (400) on the real server too, and nothing is stored: no
    /// settle, no upgrade, no ledger row, no receipt, although time has passed.
    /// </summary>
    [Fact]
    public async Task Security_PostWithALevelField_Is400AndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync("upgrader", 1000, 100, 50, House(1));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(Factory, _path, player, $$"""{"requestId":"{{_requestId}}","buildingId":1,"level":2}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Sends an upgrade and asserts the refusal: the status, the code and the request id, and the city stored exactly as before (no time
    /// has elapsed, so not even a settlement is written).
    /// </summary>
    /// <param name="player">The acting player.</param>
    /// <param name="city">Their city.</param>
    /// <param name="buildingId">The building id sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    private async Task AssertRefusedAsync(Guid player, Guid city, int buildingId, HttpStatusCode status, string code)
    {
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await UpgradeAsync(player, _requestId, buildingId);

        Assert.Equal(status, response.Status);
        JsonNode problem = response.Json();
        Assert.Equal((code, _requestId.ToString()), ((string)problem["code"]!, (string)problem["requestId"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Stores a new player with a city at clock 0 on the server's content, era 1, no research, last settled at
    /// <see cref="PostgresApiFactory.Start"/>. The zero-length advance a command ends with runs first, so the stored city is canonical and a
    /// request with no time elapsed saves nothing of its own.
    /// </summary>
    /// <param name="name">The player's dev name.</param>
    /// <param name="coins">The coins in stock.</param>
    /// <param name="logs">The logs in stock.</param>
    /// <param name="planks">The planks in stock.</param>
    /// <param name="buildings">The buildings, with ids from 1 up.</param>
    /// <returns>The player and their city.</returns>
    private async Task<(Guid Player, Guid City)> StoreCityAsync(string name, long coins, long logs, long planks, params Building[] buildings)
    {
        GameContent content = ContentOf(Factory);
        var state = new CityState(
            0,
            era: 1,
            nextBuildingId: buildings.Length + 1,
            [new(new ItemId("coins"), coins), new(new ItemId("logs"), logs), new(new ItemId("planks"), planks)],
            buildings,
            [],
            activeResearch: null);
        Simulator.Advance(state, content, 0);
        (Guid player, Guid city) = (Guid.NewGuid(), Guid.NewGuid());
        var aggregate = new CityAggregate(city, player, state, [], PostgresApiFactory.Start.ToUnixTimeSeconds(), 0, content.ContentVersion);
        await StoreAsync(Factory, new Player(player, name), aggregate);
        return (player, city);
    }

    /// <summary>A completed, idle house (4 workers at level 1, 6 at level 2) on row 0.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="level">Its level.</param>
    /// <param name="x">Its column.</param>
    /// <returns>The building.</returns>
    private static Building House(int id, int level = 1, int x = 0) =>
        new(id, new BuildingTypeId("house"), x, 0, level, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

    /// <summary>
    /// A level-1 woodcutter on row 1: <c>Constructing</c> from 0 to its 60 seconds, <c>Idle</c>, or <c>Producing</c> a cycle of 60 labor
    /// since clock 0 with its workers.
    /// </summary>
    /// <param name="id">Its id.</param>
    /// <param name="phase">Its phase.</param>
    /// <param name="workers">Its workers (0 unless producing).</param>
    /// <param name="x">Its column.</param>
    /// <returns>The building.</returns>
    private static Building Woodcutter(int id, BuildingPhase phase, int workers, int x = 1) => phase switch
    {
        BuildingPhase.Constructing => new(id, new BuildingTypeId("woodcutter"), x, 1, 1, 0, phase, 0, 60, 0, 0, 0, 0),
        BuildingPhase.Producing => new(id, new BuildingTypeId("woodcutter"), x, 1, 1, workers, phase, 0, 60 / workers, 0, 60, 60, 0),
        _ => new(id, new BuildingTypeId("woodcutter"), x, 1, 1, workers, phase, 0, null, 0, 0, 0, 0),
    };

    /// <summary>A level-1 sawmill at (1,2) producing since clock 0 with 2 workers: 180 labor, ends at 90; its 2 logs are in the cycle.</summary>
    /// <param name="id">Its id.</param>
    /// <returns>The building.</returns>
    private static Building Sawmill(int id) =>
        new(id, new BuildingTypeId("sawmill"), 1, 2, 1, 2, BuildingPhase.Producing, 0, 90, 0, 180, 180, 0);

    /// <summary>A level-1 trading post at (1,2) producing since clock 0 with 2 workers: 120 labor, ends at 60; its 2 planks are in the cycle.</summary>
    /// <param name="id">Its id.</param>
    /// <returns>The building.</returns>
    private static Building TradingPost(int id) =>
        new(id, new BuildingTypeId("trading_post"), 1, 2, 1, 2, BuildingPhase.Producing, 0, 60, 0, 120, 120, 0);

    /// <summary>Sends an upgrade over HTTP with the player's token.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="buildingId">The building to upgrade.</param>
    /// <returns>The response.</returns>
    private Task<ApiResponse> UpgradeAsync(Guid player, Guid requestId, int buildingId) =>
        ApiClient.PostAsync(Factory, _path, player, $$"""{"requestId":"{{requestId}}","buildingId":{{buildingId}}}""");

    /// <summary>A ledger row as text: item, delta, balance, reason, reference type and id.</summary>
    /// <param name="row">The row.</param>
    /// <returns>For example <c>coins -150 850 COST_UPGRADE command …</c>.</returns>
    private static string Line(LedgerEntryRow row) => $"{row.ItemId} {row.Delta} {row.BalanceAfter} {row.Reason} {row.RefType} {row.RefId}";

    /// <summary>A stored building row as text, read by a context of the test's own.</summary>
    /// <param name="city">The city.</param>
    /// <param name="buildingId">The building.</param>
    /// <returns>For example <c>2 woodcutter L1 Upgrading 0-120 target 2 workers 0</c>.</returns>
    private async Task<string> BuildingAsync(Guid city, int buildingId)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        CityBuildingRow row = await db.CityBuildings.AsNoTracking()
            .SingleAsync(building => building.CityId == city && building.BuildingId == buildingId, TestContext.Current.CancellationToken);
        return $"{row.BuildingId} {row.TypeId} L{row.Level} {row.Phase} {row.PhaseStartedAt}-{row.PhaseEndsAt} target {row.TargetLevel ?? 0} workers {row.AssignedWorkers}";
    }

    /// <summary>A city's stored stock above 0 as text in ordinal item order, read by a context of the test's own.</summary>
    /// <param name="city">The city.</param>
    /// <returns>For example <c>coins 850, logs 100, planks 30</c>.</returns>
    private async Task<string> StockAsync(Guid city)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityInventoryRow> rows = await db.CityInventory.AsNoTracking().Where(row => row.CityId == city && row.Quantity > 0)
            .ToListAsync(TestContext.Current.CancellationToken);
        return string.Join(", ", rows.OrderBy(row => row.ItemId, StringComparer.Ordinal).Select(row => $"{row.ItemId} {row.Quantity}"));
    }
}
