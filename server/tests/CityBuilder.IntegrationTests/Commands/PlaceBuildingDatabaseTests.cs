using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.PlaceBuilding;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// <c>POST /api/v1/commands/place-building</c> end to end on PostgreSQL over HTTP: the server's real content (house 50 coins and 30 s,
/// woodcutter 80 coins and 60 s, sawmill 150 coins, 30 logs and 120 s, the trading post locked until <c>trade_charter</c>; 2 builders, an
/// 8 × 8 grid), endpoint, pipeline and repositories, the server clock a <c>FakeTimeProvider</c> at <see cref="PostgresApiFactory.Start"/>. Each
/// player starts with the starter city (700 coins, 40 logs, 10 planks, nothing built). Every test checks what was stored, not only the
/// status: the city, its buildings and stock, the ledger rows and the receipts.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class PlaceBuildingDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/place-building";

    /// <summary>The request id most tests send.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000002a1");

    /// <summary>
    /// A sawmill on a free tile: the stock pays its cost, the building row is level 1, <c>Constructing</c> from clock 0 to its 120 seconds,
    /// with no workers; the ledger holds one <c>COST_PLACE</c> row per item of the cost, referenced by the request id; the receipt holds the
    /// response text and the payload's hash; and the response shows the stored version and stock.
    /// </summary>
    [Fact]
    public async Task Post_Sawmill_PaysPlacesAndStoresTheReceipt()
    {
        (Guid player, Guid city) = await StoreStarterAsync("builder");

        ApiResponse response = await PlaceAsync(player, _requestId, "sawmill", 3, 4);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode snapshot = response.Json();
        await using CityBuilderDbContext db = Database.CreateContext();
        CityRow row = await db.Cities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((2L, 0L), (row.StateVersion, row.ClockSeconds));
        Assert.Equal(row.StateVersion, (long)snapshot["stateVersion"]!);

        Assert.Equal("coins 550, logs 10, planks 10", await StockAsync(city));
        Assert.Equal("""{"coins":550,"logs":10,"planks":10}""", snapshot["city"]!["inventory"]!.ToJsonString());

        CityBuildingRow building = await db.CityBuildings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            (1, "sawmill", (short)3, (short)4, (short)1, (short)0, "Constructing", (long?)0, (long?)120),
            (building.BuildingId, building.TypeId, building.X, building.Y, building.Level, building.AssignedWorkers, building.Phase,
                building.PhaseStartedAt, building.PhaseEndsAt));
        Assert.Equal(2, row.NextBuildingId);

        List<LedgerEntryRow> ledger = await LedgerAsync(Database, city);
        Assert.Equal(
            ["coins -150 550 COST_PLACE command " + _requestId, "logs -30 10 COST_PLACE command " + _requestId],
            ledger.Select(entry => $"{entry.ItemId} {entry.Delta} {entry.BalanceAfter} {entry.Reason} {entry.RefType} {entry.RefId}").Order(StringComparer.Ordinal));

        CommandReceiptRow receipt = Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal((_requestId, PlaceBuildingCommand.Type, response.Body), (receipt.RequestId, receipt.CommandType, receipt.ResponseJson));
        Assert.Equal(CommandPayload.Hash(new PlaceBuildingCommand(_requestId, "sawmill", 3, 4)), receipt.PayloadHash);
    }

    /// <summary>
    /// Each rule refuses with its code and status, and a refusal with no time elapsed stores nothing: the city, its stock, ledger and
    /// receipts are as they were after the commands before it. The commands before it set the city up (a tile taken, both builders busy,
    /// the logs spent).
    /// </summary>
    /// <param name="before">The placements that succeed first, as <c>type x y</c>.</param>
    /// <param name="typeId">The refused placement's type.</param>
    /// <param name="x">Its column.</param>
    /// <param name="y">Its row.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(new string[0], "castle", 0, 0, HttpStatusCode.UnprocessableEntity, "UNKNOWN_BUILDING_TYPE")]
    [InlineData(new string[0], "trading_post", 0, 0, HttpStatusCode.UnprocessableEntity, "BUILDING_LOCKED")]
    [InlineData(new string[0], "house", 8, 0, HttpStatusCode.UnprocessableEntity, "OUT_OF_BOUNDS")]
    [InlineData(new[] { "woodcutter 2 3" }, "house", 2, 3, HttpStatusCode.Conflict, "TILE_OCCUPIED")]
    [InlineData(new[] { "house 0 0", "house 1 0" }, "house", 2, 0, HttpStatusCode.Conflict, "NO_FREE_BUILDER")]
    [InlineData(new[] { "sawmill 0 0" }, "sawmill", 1, 0, HttpStatusCode.UnprocessableEntity, "INSUFFICIENT_RESOURCE")]
    public async Task Post_RuleRefuses_AnswersItsCodeAndStoresNothing(string[] before, string typeId, int x, int y, HttpStatusCode status, string code)
    {
        (Guid player, Guid city) = await StoreStarterAsync("builder");
        foreach (string placement in before)
        {
            string[] parts = placement.Split(' ');
            ApiResponse setUp = await PlaceAsync(player, Guid.CreateVersion7(), parts[0], int.Parse(parts[1], null), int.Parse(parts[2], null));
            Assert.Equal(HttpStatusCode.OK, setUp.Status);
        }

        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await PlaceAsync(player, _requestId, typeId, x, y);

        Assert.Equal(status, response.Status);
        JsonNode problem = response.Json();
        Assert.Equal((code, _requestId.ToString()), ((string)problem["code"]!, (string)problem["requestId"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A retry with the same request id and body, a minute later, answers with the first response byte for byte and writes nothing: the
    /// minute is not settled, the building is not placed twice, no ledger row or receipt is added.
    /// </summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndWritesNothing()
    {
        (Guid player, Guid city) = await StoreStarterAsync("builder");
        ApiResponse first = await PlaceAsync(player, _requestId, "house", 0, 0);
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await PlaceAsync(player, _requestId, "house", 0, 0);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.Status, retry.Status));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>The same request id on another tile is <c>IDEMPOTENCY_CONFLICT</c> (409) and writes nothing.</summary>
    [Fact]
    public async Task Post_SameRequestIdOnAnotherTile_Is409AndWritesNothing()
    {
        (Guid player, Guid city) = await StoreStarterAsync("builder");
        await PlaceAsync(player, _requestId, "house", 0, 0);
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await PlaceAsync(player, _requestId, "house", 1, 0);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal("IDEMPOTENCY_CONFLICT", (string)response.Json()["code"]!);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Ten identical requests at once (a client's retries racing each other) place one building: all answer 200 with the same text, and the
    /// city holds one building, one receipt and one cost, paid once.
    /// </summary>
    [Fact]
    public async Task Post_TenIdenticalRequestsAtOnce_PlaceOneBuilding()
    {
        (Guid player, Guid city) = await StoreStarterAsync("builder");

        ApiResponse[] responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PlaceAsync(player, _requestId, "house", 0, 0)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.Status));
        Assert.Single(responses.Select(response => response.Body).Distinct(StringComparer.Ordinal));
        Assert.Single((await LoadAsync(Factory, player)).State.Buildings);
        Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal("coins 650, logs 40, planks 10", await StockAsync(city));
        Assert.Single(await LedgerAsync(Database, city));
    }

    /// <summary>
    /// Another player's request id never matches: a second player sending the first one's request id and body places the building in their
    /// own city and gets their own snapshot, and the first player's city is untouched.
    /// </summary>
    [Fact]
    public async Task Security_AnotherPlayersRequestId_RunsAsANewCommandInTheirOwnCity()
    {
        (Guid first, Guid firstCity) = await StoreStarterAsync("first");
        (Guid second, Guid secondCity) = await StoreStarterAsync("second");
        ApiResponse ofFirst = await PlaceAsync(first, _requestId, "house", 0, 0);
        string firstStored = await StoredAsync(Factory, Database, first, firstCity);

        ApiResponse ofSecond = await PlaceAsync(second, _requestId, "house", 0, 0);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (ofFirst.Status, ofSecond.Status));
        // The two snapshots are the same text (the same starter city, request and second; a snapshot names no player), so the stored rows
        // prove that the second request ran in the second city and the first one's receipt didn't answer it.
        Assert.Equal(firstStored, await StoredAsync(Factory, Database, first, firstCity));
        Assert.Single((await LoadAsync(Factory, second)).State.Buildings);
        Assert.Equal("coins 650, logs 40, planks 10", await StockAsync(secondCity));
        Assert.Equal(_requestId, Assert.Single(await ReceiptsAsync(Database, second)).RequestId);
    }

    /// <summary>
    /// The settlement runs before the mutation: two houses (30 s) take both builders at clock 0; a second before they end a third placement is
    /// <c>NO_FREE_BUILDER</c>, and at second 30 the settle completes both houses first, so the same placement gets a builder. The response
    /// reports the two completions, and the stored city has the two houses <c>Idle</c> and the woodcutter constructing from clock 30.
    /// </summary>
    [Fact]
    public async Task Post_AfterAConstructionEndedInTheAbsence_UsesTheFreedBuilder()
    {
        (Guid player, _) = await StoreStarterAsync("builder");
        await PlaceAsync(player, Guid.CreateVersion7(), "house", 0, 0);
        await PlaceAsync(player, Guid.CreateVersion7(), "house", 1, 0);
        Factory.Clock.Advance(TimeSpan.FromSeconds(29));
        ApiResponse tooEarly = await PlaceAsync(player, Guid.CreateVersion7(), "woodcutter", 2, 0);
        Factory.Clock.Advance(TimeSpan.FromSeconds(1));

        ApiResponse response = await PlaceAsync(player, _requestId, "woodcutter", 2, 0);

        Assert.Equal("NO_FREE_BUILDER", (string)tooEarly.Json()["code"]!);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode snapshot = response.Json();
        Assert.Equal((1L, "[1,2]"), ((long)snapshot["settlement"]!["elapsedSeconds"]!, snapshot["settlement"]!["completedBuildings"]!.ToJsonString()));
        Assert.Equal("""{"slots":2,"busy":1}""", snapshot["city"]!["builders"]!.ToJsonString());

        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityBuildingRow> buildings = await db.CityBuildings.AsNoTracking().OrderBy(row => row.BuildingId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            ["1 house Idle", "2 house Idle", "3 woodcutter Constructing 30-90"],
            buildings.Select(row => $"{row.BuildingId} {row.TypeId} {row.Phase}" + (row.Phase == "Constructing" ? $" {row.PhaseStartedAt}-{row.PhaseEndsAt}" : string.Empty)));
    }

    /// <summary>
    /// Auto-staffing after completion (<c>04</c>): a house (4 workers, 30 s) and a woodcutter (2 max, 60 s) placed at clock 0; a
    /// <c>GET /city</c> at second 60 settles the woodcutter's completion, which takes <c>min(free, max) = 2</c> workers and starts producing.
    /// The snapshot shows it, and so do the stored rows.
    /// </summary>
    [Fact]
    public async Task Get_AfterThePlacedProducerCompleted_ShowsItStaffed()
    {
        (Guid player, _) = await StoreStarterAsync("builder");
        await PlaceAsync(player, Guid.CreateVersion7(), "house", 0, 0);
        await PlaceAsync(player, _requestId, "woodcutter", 1, 0);
        Factory.Clock.Advance(TimeSpan.FromSeconds(60));

        JsonNode snapshot = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal("""{"provided":4,"assigned":2,"free":2}""", snapshot["city"]!["workers"]!.ToJsonString());
        JsonNode woodcutter = snapshot["city"]!["buildings"]![1]!;
        Assert.Equal(("woodcutter", 2, "Producing"), ((string)woodcutter["typeId"]!, (int)woodcutter["assignedWorkers"]!, (string)woodcutter["phase"]!));

        await using CityBuilderDbContext db = Database.CreateContext();
        CityBuildingRow row = await db.CityBuildings.AsNoTracking().SingleAsync(building => building.BuildingId == 2, TestContext.Current.CancellationToken);
        Assert.Equal(((short)2, "Producing"), (row.AssignedWorkers, row.Phase));
    }

    /// <summary>
    /// A body with a field the contract doesn't have is refused (400) on the real server too, and nothing is stored: no settle, no building,
    /// no ledger row, no receipt, although time has passed.
    /// </summary>
    [Fact]
    public async Task Security_PostWithACostField_Is400AndStoresNothing()
    {
        (Guid player, Guid city) = await StoreStarterAsync("builder");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(
            Factory, _path, player, $$$"""{"requestId":"{{{_requestId}}}","buildingTypeId":"house","x":0,"y":0,"cost":{"coins":0}}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>Stores a new player with the starter city of the server's content, last settled at <see cref="PostgresApiFactory.Start"/>.</summary>
    /// <param name="name">The player's dev name.</param>
    /// <returns>The player and their city.</returns>
    private async Task<(Guid Player, Guid City)> StoreStarterAsync(string name)
    {
        (Guid player, Guid city) = (Guid.NewGuid(), Guid.NewGuid());
        await StoreAsync(Factory, new Player(player, name), Starter(ContentOf(Factory), city, player, PostgresApiFactory.Start.ToUnixTimeSeconds()));
        return (player, city);
    }

    /// <summary>Sends a placement over HTTP with the player's token.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="typeId">The type to place.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The response.</returns>
    private Task<ApiResponse> PlaceAsync(Guid player, Guid requestId, string typeId, int x, int y) =>
        ApiClient.PostAsync(Factory, _path, player, $$"""{"requestId":"{{requestId}}","buildingTypeId":"{{typeId}}","x":{{x}},"y":{{y}}}""");

    /// <summary>A city's stored stock above 0 as text in ordinal item order, read by a context of the test's own.</summary>
    /// <param name="city">The city.</param>
    /// <returns>For example <c>coins 550, logs 10</c>.</returns>
    private async Task<string> StockAsync(Guid city)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityInventoryRow> rows = await db.CityInventory.AsNoTracking().Where(row => row.CityId == city && row.Quantity > 0)
            .ToListAsync(TestContext.Current.CancellationToken);
        return string.Join(", ", rows.OrderBy(row => row.ItemId, StringComparer.Ordinal).Select(row => $"{row.ItemId} {row.Quantity}"));
    }
}
