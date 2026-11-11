using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.UpgradeBuilding;
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
/// <c>POST /api/v1/commands/upgrade-building</c> end to end on PostgreSQL over HTTP: the server's real content (level 2 of a house: 120 coins,
/// 30 logs, 90 s, 4 → 6 workers provided; of a woodcutter: 150 coins, 20 planks, 120 s, max workers 2 → 4; of a sawmill: 250 coins,
/// 30 planks, 180 s; of a trading post: 400 coins, 40 planks, 300 s; every type's max level is 2; 2 builders; trips carry 10), endpoint,
/// pipeline and repositories, the server clock a <c>FakeTimeProvider</c> at <see cref="PostgresApiFactory.Start"/>. Every level-2 cost has
/// goods, so an upgrade pays its coins and waits for them (CR-0008): the building keeps working until <c>start-trip</c>s from a warehouse
/// deliver the last of them, then it upgrades. Each test stores a city built by hand at clock 0 (<see cref="StoreCityAsync"/>) and checks
/// what was stored, not only the status: the building and need rows, the purse and stores, the ledger rows and the receipts.
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
    /// An idle woodcutter without workers: the purse pays the level-2 coins (1000 → 850); the upgrade waits for its 20 planks: the building row
    /// stays <c>Idle</c> at level 1 with a need row of 20 planks, and its builder is busy; the ledger holds the one <c>COST_UPGRADE</c> row of the
    /// coins, referenced by the request id; the receipt holds the response text and the payload's hash.
    /// </summary>
    [Fact]
    public async Task Post_IdleWoodcutter_PaysTheCoinsAndWaitsForThePlanks()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        long versionBefore = (await LoadAsync(Factory, player)).StateVersion;

        ApiResponse response = await UpgradeAsync(player, _requestId, 2);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode snapshot = response.Json();
        await using CityBuilderDbContext db = Database.CreateContext();
        CityRow row = await db.Cities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((versionBefore + 1, 0L), (row.StateVersion, row.ClockSeconds));
        Assert.Equal(row.StateVersion, (long)snapshot["stateVersion"]!);
        Assert.Equal("coins 850", await StockAsync(Database, city));
        Assert.Equal("""{"slots":2,"busy":1}""", snapshot["city"]!["builders"]!.ToJsonString());
        Assert.Equal(("Upgrade", """{"planks":20}"""), ((string)snapshot["city"]!["buildings"]![1]!["project"]!, snapshot["city"]!["buildings"]![1]!["needs"]!.ToJsonString()));
        Assert.Equal("2 woodcutter L1 Idle - target 0 workers 0", await BuildingAsync(Database, city, 2));
        CityProjectNeedRow need = await db.CityProjectNeeds.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((2, "planks", 20L), (need.BuildingId, need.ItemId, need.Quantity));

        Assert.Equal(["coins -150 850 COST_UPGRADE command " + _requestId], (await LedgerAsync(Database, city)).Select(Line));

        CommandReceiptRow receipt = Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal((_requestId, UpgradeBuildingCommand.Type, response.Body), (receipt.RequestId, receipt.CommandType, receipt.ResponseJson));
        Assert.Equal(CommandPayload.Hash(new UpgradeBuildingCommand(_requestId, 2)), receipt.PayloadHash);
    }

    /// <summary>
    /// A producing sawmill whose store is full (40 logs, its cycle took 2 more) is upgraded and keeps producing while two workers bring its 30
    /// planks from the warehouse, 5 s away, 10 a load (CR-0008 D21): both deliver at second 5 (20 planks, 10 still needed) and walk back; at
    /// 10 the first takes the last 10, the second finds none and is done. The last arrival, at second 15, starts the upgrade: the cycle ends
    /// without output and its 2 logs come back into the store, above the limit (42); that settlement writes <c>SETTLEMENT_REFUNDED</c> and
    /// <c>SETTLEMENT_DELIVERED</c> rows with the city's totals, and the sawmill is <c>Upgrading</c> from 15 for 180 seconds.
    /// </summary>
    [Fact]
    public async Task Post_ProducingSawmill_LastPlanksDeliveredStartTheUpgradeAndRefundAboveTheLimit()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1), Sawmill(2, logs: 40), Warehouse(3, logs: 0, planks: 30));
        Assert.Equal(HttpStatusCode.OK, (await UpgradeAsync(player, _requestId, 2)).Status);
        await TripAsync(player, 3, 2, "planks");
        await TripAsync(player, 3, 2, "planks");
        Factory.Clock.Advance(TimeSpan.FromSeconds(5));
        JsonNode waiting = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(10));

        JsonNode started = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal(("Producing", """{"planks":10}"""), ((string)waiting["city"]!["buildings"]![1]!["phase"]!, waiting["city"]!["buildings"]![1]!["needs"]!.ToJsonString()));
        Assert.Equal("""{"delivered":{"planks":10},"moved":{},"refunded":{"logs":2}}""", Maps(started["settlement"]!));
        Assert.Equal("2 sawmill L1 Upgrading 15-195 target 2 workers 2", await BuildingAsync(Database, city, 2));
        Assert.Equal("coins 750, logs 42", await StockAsync(Database, city));
        Assert.Equal(
            ["logs 2 42 SETTLEMENT_REFUNDED settlement", "planks -10 0 SETTLEMENT_DELIVERED settlement"],
            (await LedgerAsync(Database, city)).TakeLast(2).Select(row => $"{row.ItemId} {row.Delta} {row.BalanceAfter} {row.Reason} {row.RefType}"));
    }

    /// <summary>
    /// The workers stay assigned and the max rises after the upgrade: a woodcutter with its 2 workers (the level-1 max) producing since clock
    /// 0 is upgraded at second 10 and keeps producing while two trips bring its 20 planks (10 s away). They arrive at second 20, which starts
    /// the upgrade and ends the cycle without a log; at second 60 it is <c>Upgrading</c> with the 2 workers kept (<c>maxWorkers</c> still
    /// level 1's 2); at second 150 it is level 2 with the level's max of 4, the same 2 workers, and a cycle running from 140.
    /// </summary>
    [Fact]
    public async Task Post_StaffedWoodcutter_KeepsItsWorkersAndHasTheHigherMaxAfter()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory, "upgrader", 1000, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2), Warehouse(3, logs: 0, planks: 20));
        Factory.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.OK, (await UpgradeAsync(player, _requestId, 2)).Status);
        await TripAsync(player, 3, 2, "planks");
        JsonNode ordered = await TripAsync(player, 3, 2, "planks");
        Factory.Clock.Advance(TimeSpan.FromSeconds(50));

        JsonNode during = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(90));
        JsonNode after = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal(("Producing", 1, 2, 2), Seen(ordered));
        Assert.Equal(("Upgrading", 1, 2, 2), Seen(during));
        Assert.Equal("""{"provided":4,"assigned":2,"onTrips":0,"free":2}""", during["city"]!["workers"]!.ToJsonString());
        Assert.Equal(("Producing", 2, 2, 4), Seen(after));
        Assert.Equal("2 woodcutter L2 Producing 140-170 target 0 workers 2", await BuildingAsync(Database, city, 2));
        Assert.Equal("coins 850", await StockAsync(Database, city));

        // The woodcutter in a snapshot: its phase, level, workers and max workers.
        static (string, int, int, int) Seen(JsonNode snapshot)
        {
            JsonNode building = snapshot["city"]!["buildings"]![1]!;
            return ((string)building["phase"]!, (int)building["level"]!, (int)building["assignedWorkers"]!, (int)building["maxWorkers"]!);
        }
    }

    /// <summary>
    /// A house upgrade raises the workers provided on completion, not before: ordered at clock 0, it starts when three trips have brought its 30
    /// logs from the warehouse (20 s away), and ends 90 s later, at second 110; the house still provides its level-1 4 workers at second 109
    /// and provides level 2's 6 at second 110, when it is level 2 and <c>Idle</c>, also in its stored row.
    /// </summary>
    [Fact]
    public async Task Post_House_RaisesTheWorkersProvidedWhenTheUpgradeCompletes()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1), Warehouse(2, logs: 30, planks: 0));

        JsonNode started = (await UpgradeAsync(player, _requestId, 1)).Json();
        for (int trip = 0; trip < 3; trip++)
        {
            await TripAsync(player, 2, 1, "logs");
        }

        Factory.Clock.Advance(TimeSpan.FromSeconds(109));
        JsonNode before = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        JsonNode completed = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal(4, (int)started["city"]!["workers"]!["provided"]!);
        Assert.Equal((4, "Upgrading"), ((int)before["city"]!["workers"]!["provided"]!, (string)before["city"]!["buildings"]![0]!["phase"]!));
        Assert.Equal((6, "Idle", 2), ((int)completed["city"]!["workers"]!["provided"]!, (string)completed["city"]!["buildings"]![0]!["phase"]!, (int)completed["city"]!["buildings"]![0]!["level"]!));
        Assert.Equal("[1]", completed["settlement"]!["completedBuildings"]!.ToJsonString());
        Assert.Equal("1 house L2 Idle - target 0 workers 0", await BuildingAsync(Database, city, 1));
    }

    /// <summary>An id of 1 or more the city doesn't have is <c>BUILDING_NOT_FOUND</c> (404) and stores nothing.</summary>
    /// <param name="buildingId">The id sent.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public async Task Post_IdNotInTheCity_Is404AndStoresNothing(int buildingId)
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));

        await AssertRefusedAsync(player, city, buildingId, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND");
    }

    /// <summary>
    /// An id below 1, which no city has, is <c>INVALID_ARGUMENT</c> (400) before the city is locked (owner directive OD-13): five minutes after
    /// the last settle, nothing is stored, not even the five minutes.
    /// </summary>
    /// <param name="buildingId">The id sent.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task Security_BuildingIdBelowOne_Is400AndStoresNothing(int buildingId)
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "attacker", 1000, House(1));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await UpgradeAsync(player, _requestId, buildingId);

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_ARGUMENT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Another player's building id is <c>BUILDING_NOT_FOUND</c> (404), never a sign that it exists: the second player has buildings 1 and 2,
    /// the first player's woodcutter is building 3. The second player's upgrade of 3 is refused and stores nothing, and the first player's
    /// city is untouched (its woodcutter still idle at level 1, its stock, ledger and receipts as they were).
    /// </summary>
    [Fact]
    public async Task Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity()
    {
        (Guid owner, Guid ownerCity) = await StoreCityAsync(Factory, "owner", 1000, House(1), House(2, x: 1), Woodcutter(3, BuildingPhase.Idle, workers: 0));
        (Guid attacker, Guid attackerCity) = await StoreCityAsync(Factory, "attacker", 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        string ownerStored = await StoredAsync(Factory, Database, owner, ownerCity);

        await AssertRefusedAsync(attacker, attackerCity, 3, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND");

        Assert.Equal(ownerStored, await StoredAsync(Factory, Database, owner, ownerCity));
        Assert.Equal("3 woodcutter L1 Idle - target 0 workers 0", await BuildingAsync(Database, ownerCity, 3));
    }

    /// <summary>
    /// A building under construction is <c>BUILDING_BUSY</c> (409) until the settle completes it: the woodcutter is built until second 60; at
    /// second 59 the upgrade is refused and stores nothing but the settlement, and at second 60 the settle completes the construction first
    /// (the woodcutter gets 2 workers and starts a cycle), so the same upgrade is ordered and waits for its planks.
    /// </summary>
    [Fact]
    public async Task Post_WhileConstructing_IsBusyUntilTheSettleCompletesTheConstruction()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1), Woodcutter(2, BuildingPhase.Constructing, workers: 0));
        Factory.Clock.Advance(TimeSpan.FromSeconds(59));

        ApiResponse tooEarly = await UpgradeAsync(player, Guid.CreateVersion7(), 2);
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        ApiResponse response = await UpgradeAsync(player, _requestId, 2);

        Assert.Equal((HttpStatusCode.Conflict, "BUILDING_BUSY"), (tooEarly.Status, (string)tooEarly.Json()["code"]!));
        Assert.Contains("#2 woodcutter (1,1) L1 w0 Constructing 0..60", stored, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("[2]", response.Json()["settlement"]!["completedBuildings"]!.ToJsonString());
        Assert.Equal("2 woodcutter L1 Producing 60-90 target 0 workers 2", await BuildingAsync(Database, city, 2));
        Assert.Equal("""{"planks":20}""", response.Json()["city"]!["buildings"]![1]!["needs"]!.ToJsonString());
    }

    /// <summary>A building that is already upgrading is <c>BUILDING_BUSY</c> (409) for a second upgrade, which stores nothing.</summary>
    [Fact]
    public async Task Post_WhileUpgrading_Is409BuildingBusyAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1));
        Assert.Equal(HttpStatusCode.OK, (await UpgradeAsync(player, Guid.CreateVersion7(), 1)).Status);

        await AssertRefusedAsync(player, city, 1, HttpStatusCode.Conflict, "BUILDING_BUSY");
    }

    /// <summary>A building at its type's max level (a level-2 house) is <c>MAX_LEVEL</c> (422) and stores nothing.</summary>
    [Fact]
    public async Task Post_AtMaxLevel_Is422MaxLevelAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1, level: 2));

        await AssertRefusedAsync(player, city, 1, HttpStatusCode.UnprocessableEntity, "MAX_LEVEL");
    }

    /// <summary>With both builders on constructions, an upgrade of the idle house is <c>NO_FREE_BUILDER</c> (409) and stores nothing.</summary>
    [Fact]
    public async Task Post_EveryBuilderBusy_Is409NoFreeBuilderAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory, "upgrader", 1000, House(1), Woodcutter(2, BuildingPhase.Constructing, workers: 0), Woodcutter(3, BuildingPhase.Constructing, workers: 0, x: 2));

        await AssertRefusedAsync(player, city, 1, HttpStatusCode.Conflict, "NO_FREE_BUILDER");
    }

    /// <summary>
    /// Coins the purse doesn't cover are <c>INSUFFICIENT_RESOURCE</c> (422): 399 coins for a trading post's 400; nothing is stored and the
    /// cycle keeps running.
    /// </summary>
    [Fact]
    public async Task Post_CoinsShort_Is422InsufficientResourceAndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 399, House(1), TradingPost(2));

        await AssertRefusedAsync(player, city, 2, HttpStatusCode.UnprocessableEntity, "INSUFFICIENT_RESOURCE");
        Assert.Equal("2 trading_post L1 Producing 0-60 target 0 workers 2", await BuildingAsync(Database, city, 2));
    }

    /// <summary>
    /// A producing trading post is upgraded with exactly its 400 coins: only the coins are paid (CR-0008: goods are never taken from a store),
    /// the cycle keeps running with its 2 planks, and the 40 planks become the upgrade's needs.
    /// </summary>
    [Fact]
    public async Task Post_ProducingTradingPost_PaysOnlyTheCoinsAndKeepsProducing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 400, House(1), TradingPost(2, planks: 40));

        ApiResponse response = await UpgradeAsync(player, _requestId, 2);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(["coins -400 0 COST_UPGRADE command " + _requestId], (await LedgerAsync(Database, city)).Select(Line));
        Assert.Equal("planks 40", await StockAsync(Database, city));
        Assert.Equal("2 trading_post L1 Producing 0-60 target 0 workers 2", await BuildingAsync(Database, city, 2));
        Assert.Equal("""{"planks":40}""", response.Json()["city"]!["buildings"]![1]!["needs"]!.ToJsonString());
    }

    /// <summary>
    /// A retry with the same request id and body, a minute later, answers with the first response byte for byte and writes nothing: the
    /// minute is not settled, the cost is not paid twice, no ledger row or receipt is added.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1));
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
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
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
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1));

        ApiResponse[] responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => UpgradeAsync(player, _requestId, 1)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.Status));
        Assert.Single(responses.Select(response => response.Body).Distinct(StringComparer.Ordinal));
        Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal("coins 880", await StockAsync(Database, city));
        Assert.Single(await LedgerAsync(Database, city));
    }

    /// <summary>
    /// Two different upgrades at once with one builder free (the other on a construction): the city's lock puts them one after the other, so
    /// exactly one is ordered (it waits for its logs and keeps its builder) and the other is <c>NO_FREE_BUILDER</c>; one upgrade, one cost
    /// and one receipt are stored.
    /// </summary>
    [Fact]
    public async Task Post_TwoUpgradesAtOnceWithOneFreeBuilder_ExactlyOneIsOrdered()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory, "upgrader", 1000, House(1), House(2, x: 1), Woodcutter(3, BuildingPhase.Constructing, workers: 0, x: 2));

        ApiResponse[] responses = await Task.WhenAll(UpgradeAsync(player, Guid.CreateVersion7(), 1), UpgradeAsync(player, Guid.CreateVersion7(), 2));

        Assert.Equal(
            ["NO_FREE_BUILDER", "OK"],
            responses.Select(response => response.Status == HttpStatusCode.OK ? "OK" : (string)response.Json()["code"]!).Order(StringComparer.Ordinal));
        Assert.Single((await LoadAsync(Factory, player)).State.Buildings, building => building.HasUpgradeWaiting);
        Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal("coins 880", await StockAsync(Database, city));
    }

    /// <summary>
    /// A body with a field the contract doesn't have (a target level) is refused (400) on the real server too, and nothing is stored: no
    /// settle, no upgrade, no ledger row, no receipt, although time has passed.
    /// </summary>
    [Fact]
    public async Task Security_PostWithALevelField_Is400AndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 1000, House(1));
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

    /// <summary>Sends a trip of all of a good from one building's store to another over HTTP and returns the snapshot; it must succeed.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="source">The building the goods come from.</param>
    /// <param name="target">The building they go to.</param>
    /// <param name="item">The good.</param>
    /// <returns>The snapshot.</returns>
    private async Task<JsonNode> TripAsync(Guid player, int source, int target, string item)
    {
        ApiResponse response = await ApiClient.PostAsync(
            Factory,
            "/api/v1/commands/start-trip",
            player,
            $$"""{"requestId":"{{Guid.CreateVersion7()}}","sourceId":{{source}},"targetId":{{target}},"itemId":"{{item}}","amount":"all"}""");
        Assert.Equal(HttpStatusCode.OK, response.Status);
        return response.Json();
    }

    /// <summary>The settlement's delivered, moved and refunded maps as one JSON object, for one assertion.</summary>
    /// <param name="settlement">The snapshot's settlement.</param>
    /// <returns>The three maps.</returns>
    private static string Maps(JsonNode settlement) =>
        $$"""{"delivered":{{settlement["delivered"]!.ToJsonString()}},"moved":{{settlement["moved"]!.ToJsonString()}},"refunded":{{settlement["refunded"]!.ToJsonString()}}}""";

    /// <summary>Sends an upgrade over HTTP with the player's token.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="buildingId">The building to upgrade.</param>
    /// <returns>The response.</returns>
    private Task<ApiResponse> UpgradeAsync(Guid player, Guid requestId, int buildingId) =>
        ApiClient.PostAsync(Factory, _path, player, $$"""{"requestId":"{{requestId}}","buildingId":{{buildingId}}}""");
}
