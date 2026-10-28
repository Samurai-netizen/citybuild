using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Security;

/// <summary>
/// Moving workers between buildings can't make more than the labor allows (<c>core-instructions/07-security-model.md</c>, "Assign more
/// workers than exist, or use workers twice"; <c>04</c> section 3), checked property-style end to end on PostgreSQL over HTTP. For each fixed
/// seed, a city with one house (4 workers) and two idle level-2 woodcutters (60 labor a log, at most 4 workers each, no inputs) gets a loop of
/// waits and assignments to either woodcutter, including raising one before lowering the other, counts above the max and the same count
/// again. The test keeps its own model of the counts and checks every answer against it, and at the end the logs against the worker-seconds it
/// delivered. A failure names the seed and the step.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class WorkerLaborDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/assign-workers";

    /// <summary>The worker-seconds a log takes (the woodcutter's recipe in the real content).</summary>
    private const long _laborPerLog = 60;

    /// <summary>The workers the house provides and the max of a level-2 woodcutter.</summary>
    private const int _workers = 4;

    /// <summary>The assignments of one run.</summary>
    private const int _steps = 40;

    /// <summary>
    /// For each seed, at every step the server agrees with the model: an assignment succeeds exactly when it is at most the max and its
    /// increase at most the free workers (otherwise <c>WORKER_LIMIT</c> or <c>INSUFFICIENT_WORKERS</c>), and the snapshot never shows more
    /// assigned than provided or a building above its max. At the end, every worker-second delivered (the model's counts times the seconds
    /// waited) is accounted for: <c>60 × logs + labor done in the running cycles ≤ delivered</c>, and the difference is only the rounding of
    /// cycle ends (each finished cycle wastes at most workers − 1 ≤ 3 worker-seconds). The stock's logs equal the ledger's. Each run makes
    /// logs and sees both refusals, so the property is not vacuous.
    /// </summary>
    /// <param name="seed">The seed of the run.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Security_MovingWorkersBetweenBuildings_NeverMakesMoreThanTheLaborAllows(int seed)
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory,
            "mover",
            1000,
            0,
            0,
            House(1),
            Woodcutter(2, BuildingPhase.Idle, workers: 0, level: 2),
            Woodcutter(3, BuildingPhase.Idle, workers: 0, x: 2, level: 2));
        ulong random = (ulong)seed;
        int[] assigned = [0, 0];
        long delivered = 0;
        (int ok, int workerLimit, int insufficient) = (0, 0, 0);

        for (int step = 0; step < _steps; step++)
        {
            int wait = Next(ref random, 45);
            delivered += (assigned[0] + assigned[1]) * (long)wait;
            Factory.Clock.Advance(TimeSpan.FromSeconds(wait));
            int target = Next(ref random, 1);
            int workers = Next(ref random, _workers + 1);
            string at = string.Create(CultureInfo.InvariantCulture, $"seed {seed}, step {step}: {workers} on building {target + 2} with {assigned[0]}+{assigned[1]}");

            ApiResponse response = await ApiClient.PostAsync(
                Factory, _path, player, $$"""{"requestId":"{{Guid.CreateVersion7()}}","buildingId":{{target + 2}},"workers":{{workers}}}""");

            string expected = workers > _workers ? "WORKER_LIMIT"
                : workers - assigned[target] > _workers - assigned[0] - assigned[1] ? "INSUFFICIENT_WORKERS"
                : "OK";
            string actual = response.Status == HttpStatusCode.OK ? "OK" : (string)response.Json()["code"]!;
            Assert.True(expected == actual, $"{at}: expected {expected}, got {actual}.");
            if (actual == "OK")
            {
                assigned[target] = workers;
                AssertSnapshotAgrees(response.Json(), assigned, at);
                ok++;
            }
            else if (actual == "WORKER_LIMIT")
            {
                workerLimit++;
            }
            else
            {
                insufficient++;
            }
        }

        int last = Next(ref random, 45);
        delivered += (assigned[0] + assigned[1]) * (long)last;
        Factory.Clock.Advance(TimeSpan.FromSeconds(last));
        JsonNode end = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        string run = $"seed {seed}";
        AssertSnapshotAgrees(end, assigned, run + ", end");
        long clock = (long)end["city"]!["clock"]!;
        long logs = (long?)end["city"]!["inventory"]!["logs"] ?? 0;
        long inCycles = LaborDone(Find(end, 2), clock) + LaborDone(Find(end, 3), clock);
        long accounted = (_laborPerLog * logs) + inCycles;
        string totals = $"{run}: delivered {delivered}, logs {logs}, in cycles {inCycles}";
        Assert.True(accounted <= delivered, $"{totals}: more labor made than delivered.");
        Assert.True(delivered - accounted <= (_workers - 1) * logs, $"{totals}: more labor lost than the rounding of cycle ends.");
        Assert.True(logs > 0 && ok > 0 && workerLimit > 0 && insufficient > 0, $"{totals}, {ok} ok, {workerLimit} over the max, {insufficient} too few free: the run proves too little.");
        long ledgerLogs = (await LedgerAsync(Database, city)).Where(row => row.ItemId == "logs").Sum(row => row.Delta);
        Assert.True(ledgerLogs == logs, $"{totals}: the ledger's logs are {ledgerLogs}.");
    }

    /// <summary>
    /// Asserts that a snapshot shows the model's counts on the two woodcutters, no building above its max, and no more assigned than provided.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="assigned">The model's counts of woodcutters 2 and 3.</param>
    /// <param name="at">The seed and step, for the failure message.</param>
    private static void AssertSnapshotAgrees(JsonNode snapshot, int[] assigned, string at)
    {
        JsonNode workers = snapshot["city"]!["workers"]!;
        (int provided, int total, int free) = ((int)workers["provided"]!, (int)workers["assigned"]!, (int)workers["free"]!);
        int[] seen = [(int)Find(snapshot, 2)["assignedWorkers"]!, (int)Find(snapshot, 3)["assignedWorkers"]!];
        Assert.True(seen[0] == assigned[0] && seen[1] == assigned[1], $"{at}: the snapshot has {seen[0]}+{seen[1]}.");
        Assert.True(
            provided == _workers && total == seen[0] + seen[1] && free == provided - total && free >= 0,
            $"{at}: the workers block is {workers.ToJsonString()}.");
        Assert.True(
            snapshot["city"]!["buildings"]!.AsArray().All(building => (int)building!["assignedWorkers"]! <= (int)building["maxWorkers"]!),
            $"{at}: a building is above its max.");
    }

    /// <summary>
    /// The labor done so far in a building's running cycle at a clock second (<c>05</c> → Snapshot, progress for display), or 0 when it runs
    /// none.
    /// </summary>
    /// <param name="building">The building's JSON object.</param>
    /// <param name="clock">The clock second.</param>
    /// <returns>The worker-seconds done.</returns>
    private static long LaborDone(JsonNode building, long clock) => (string)building["phase"]! != "Producing" ? 0
        : (long)building["cycleLabor"]! - ((long)building["laborRemaining"]! - ((clock - (long)building["segmentStartedAt"]!) * (long)building["assignedWorkers"]!));

    /// <summary>A building of a snapshot by its id.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="buildingId">The building's id.</param>
    /// <returns>The building's JSON object.</returns>
    private static JsonNode Find(JsonNode snapshot, int buildingId) =>
        snapshot["city"]!["buildings"]!.AsArray().Single(building => (int)building!["id"]! == buildingId)!;

    /// <summary>
    /// The next number from 0 to <paramref name="max"/> of a linear congruential generator with Knuth's MMIX constants: written out, so a seed
    /// gives the same run on every machine and runtime. The bias of the modulo doesn't matter for picking test cases.
    /// </summary>
    /// <param name="state">The generator's state, advanced.</param>
    /// <param name="max">The largest number.</param>
    /// <returns>The number.</returns>
    private static int Next(ref ulong state, int max)
    {
        state = unchecked((state * 6364136223846793005UL) + 1442695040888963407UL);
        return (int)((state >> 33) % (ulong)(max + 1));
    }
}
