using System.Globalization;
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
/// Golden scenario #1 (<c>core-instructions/23-mvp-content.md</c>) played end to end through the real HTTP API on PostgreSQL, with the frozen
/// content <c>content-golden-v1.json</c> and the server clock a <c>FakeTimeProvider</c>: a dev login creates the city at clock 0, the script's
/// commands are HTTP requests at t = 0, 30 and 750 (<see cref="GoldenScenario1Http"/>), the clock moves between them, and <c>GET /city</c>
/// reads the city at every row of the table. Each snapshot must show the row the engine's golden test asserts
/// (<c>CityBuilder.Simulation.Tests/Golden/GoldenScenario1Tests</c>: the same numbers, phases, stall reasons and research), the stored city
/// must be the city the engine computes for the same script, and the ledger must add up to the stock. Then the scenario continues: the
/// Trading Post at 1 worker earns what it earns at 2 and frees a worker. If a row fails, check the engine test first: when that one passes,
/// the difference is in the server (settle, mapping, persistence), not in the table.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class GoldenScenario1HttpTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The two finished Cottages as <see cref="Describe"/> shows them: idle, and a house has no recipe.</summary>
    private const string _houses = "1 Idle NoRecipe | 2 Idle NoRecipe";
    /// <summary>The research while <c>trade_charter</c> runs (until t=600).</summary>
    private const string _researching = "active trade_charter until 600";
    /// <summary>The research once <c>trade_charter</c> is done.</summary>
    private const string _researched = "completed trade_charter";
    /// <summary>The city-clock second <c>trade_charter</c> completes at: started at 0, it takes 600 s.</summary>
    private const long _tradeCharterCompletesAt = 600;
    /// <summary>The Trading Post's building id: the fifth building placed.</summary>
    private const int _tradingPostId = 5;

    /// <summary>
    /// The table of <c>23</c> as <see cref="Describe"/> writes a snapshot, row by row: the clock, the stock, the free workers, every building
    /// with its phase and stall reason, and the research. The values are the engine golden test's, checked by hand in M0-11.
    /// </summary>
    private static readonly (long Time, string Row)[] _table =
    [
        (0, "t=0 coins 450 logs 40 planks 10 free 0 | 1 Constructing | 2 Constructing | " + _researching),
        (30, "t=30 coins 220 logs 10 planks 10 free 8 | " + _houses + " | 3 Constructing | 4 Constructing | " + _researching),
        (90, "t=90 coins 220 logs 10 planks 10 free 6 | " + _houses + " | 3 Producing | 4 Constructing | " + _researching),
        (150, "t=150 coins 220 logs 10 planks 10 free 3 | " + _houses + " | 3 Producing | 4 Producing | " + _researching),
        (600, "t=600 coins 220 logs 11 planks 17 free 3 | " + _houses + " | 3 Producing | 4 Producing | " + _researched),
        (750, "t=750 coins 20 logs 10 planks 0 free 3 | " + _houses + " | 3 Producing | 4 Producing | 5 Constructing | " + _researched),
        (930, "t=930 coins 20 logs 10 planks 1 free 1 | " + _houses + " | 3 Producing | 4 Producing | 5 Producing | " + _researched),
        (990, "t=990 coins 30 logs 10 planks 0 free 1 | " + _houses + " | 3 Producing | 4 Producing | 5 Producing | " + _researched),
        (1050, "t=1050 coins 40 logs 10 planks 1 free 1 | " + _houses + " | 3 Producing | 4 Producing | 5 Idle MissingInput:planks | " + _researched),
        (1170, "t=1170 coins 50 logs 10 planks 1 free 1 | " + _houses + " | 3 Producing | 4 Producing | 5 Idle MissingInput:planks | " + _researched),
    ];

    /// <summary>The times of the table's rows.</summary>
    private static IEnumerable<long> RowTimes => _table.Select(row => row.Time);

    /// <summary>
    /// At every row of the table, <c>GET /city</c> shows exactly the row: the coins, logs, planks and free workers, each building's phase and
    /// stall reason, and the research. Just before the Trading Post is placed at 750 the city has the 220 coins and 20 planks of the row's
    /// note, and the request at 600 is the one whose settlement completes <c>trade_charter</c>.
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario1OverHttp_EverySnapshotShowsItsTableRow()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden");

        await PlayAsync(
            [player],
            RowTimes,
            atRow: async time =>
            {
                JsonNode snapshot = await player.GetCityAsync();
                Assert.Equal(_table.Single(row => row.Time == time).Row, Describe(snapshot));
                if (time == _tradeCharterCompletesAt)
                {
                    Assert.Equal("""["trade_charter"]""", snapshot["settlement"]!["completedResearch"]!.ToJsonString());
                }
            },
            beforeActions: async time =>
            {
                if (time == 750)
                {
                    JsonNode before = await player.GetCityAsync();
                    Assert.Equal((220L, 20L), (Quantity(before, "coins"), Quantity(before, "planks")));
                }
            });
    }

    /// <summary>
    /// At every row of the table the stored city is the one the engine computes for the same script alone (<see cref="GoldenScenario1Http.Replay"/>):
    /// every stored value (clock, stock, research and when it completed, each building's phase, timers, workers and labor), settled at the
    /// server second of the row. The server's many settles (one per request) give the engine's city, as split invariance promises (<c>04</c>).
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario1OverHttp_StoresTheCityTheEngineComputes()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden");
        GameContent content = ContentOf(server);

        await PlayAsync(
            [player],
            RowTimes,
            atRow: async time =>
            {
                await player.GetCityAsync();
                CityAggregate stored = await LoadAsync(server, player.PlayerId);
                CityState engine = GoldenScenario1Http.Replay(content, time);
                var expected = new CityAggregate(
                    stored.CityId,
                    stored.PlayerId,
                    engine,
                    engine.CompletedResearch.Select(id => KeyValuePair.Create(id, _tradeCharterCompletesAt)),
                    PostgresApiFactory.Start.ToUnixTimeSeconds() + time,
                    stored.StateVersion,
                    content.ContentVersion);
                Assert.Equal(SampleCities.Describe(expected), SampleCities.Describe(stored));
            });
    }

    /// <summary>
    /// At every row of the table the city's ledger reconciles with the snapshot's stock: for each item, the deltas of all its rows (the
    /// starting grant, the costs, the settlements' production and consumption) add up to the quantity, and its latest row's balance is the
    /// quantity. An item the snapshot leaves out (none in stock) must come to 0.
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario1OverHttp_LedgerReconcilesWithTheStockAtEveryRow()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden");

        await PlayAsync(
            [player],
            RowTimes,
            atRow: async time =>
            {
                JsonNode snapshot = await player.GetCityAsync();
                Guid cityId = (await LoadAsync(server, player.PlayerId)).CityId;
                List<LedgerEntryRow> ledger = await LedgerAsync(Database, cityId);
                string[] items = ledger.Select(row => row.ItemId)
                    .Concat(snapshot["city"]!["inventory"]!.AsObject().Select(stock => stock.Key))
                    .Distinct().Order(StringComparer.Ordinal).ToArray();

                Assert.Equal(
                    string.Join("\n", items.Select(item => $"t={time} {item} sum {Quantity(snapshot, item)} latest {Quantity(snapshot, item)}")),
                    string.Join("\n", items.Select(item =>
                    {
                        LedgerEntryRow[] rows = ledger.Where(row => row.ItemId == item).ToArray();
                        return $"t={time} {item} sum {rows.Sum(row => row.Delta)} latest {rows.LastOrDefault()?.BalanceAfter}";
                    })));
            });
    }

    /// <summary>
    /// The scenario's closing note: two players play the script in step; at 1170 one of them sets the Trading Post to 1 worker. Over the next
    /// hour both earn about 300 coins (+10 every 120 s, one Sawmill feeds the post at half speed), the two differ by at most one cycle (10
    /// coins), and the city with 1 worker on the post has one more free worker.
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario1ThenTradingPostAtOneWorker_EarnsTheSameAndFreesAWorker()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer twoWorkers = await GoldenHttpPlayer.LoginAsync(server, "golden_two");
        using GoldenHttpPlayer oneWorker = await GoldenHttpPlayer.LoginAsync(server, "golden_one");
        (long Two, long One) coinsAt1170 = default;

        await PlayAsync(
            [twoWorkers, oneWorker],
            [1170],
            atRow: async _ =>
            {
                long two = Quantity(await twoWorkers.GetCityAsync(), "coins");
                JsonNode assigned = await oneWorker.AssignWorkersAsync(_tradingPostId, 1);
                coinsAt1170 = (two, Quantity(assigned, "coins"));
            });
        Factory.Clock.Advance(TimeSpan.FromHours(1));
        JsonNode twoLater = await twoWorkers.GetCityAsync();
        JsonNode oneLater = await oneWorker.GetCityAsync();

        long earnedTwo = Quantity(twoLater, "coins") - coinsAt1170.Two;
        long earnedOne = Quantity(oneLater, "coins") - coinsAt1170.One;
        Assert.Equal((50L, 50L), coinsAt1170);
        Assert.InRange(earnedTwo, 290, 310);
        Assert.InRange(earnedOne, earnedTwo - 10, earnedTwo + 10);
        Assert.Equal((2, 1), (AssignedWorkers(twoLater, _tradingPostId), AssignedWorkers(oneLater, _tradingPostId)));
        Assert.Equal(FreeWorkers(twoLater) + 1, FreeWorkers(oneLater));
    }

    /// <summary>
    /// Plays the script for the players in step, from clock 0 (their logins) to the last of <paramref name="rowTimes"/>: the clock moves to
    /// each action time and each row time in order; at an action time <paramref name="beforeActions"/> runs, then every player's commands;
    /// at a row time <paramref name="atRow"/> runs (after the actions when both fall on one second).
    /// </summary>
    /// <param name="players">The players, logged in at clock 0.</param>
    /// <param name="rowTimes">The city-clock seconds to stop at for <paramref name="atRow"/>, ascending.</param>
    /// <param name="atRow">What the test checks at a row time.</param>
    /// <param name="beforeActions">What the test checks at an action time before the commands, or <see langword="null"/> for nothing.</param>
    /// <returns>A task that completes at the last row.</returns>
    private async Task PlayAsync(
        IReadOnlyList<GoldenHttpPlayer> players, IEnumerable<long> rowTimes, Func<long, Task> atRow, Func<long, Task>? beforeActions = null)
    {
        long[] rows = rowTimes.ToArray();
        long clock = 0;
        foreach (long time in GoldenScenario1Http.ActionTimes.Where(action => action <= rows[^1]).Concat(rows).Distinct().Order())
        {
            Factory.Clock.Advance(TimeSpan.FromSeconds(time - clock));
            clock = time;
            if (GoldenScenario1Http.ActionTimes.Contains(time))
            {
                if (beforeActions is not null)
                {
                    await beforeActions(time);
                }

                foreach (GoldenHttpPlayer player in players)
                {
                    await GoldenScenario1Http.ActAsync(player, time);
                }
            }

            if (rows.Contains(time))
            {
                await atRow(time);
            }
        }
    }

    /// <summary>
    /// A snapshot as one line in the form of <see cref="_table"/>: the clock, coins, logs, planks and free workers, then each building as its
    /// id and phase followed by its stall reason when it has one, then the research as "active id until end" or "completed" and the nodes.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The line.</returns>
    private static string Describe(JsonNode snapshot)
    {
        JsonNode city = snapshot["city"]!;
        JsonNode research = city["research"]!;
        IEnumerable<string> buildings = city["buildings"]!.AsArray().Select(building =>
        {
            string text = $"{(int)building!["id"]!} {(string)building["phase"]!}";
            return building["stallReason"] is { } stall ? text + " " + (string)stall! : text;
        });
        string researchText = research["activeId"] is { } active
            ? string.Create(CultureInfo.InvariantCulture, $"active {(string)active!} until {(long)research["endsAt"]!}")
            : "completed " + string.Join(", ", research["completed"]!.AsArray().Select(id => (string)id!));
        return string.Join(
            " | ",
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"t={(long)city["clock"]!} coins {Quantity(snapshot, "coins")} logs {Quantity(snapshot, "logs")} " +
                    $"planks {Quantity(snapshot, "planks")} free {FreeWorkers(snapshot)}"),
                .. buildings,
                researchText,
            ]);
    }

    /// <summary>The snapshot's quantity of an item; 0 when the inventory leaves it out (<c>05</c>: an item the city has none of is absent).</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="item">The item id.</param>
    /// <returns>The quantity.</returns>
    private static long Quantity(JsonNode snapshot, string item) =>
        snapshot["city"]!["inventory"]![item] is { } quantity ? (long)quantity : 0;

    /// <summary>The snapshot's free workers.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns><c>city.workers.free</c>.</returns>
    private static int FreeWorkers(JsonNode snapshot) => (int)snapshot["city"]!["workers"]!["free"]!;

    /// <summary>The workers assigned to one building of the snapshot.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="buildingId">The building's id.</param>
    /// <returns>Its <c>assignedWorkers</c>.</returns>
    private static int AssignedWorkers(JsonNode snapshot, int buildingId) =>
        (int)snapshot["city"]!["buildings"]!.AsArray().Single(building => (int)building!["id"]! == buildingId)!["assignedWorkers"]!;
}
