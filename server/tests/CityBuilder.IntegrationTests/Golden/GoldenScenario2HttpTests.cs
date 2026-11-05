using System.Text.Json.Nodes;
using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Golden;

/// <summary>
/// Golden scenario #2 (<c>core-instructions/23-mvp-content.md</c>, CR-0008-T1 part H) played end to end through the real HTTP API on
/// PostgreSQL, with the frozen content <c>content-golden-v2.json</c>, the server clock a <c>FakeTimeProvider</c> and the server's presence
/// grace of 300 s: a dev login creates the city at clock 0, the script's requests are HTTP requests at their seconds
/// (<see cref="GoldenScenario2Http"/>), and the clock moves between them. The looks must show what the table says (the Warehouse's logs,
/// courier 1's stamina, the report of the long absence), the stored city must be the one the engine computes for the same script and
/// absences, and the ledger must add up to the city's totals. If a value fails, check the engine test (<c>GoldenScenario2Tests</c>) first:
/// when that one passes, the difference is in the server (presence, settle, mapping, persistence).
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class GoldenScenario2HttpTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>
    /// The looks and the re-energizing show the table: at 745 the Warehouse holds 2 logs and courier 1 has 14 045 s (355 spent after 390);
    /// at 25000 the report of 745 → 25000 says 148 logs produced, 98 moved, 14 045 courier seconds and courier 1 tired at 15095, and courier 1
    /// rests with 10 logs; re-energizing sends it off with 14 400 s; at 25005 it walks on to the Warehouse. The purse is 440 from t=60 on.
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario2OverHttp_TheLooksShowWhatTheTableSays()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden2");
        var looks = new Dictionary<long, JsonNode>();

        await PlayAsync(player, 25005, async (time, snapshot) =>
        {
            looks[time] = snapshot;
            if (time == 25000)
            {
                looks[-25000] = await player.ReenergizeCouriersAsync();
            }
        });

        Assert.Equal(("""{"logs":2}""", 14_045L), (Store(looks[745], 4), Stamina(looks[745])));
        Assert.Equal((355L, "[]"), ((long)looks[745]["settlement"]!["courierSeconds"]!, looks[745]["settlement"]!["tiredCouriers"]!.ToJsonString()));
        JsonNode report = looks[25000]["settlement"]!;
        Assert.Equal(("""{"logs":148}""", """{"logs":98}"""), (report["produced"]!.ToJsonString(), report["moved"]!.ToJsonString()));
        Assert.Equal((14_045L, """[{"stationId":3,"slot":1,"at":15095}]"""), ((long)report["courierSeconds"]!, report["tiredCouriers"]!.ToJsonString()));
        Assert.Equal(
            """{"stationId":3,"slot":1,"state":"Resting","toStop":0,"departedAt":null,"arrivesAt":null,"stamina":0,"load":[{"itemId":"logs","fromId":2,"quantity":10}]}""",
            Courier(looks[25000]).ToJsonString());
        Assert.Equal(("""{"logs":40}""", """{"logs":100}"""), (Store(looks[25000], 2), Store(looks[25000], 4)));
        Assert.Equal(
            """{"stationId":3,"slot":1,"state":"Walking","toStop":1,"departedAt":25000,"arrivesAt":25005,"stamina":14400,"load":[{"itemId":"logs","fromId":2,"quantity":10}]}""",
            Courier(looks[-25000]).ToJsonString());
        Assert.Equal(("Walking", 2, 25_015L), ((string)Courier(looks[25005])["state"]!, (int)Courier(looks[25005])["toStop"]!, (long)Courier(looks[25005])["arrivesAt"]!));
        Assert.All(looks.Where(look => Math.Abs(look.Key) >= 60).Select(look => look.Value), snapshot => Assert.Equal(440L, (long)snapshot["city"]!["purse"]!));
    }

    /// <summary>
    /// At each look the stored city is the one the engine computes for the same script with the server's absences
    /// (<see cref="GoldenScenario2Http.Replay"/>): every stored value, couriers, stamina and loads included, and the last-seen second is the
    /// look's; the re-energizing at 25000 is sent after that look's comparison and shows at 25005.
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario2OverHttp_StoresTheCityTheEngineComputes()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden2");
        GameContent content = ContentOf(server);

        await PlayAsync(player, 25005, async (time, _) =>
        {
            if (time < 745)
            {
                return;
            }

            CityAggregate stored = await LoadAsync(server, player.PlayerId);
            CityState engine = GoldenScenario2Http.Replay(content, time, withReenergizing: time > 25000);
            long second = PostgresApiFactory.Start.ToUnixTimeSeconds() + time;
            var expected = new CityAggregate(stored.CityId, stored.PlayerId, engine, [], second, stored.StateVersion, content.ContentVersion, second);
            Assert.Equal(SampleCities.Describe(expected), SampleCities.Describe(stored));
            if (time == 25000)
            {
                await player.ReenergizeCouriersAsync();
            }
        });
    }

    /// <summary>
    /// After the script the city's ledger reconciles with the snapshot's totals: for each item the deltas of its rows add up to the total, and
    /// its latest row's balance is the total (couriers' loads count, <c>06</c>).
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario2OverHttp_LedgerReconcilesWithTheTotals()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden2");
        JsonNode last = null!;

        await PlayAsync(player, 25005, (_, snapshot) =>
        {
            last = snapshot;
            return Task.CompletedTask;
        });

        Guid cityId = (await LoadAsync(server, player.PlayerId)).CityId;
        List<LedgerEntryRow> ledger = await LedgerAsync(Database, cityId);
        string[] items = last["city"]!["totals"]!.AsObject().Select(total => total.Key).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            string.Join("\n", items.Select(item => $"{item} sum {(long)last["city"]!["totals"]![item]!} latest {(long)last["city"]!["totals"]![item]!}")),
            string.Join("\n", items.Select(item =>
            {
                LedgerEntryRow[] rows = ledger.Where(row => row.ItemId == item).ToArray();
                return $"{item} sum {rows.Sum(row => row.Delta)} latest {rows.LastOrDefault()?.BalanceAfter ?? 0}";
            })));
    }

    /// <summary>
    /// Plays the script for the player from clock 0 (the login) to <paramref name="until"/>: the clock moves to each request time, the requests
    /// due then are sent, and <paramref name="afterRequests"/> sees the last snapshot.
    /// </summary>
    /// <param name="player">The player, logged in at clock 0.</param>
    /// <param name="until">The last request time to play.</param>
    /// <param name="afterRequests">What the test does after each request time's requests, with the time and the last snapshot.</param>
    /// <returns>A task that completes after the last request.</returns>
    private async Task PlayAsync(GoldenHttpPlayer player, long until, Func<long, JsonNode, Task> afterRequests)
    {
        long clock = 0;
        foreach (long time in GoldenScenario2Http.RequestTimes.Where(time => time <= until))
        {
            Factory.Clock.Advance(TimeSpan.FromSeconds(time - clock));
            clock = time;
            await afterRequests(time, await GoldenScenario2Http.ActAsync(player, time));
        }
    }

    /// <summary>Building <paramref name="id"/>'s store in the snapshot, as JSON text.</summary>
    private static string Store(JsonNode snapshot, int id) =>
        snapshot["city"]!["buildings"]!.AsArray().Single(building => (int)building!["id"]! == id)!["store"]!.ToJsonString();

    /// <summary>Courier 3/1 in the snapshot.</summary>
    private static JsonNode Courier(JsonNode snapshot) =>
        snapshot["city"]!["couriers"]!.AsArray().Single(courier => (int)courier!["stationId"]! == 3 && (int)courier["slot"]! == 1)!;

    /// <summary>Courier 3/1's stamina in the snapshot.</summary>
    private static long Stamina(JsonNode snapshot) => (long)Courier(snapshot)["stamina"]!;
}
