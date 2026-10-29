using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.AssignWorkers;
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
/// <c>POST /api/v1/commands/assign-workers</c> end to end on PostgreSQL over HTTP: the server's real content (a level-1 house provides 4
/// workers; a woodcutter makes a log per 60 labor and takes at most 2 workers at level 1, 4 at level 2; its upgrade takes 120 s), endpoint,
/// pipeline and repositories, the server clock a <c>FakeTimeProvider</c> at <see cref="PostgresApiFactory.Start"/>. Each test stores a city
/// built by hand at clock 0 (<see cref="HandBuiltCities.StoreCityAsync"/>) and checks what was stored, not only the status: the building
/// rows, the stock, the ledger and the receipts. The cycle ends are worked out by hand in each test's comment with <c>04</c> section 3: on a
/// reassignment the labor done is <c>(t − segmentStartedAt) × old workers</c>, and the end is <c>t + ceil(laborRemaining / new workers)</c>.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class AssignWorkersDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/assign-workers";

    /// <summary>The request id most tests send.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000003b1");

    /// <summary>
    /// Two workers on an idle woodcutter start its cycle in the same response: 60 labor from clock 0, ending at 30, no stall; the workers
    /// block counts them. The row is stored so, the command writes no ledger row (workers cost nothing), and the receipt holds the response
    /// and the payload's hash. At second 30 the log is in the stock.
    /// </summary>
    [Fact]
    public async Task Post_IdleWoodcutter_StartsProducingInTheSameResponse()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        long versionBefore = (await LoadAsync(Factory, player)).StateVersion;

        ApiResponse response = await AssignAsync(player, _requestId, 2, 2);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        JsonNode snapshot = response.Json();
        Assert.Equal(versionBefore + 1, (long)snapshot["stateVersion"]!);
        Assert.Equal("Producing L1 w2/2 0-30 labor 60/60 segment 0 stall null", Seen(snapshot, 2));
        Assert.Equal("""{"provided":4,"assigned":2,"free":2}""", snapshot["city"]!["workers"]!.ToJsonString());
        Assert.Equal("2 woodcutter L1 Producing 0-30 target 0 workers 2", await BuildingAsync(Database, city, 2));
        Assert.Empty(await LedgerAsync(Database, city));
        CommandReceiptRow receipt = Assert.Single(await ReceiptsAsync(Database, player));
        Assert.Equal((_requestId, AssignWorkersCommand.Type, response.Body), (receipt.RequestId, receipt.CommandType, receipt.ResponseJson));
        Assert.Equal(CommandPayload.Hash(new AssignWorkersCommand(_requestId, 2, 2)), receipt.PayloadHash);

        Factory.Clock.Advance(TimeSpan.FromSeconds(30));
        await ApiClient.GetAsync(Factory, "/api/v1/city", player);
        Assert.Equal("coins 1000, logs 101, planks 50", await StockAsync(Database, city));
    }

    /// <summary>
    /// Reassigning mid-cycle keeps the progress. A level-2 woodcutter (max 4) runs a 60-labor cycle with 1 worker from 0 (end 60). At 20, 2
    /// workers: 20 labor done, 40 left, a segment from 20, end 20 + 40/2 = 40. At 30, 3 workers: 10 × 2 = 20 more done, 20 left, a segment
    /// from 30, end 30 + ceil(20/3) = 37. At 37 the log is in, and the next cycle runs 37 to 37 + 60/3 = 57. The cycle's start stays 0.
    /// </summary>
    [Fact]
    public async Task Post_ReassignMidCycle_KeepsTheProgress()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 1, level: 2));

        Factory.Clock.Advance(TimeSpan.FromSeconds(20));
        JsonNode twoWorkers = (await AssignAsync(player, Guid.CreateVersion7(), 2, 2)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(10));
        JsonNode threeWorkers = (await AssignAsync(player, Guid.CreateVersion7(), 2, 3)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(7));
        JsonNode after = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal("Producing L2 w2/4 0-40 labor 40/60 segment 20 stall null", Seen(twoWorkers, 2));
        Assert.Equal("Producing L2 w3/4 0-37 labor 20/60 segment 30 stall null", Seen(threeWorkers, 2));
        Assert.Equal("Producing L2 w3/4 37-57 labor 60/60 segment 37 stall null", Seen(after, 2));
        Assert.Equal("2 woodcutter L2 Producing 37-57 target 0 workers 3", await BuildingAsync(Database, city, 2));
        Assert.Equal("coins 1000, logs 101, planks 50", await StockAsync(Database, city));
    }

    /// <summary>
    /// Zero workers freeze the cycle and assigning again resumes it. A woodcutter producing with 2 workers from 0 (end 30) gets 0 at 10:
    /// 20 labor done, 40 left, no end (<c>phaseEndsAt</c> null), stall <c>NoWorkers</c>, the 2 workers free again. At 100 nothing has moved
    /// and no log was made. 2 workers at 100 end it at 100 + 40/2 = 120, when the log is in.
    /// </summary>
    [Fact]
    public async Task Post_ZeroWorkers_FreezesAndAssigningAgainResumes()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2));

        Factory.Clock.Advance(TimeSpan.FromSeconds(10));
        JsonNode frozen = (await AssignAsync(player, Guid.CreateVersion7(), 2, 0)).Json();
        string frozenRow = await BuildingAsync(Database, city, 2);
        Factory.Clock.Advance(TimeSpan.FromSeconds(90));
        JsonNode later = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        string logsWhileFrozen = await StockAsync(Database, city);
        JsonNode resumed = (await AssignAsync(player, Guid.CreateVersion7(), 2, 2)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(20));
        await ApiClient.GetAsync(Factory, "/api/v1/city", player);

        Assert.Equal("Producing L1 w0/2 0-null labor 40/60 segment 10 stall NoWorkers", Seen(frozen, 2));
        Assert.Equal("""{"provided":4,"assigned":0,"free":4}""", frozen["city"]!["workers"]!.ToJsonString());
        Assert.Equal("2 woodcutter L1 Producing 0- target 0 workers 0", frozenRow);
        Assert.Equal("Producing L1 w0/2 0-null labor 40/60 segment 10 stall NoWorkers", Seen(later, 2));
        Assert.Equal("coins 1000, logs 100, planks 50", logsWhileFrozen);
        Assert.Equal("Producing L1 w2/2 0-120 labor 40/60 segment 100 stall null", Seen(resumed, 2));
        Assert.Equal("coins 1000, logs 101, planks 50", await StockAsync(Database, city));
    }

    /// <summary>
    /// More workers than are free is <c>INSUFFICIENT_WORKERS</c> (422) and stores nothing: of the house's 4 workers, woodcutter 2 has 2 and
    /// woodcutter 3 has 1, so 1 is free and raising woodcutter 2 from 2 to 4 needs 2. Raising it to 3 needs only the free one and succeeds.
    /// </summary>
    [Fact]
    public async Task Post_MoreThanTheFreeWorkers_Is422InsufficientWorkers()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory,
            "assigner",
            1000,
            100,
            50,
            House(1),
            Woodcutter(2, BuildingPhase.Producing, workers: 2, level: 2),
            Woodcutter(3, BuildingPhase.Producing, workers: 1, x: 2, level: 2));

        await AssertRefusedAsync(player, city, 2, 4, (HttpStatusCode)422, "INSUFFICIENT_WORKERS");

        ApiResponse three = await AssignAsync(player, Guid.CreateVersion7(), 2, 3);
        Assert.Equal(HttpStatusCode.OK, three.Status);
        Assert.Equal("""{"provided":4,"assigned":4,"free":0}""", three.Json()["city"]!["workers"]!.ToJsonString());
    }

    /// <summary>
    /// A count above the building's level's max is <c>WORKER_LIMIT</c> (422) and stores nothing, also when enough workers are free (two
    /// houses: 8): 3 on a level-1 woodcutter (max 2), 5 on a level-2 one (max 4), the largest count there is.
    /// </summary>
    /// <param name="level">The woodcutter's level.</param>
    /// <param name="workers">The count sent.</param>
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 5)]
    [InlineData(1, int.MaxValue)]
    public async Task Post_AboveTheLevelsMax_Is422WorkerLimit(int level, int workers)
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory, "assigner", 1000, 100, 50, House(1), House(2, x: 1), Woodcutter(3, BuildingPhase.Idle, workers: 0, x: 2, level: level));

        await AssertRefusedAsync(player, city, 3, workers, (HttpStatusCode)422, "WORKER_LIMIT");
    }

    /// <summary>A house has no recipe and takes no workers: <c>INVALID_ARGUMENT</c> (400) from the rule, with the request id, and nothing stored.</summary>
    [Fact]
    public async Task Post_House_Is400InvalidArgument()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1));

        await AssertRefusedAsync(player, city, 1, 1, HttpStatusCode.BadRequest, "INVALID_ARGUMENT");
    }

    /// <summary>
    /// A woodcutter still under construction is <c>BUILDING_BUSY</c> (409) at clock 0 and nothing is stored. At 60 the settle completes it
    /// before the mutation, so the same assignment succeeds: construction auto-staffs it with 2 and starts a cycle at 60, and 1 worker then
    /// makes the end 60 + 60/1 = 120.
    /// </summary>
    [Fact]
    public async Task Post_Constructing_Is409BuildingBusyUntilTheSettleCompletesIt()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Constructing, workers: 0));

        await AssertRefusedAsync(player, city, 2, 1, HttpStatusCode.Conflict, "BUILDING_BUSY");
        Factory.Clock.Advance(TimeSpan.FromSeconds(60));
        ApiResponse response = await AssignAsync(player, Guid.CreateVersion7(), 2, 1);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Producing L1 w1/2 60-120 labor 60/60 segment 60 stall null", Seen(response.Json(), 2));
        Assert.Equal("2 woodcutter L1 Producing 60-120 target 0 workers 1", await BuildingAsync(Database, city, 2));
    }

    /// <summary>
    /// Assigning is allowed while upgrading, against the current level's max: a woodcutter upgrading from 0 to 120 with 2 workers gets 1 at
    /// 10 (the upgrade's timers don't move; 3 free), and 3 is <c>WORKER_LIMIT</c> (level 1's max is 2). At 120 it is level 2 and starts a
    /// cycle with its 1 worker, 120 to 180.
    /// </summary>
    [Fact]
    public async Task Post_Upgrading_IsAllowedUpToTheCurrentLevelsMax()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Upgrading, workers: 2));

        Factory.Clock.Advance(TimeSpan.FromSeconds(10));
        ApiResponse one = await AssignAsync(player, Guid.CreateVersion7(), 2, 1);
        ApiResponse three = await AssignAsync(player, Guid.CreateVersion7(), 2, 3);
        string duringRow = await BuildingAsync(Database, city, 2);
        Factory.Clock.Advance(TimeSpan.FromSeconds(110));
        await ApiClient.GetAsync(Factory, "/api/v1/city", player);

        Assert.Equal(HttpStatusCode.OK, one.Status);
        Assert.Equal("Upgrading L1 w1/2 0-120 labor 0/0 segment 0 stall null", Seen(one.Json(), 2));
        Assert.Equal("""{"provided":4,"assigned":1,"free":3}""", one.Json()["city"]!["workers"]!.ToJsonString());
        Assert.Equal(((HttpStatusCode)422, "WORKER_LIMIT"), (three.Status, (string)three.Json()["code"]!));
        Assert.Equal("2 woodcutter L1 Upgrading 0-120 target 2 workers 1", duringRow);
        Assert.Equal("2 woodcutter L2 Producing 120-180 target 0 workers 1", await BuildingAsync(Database, city, 2));
    }

    /// <summary>
    /// A building id is looked up in the caller's city only: the id 3 exists only in the owner's city, so the attacker's request is
    /// <c>BUILDING_NOT_FOUND</c> (404), neither city changes, and the owner's woodcutter keeps no workers.
    /// </summary>
    [Fact]
    public async Task Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity()
    {
        (Guid owner, Guid ownerCity) = await StoreCityAsync(
            Factory, "owner", 1000, 100, 50, House(1), House(2, x: 1), Woodcutter(3, BuildingPhase.Idle, workers: 0, x: 2));
        (Guid attacker, Guid attackerCity) = await StoreCityAsync(Factory, "attacker", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        string ownerStored = await StoredAsync(Factory, Database, owner, ownerCity);

        await AssertRefusedAsync(attacker, attackerCity, 3, 1, HttpStatusCode.NotFound, "BUILDING_NOT_FOUND");

        Assert.Equal(ownerStored, await StoredAsync(Factory, Database, owner, ownerCity));
        Assert.Equal("3 woodcutter L1 Idle - target 0 workers 0", await BuildingAsync(Database, ownerCity, 3));
    }

    /// <summary>
    /// An id below 1 and a negative count are <c>INVALID_ARGUMENT</c> (400) on the real server before the city is locked (OD-13): five
    /// minutes have passed, and still nothing is stored, not even the settle.
    /// </summary>
    /// <param name="buildingId">The building id sent.</param>
    /// <param name="workers">The count sent.</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(2, -1)]
    [InlineData(2, int.MinValue)]
    public async Task Security_IdBelowOneOrNegativeWorkers_Is400AndStoresNothing(int buildingId, int workers)
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "attacker", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Producing, workers: 2));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await AssignAsync(player, _requestId, buildingId, workers);

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_ARGUMENT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A body with a field the contract doesn't have (free workers) is refused (400) on the real server too, and nothing is stored, although
    /// time has passed.
    /// </summary>
    [Fact]
    public async Task Security_PostWithAFreeWorkersField_Is400AndStoresNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "attacker", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(
            Factory, _path, player, $$"""{"requestId":"{{_requestId}}","buildingId":2,"workers":2,"freeWorkers":99}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>A retry with the same request id and body a minute later answers with the stored bytes and writes nothing.</summary>
    [Fact]
    public async Task Post_SameRequestAgain_ReturnsTheSameBytesAndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        ApiResponse first = await AssignAsync(player, _requestId, 2, 2);
        string stored = await StoredAsync(Factory, Database, player, city);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        ApiResponse retry = await AssignAsync(player, _requestId, 2, 2);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.Status, retry.Status));
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>The same request id with another count is <c>IDEMPOTENCY_CONFLICT</c> (409) and writes nothing.</summary>
    [Fact]
    public async Task Post_SameRequestIdWithAnotherCount_Is409AndWritesNothing()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        await AssignAsync(player, _requestId, 2, 2);
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await AssignAsync(player, _requestId, 2, 1);

        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// The same count again, as a new request, succeeds and moves nothing a player sees (decision 5): 2 workers on an idle woodcutter at 0
    /// (end 30), 2 again at 10. The end, the workers and the stock stay; the stored segment restarts at 10 with the 20 labor done taken off
    /// (60 − 20 = 40), so the progress the client shows at 10 is 20 from either snapshot (<c>05</c>: <c>cycleLabor − (laborRemaining −
    /// (clock − segmentStartedAt) × assignedWorkers)</c>). No ledger row, two receipts, the version one higher, and the log at 30.
    /// </summary>
    [Fact]
    public async Task Post_SameCountTwice_SucceedsAndMovesNothingAPlayerSees()
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "assigner", 1000, 100, 50, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0));
        JsonNode first = (await AssignAsync(player, Guid.CreateVersion7(), 2, 2)).Json();
        Factory.Clock.Advance(TimeSpan.FromSeconds(10));

        ApiResponse again = await AssignAsync(player, Guid.CreateVersion7(), 2, 2);

        Assert.Equal(HttpStatusCode.OK, again.Status);
        JsonNode second = again.Json();
        Assert.Equal("Producing L1 w2/2 0-30 labor 60/60 segment 0 stall null", Seen(first, 2));
        Assert.Equal("Producing L1 w2/2 0-30 labor 40/60 segment 10 stall null", Seen(second, 2));
        Assert.Equal((20L, 20L), (Done(first, 2, 10), Done(second, 2, 10)));
        Assert.Equal(first["city"]!["workers"]!.ToJsonString(), second["city"]!["workers"]!.ToJsonString());
        Assert.Equal(first["city"]!["inventory"]!.ToJsonString(), second["city"]!["inventory"]!.ToJsonString());
        Assert.Equal((long)first["stateVersion"]! + 1, (long)second["stateVersion"]!);
        Assert.Empty(await LedgerAsync(Database, city));
        Assert.Equal(2, (await ReceiptsAsync(Database, player)).Count);

        Factory.Clock.Advance(TimeSpan.FromSeconds(20));
        await ApiClient.GetAsync(Factory, "/api/v1/city", player);
        Assert.Equal("coins 1000, logs 101, planks 50", await StockAsync(Database, city));
    }

    /// <summary>
    /// Two assignments at once that together need more workers than the city has: the house's 4 to each of two idle level-2 woodcutters.
    /// The city's lock puts them one after the other, so exactly one succeeds and the other is <c>INSUFFICIENT_WORKERS</c>; the stored rows
    /// hold 4 workers in all and one receipt.
    /// </summary>
    [Fact]
    public async Task Security_TwoAssignmentsAtOnce_NeverAssignMoreThanProvided()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory,
            "assigner",
            1000,
            100,
            50,
            House(1),
            Woodcutter(2, BuildingPhase.Idle, workers: 0, level: 2),
            Woodcutter(3, BuildingPhase.Idle, workers: 0, x: 2, level: 2));

        ApiResponse[] responses = await Task.WhenAll(AssignAsync(player, Guid.CreateVersion7(), 2, 4), AssignAsync(player, Guid.CreateVersion7(), 3, 4));

        Assert.Equal(
            ["INSUFFICIENT_WORKERS", "OK"],
            responses.Select(response => response.Status == HttpStatusCode.OK ? "OK" : (string)response.Json()["code"]!).Order(StringComparer.Ordinal));
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityBuildingRow> rows = await db.CityBuildings.AsNoTracking().Where(row => row.CityId == city).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, rows.Sum(row => row.AssignedWorkers));
        Assert.Single(await ReceiptsAsync(Database, player));
    }

    /// <summary>
    /// Sends an assignment and asserts the refusal: the status, the code and the request id, and the city stored exactly as before (no time
    /// has elapsed, so not even a settlement is written).
    /// </summary>
    /// <param name="player">The acting player.</param>
    /// <param name="city">Their city.</param>
    /// <param name="buildingId">The building id sent.</param>
    /// <param name="workers">The count sent.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    private async Task AssertRefusedAsync(Guid player, Guid city, int buildingId, int workers, HttpStatusCode status, string code)
    {
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await AssignAsync(player, _requestId, buildingId, workers);

        Assert.Equal(status, response.Status);
        JsonNode problem = response.Json();
        Assert.Equal((code, _requestId.ToString()), ((string)problem["code"]!, (string)problem["requestId"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>Sends an assignment over HTTP with the player's token.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="buildingId">The building.</param>
    /// <param name="workers">The count.</param>
    /// <returns>The response.</returns>
    private Task<ApiResponse> AssignAsync(Guid player, Guid requestId, int buildingId, int workers) =>
        ApiClient.PostAsync(Factory, _path, player, $$"""{"requestId":"{{requestId}}","buildingId":{{buildingId}},"workers":{{workers}}}""");

    /// <summary>A building of a snapshot as one line: phase, level, workers of the max, the phase's start and end, labor, segment, stall.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="buildingId">The building's id.</param>
    /// <returns>For example <c>Producing L1 w2/2 0-30 labor 60/60 segment 0 stall null</c>.</returns>
    private static string Seen(JsonNode snapshot, int buildingId)
    {
        JsonNode b = Find(snapshot, buildingId);
        return $"{b["phase"]} L{b["level"]} w{b["assignedWorkers"]}/{b["maxWorkers"]} {b["phaseStartedAt"]}-{b["phaseEndsAt"]?.ToString() ?? "null"} " +
            $"labor {b["laborRemaining"]}/{b["cycleLabor"]} segment {b["segmentStartedAt"]} stall {b["stallReason"]?.ToString() ?? "null"}";
    }

    /// <summary>The labor a client shows as done at a clock second from a snapshot (<c>05</c> → Snapshot, progress for display).</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="buildingId">The building's id.</param>
    /// <param name="clock">The client's clock.</param>
    /// <returns>The labor done.</returns>
    private static long Done(JsonNode snapshot, int buildingId, long clock)
    {
        JsonNode b = Find(snapshot, buildingId);
        return (long)b["cycleLabor"]! - ((long)b["laborRemaining"]! - ((clock - (long)b["segmentStartedAt"]!) * (long)b["assignedWorkers"]!));
    }

    /// <summary>A building of a snapshot by its id.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="buildingId">The building's id.</param>
    /// <returns>The building's JSON object.</returns>
    private static JsonNode Find(JsonNode snapshot, int buildingId) =>
        snapshot["city"]!["buildings"]!.AsArray().Single(building => (int)building!["id"]! == buildingId)!;
}
