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
/// Golden scenario #1 (<c>core-instructions/23-mvp-content.md</c>, CR-0008) played end to end through the real HTTP API on PostgreSQL, with
/// the frozen content <c>content-golden-v2.json</c> and the server clock a <c>FakeTimeProvider</c>: a dev login creates the city at clock 0,
/// the script's commands (placements, the research, the manual trips) are HTTP requests at their seconds (<see cref="GoldenScenario1Http"/>),
/// the clock moves between them, and <c>GET /city</c> reads the city at every row of the table. Each snapshot must show the row the engine's
/// golden test asserts (<c>CityBuilder.Simulation.Tests/Golden/GoldenScenario1Tests</c>: the same purse, free workers, stores, needs, phases,
/// stall reasons and research), the stored city must be the city the engine computes for the same script, and the ledger must add up to the
/// city's totals. If a row fails, check the engine test first: when that one passes, the difference is in the server (settle, mapping,
/// persistence), not in the table.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class GoldenScenario1HttpTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The research while <c>trade_charter</c> runs (from 60 until 660).</summary>
    private const string _researching = "active trade_charter until 660";

    /// <summary>The research once <c>trade_charter</c> is done.</summary>
    private const string _researched = "completed trade_charter";

    /// <summary>The city-clock second <c>trade_charter</c> completes at: started at 60, it takes 600 s.</summary>
    private const long _tradeCharterCompletesAt = 660;

    /// <summary>
    /// The table of <c>23</c> as <see cref="Describe"/> writes a snapshot, row by row: the clock, the purse, the free workers, the Woodcutter's
    /// logs, the Sawmill's logs and planks, what waits for goods, every building with its phase and stall reason, and the research. The values
    /// are the engine golden test's, checked by hand in CR-0008-T1 part B3.
    /// </summary>
    private static readonly (long Time, string Row)[] _table =
    [
        (0, "t=0 purse 670 free 0 | woodcutter 0 | sawmill — | waiting  | 1 Constructing | 2 Constructing | none"),
        (30, "t=30 purse 570 free 4 | woodcutter 0 | sawmill — | waiting  | 1 Idle NoRecipe | 2 Constructing | 3 Constructing | none"),
        (60, "t=60 purse 220 free 2 | woodcutter 0 | sawmill 0 / 0 | waiting 4: 10 logs | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Constructing | 5 Constructing | " + _researching),
        (90, "t=90 purse 220 free 6 | woodcutter 1 | sawmill 0 / 0 | waiting 4: 10 logs | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Constructing | 5 Idle NoRecipe | " + _researching),
        (360, "t=360 purse 220 free 5 | woodcutter 0 | sawmill 0 / 0 | waiting 4: 10 logs | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Constructing | 5 Idle NoRecipe | " + _researching),
        (370, "t=370 purse 220 free 6 | woodcutter 0 | sawmill 0 / 0 | waiting  | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Constructing | 5 Idle NoRecipe | " + _researching),
        (490, "t=490 purse 220 free 2 | woodcutter 0 | sawmill 0 / 0 | waiting  | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Idle MissingInput:logs | 5 Idle NoRecipe | " + _researching),
        (500, "t=500 purse 220 free 3 | woodcutter 0 | sawmill 2 / 0 | waiting  | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Producing | 5 Idle NoRecipe | " + _researching),
        (560, "t=560 purse 220 free 3 | woodcutter 2 | sawmill 0 / 1 | waiting  | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Producing | 5 Idle NoRecipe | " + _researching),
        (620, "t=620 purse 220 free 3 | woodcutter 4 | sawmill 0 / 2 | waiting  | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Idle MissingInput:logs | 5 Idle NoRecipe | " + _researching),
        (660, "t=660 purse 20 free 1 | woodcutter 3 | sawmill 0 / 0 | waiting 6: 10 planks | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Idle MissingInput:logs | 5 Idle NoRecipe | 6 Constructing | " + _researched),
        (670, "t=670 purse 20 free 3 | woodcutter 3 | sawmill 1 / 0 | waiting 6: 8 planks | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Producing | 5 Idle NoRecipe | 6 Constructing | " + _researched),
        (730, "t=730 purse 20 free 3 | woodcutter 5 | sawmill 1 / 1 | waiting 6: 8 planks | 1 Idle NoRecipe | 2 Producing | 3 Idle NoRecipe | "
            + "4 Idle MissingInput:logs | 5 Idle NoRecipe | 6 Constructing | " + _researched),
    ];

    /// <summary>The times of the table's rows.</summary>
    private static IEnumerable<long> RowTimes => _table.Select(row => row.Time);

    /// <summary>
    /// At every row of the table, <c>GET /city</c> shows exactly the row. The request at 370 is the one whose settlement delivers the 10 logs
    /// to the site, and the one at 660 completes <c>trade_charter</c>.
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario1OverHttp_EverySnapshotShowsItsTableRow()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden");

        await PlayAsync(
            player,
            RowTimes,
            atRow: async time =>
            {
                JsonNode snapshot = await player.GetCityAsync();
                Assert.Equal(_table.Single(row => row.Time == time).Row, Describe(snapshot));
                if (time == 370)
                {
                    Assert.Equal("""{"logs":10}""", snapshot["settlement"]!["delivered"]!.ToJsonString());
                }
            },
            beforeActions: async time =>
            {
                if (time == _tradeCharterCompletesAt)
                {
                    JsonNode before = await player.GetCityAsync();
                    Assert.Equal("""["trade_charter"]""", before["settlement"]!["completedResearch"]!.ToJsonString());
                }
            });
    }

    /// <summary>
    /// At every row of the table the stored city is the one the engine computes for the same script alone (<see cref="GoldenScenario1Http.Replay"/>):
    /// every stored value (clock, purse, stores, needs, trips, research and when it completed, each building's phase, timers, workers and
    /// labor), settled at the server second of the row. The server's many settles (one per request) give the engine's city, as split
    /// invariance promises (<c>04</c>).
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario1OverHttp_StoresTheCityTheEngineComputes()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden");
        GameContent content = ContentOf(server);

        await PlayAsync(
            player,
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
    /// At every row of the table the city's ledger reconciles with the snapshot's totals (CR-0008: stores, loads and the purse): for each
    /// item, the deltas of all its rows (the starting grant, the coins of the costs, the settlements' production, consumption and deliveries)
    /// add up to the total, and its latest row's balance is the total. An item with no rows must have a total of 0.
    /// </summary>
    [Fact]
    public async Task Play_GoldenScenario1OverHttp_LedgerReconcilesWithTheTotalsAtEveryRow()
    {
        await using WebApplicationFactory<Program> server = GoldenScenario1Http.WithGoldenContent(Factory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, "golden");

        await PlayAsync(
            player,
            RowTimes,
            atRow: async time =>
            {
                JsonNode snapshot = await player.GetCityAsync();
                Guid cityId = (await LoadAsync(server, player.PlayerId)).CityId;
                List<LedgerEntryRow> ledger = await LedgerAsync(Database, cityId);
                string[] items = snapshot["city"]!["totals"]!.AsObject().Select(total => total.Key).Order(StringComparer.Ordinal).ToArray();

                Assert.Equal(
                    string.Join("\n", items.Select(item => $"t={time} {item} sum {Total(snapshot, item)} latest {Total(snapshot, item)}")),
                    string.Join("\n", items.Select(item =>
                    {
                        LedgerEntryRow[] rows = ledger.Where(row => row.ItemId == item).ToArray();
                        return $"t={time} {item} sum {rows.Sum(row => row.Delta)} latest {rows.LastOrDefault()?.BalanceAfter ?? 0}";
                    })));
            });
    }

    /// <summary>
    /// Plays the script for the player from clock 0 (the login) to the last of <paramref name="rowTimes"/>: the clock moves to each action time
    /// and each row time in order; at an action time <paramref name="beforeActions"/> runs, then the commands; at a row time
    /// <paramref name="atRow"/> runs (after the actions when both fall on one second).
    /// </summary>
    /// <param name="player">The player, logged in at clock 0.</param>
    /// <param name="rowTimes">The city-clock seconds to stop at for <paramref name="atRow"/>, ascending.</param>
    /// <param name="atRow">What the test checks at a row time.</param>
    /// <param name="beforeActions">What the test checks at an action time before the commands, or <see langword="null"/> for nothing.</param>
    /// <returns>A task that completes at the last row.</returns>
    private async Task PlayAsync(GoldenHttpPlayer player, IEnumerable<long> rowTimes, Func<long, Task> atRow, Func<long, Task>? beforeActions = null)
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

                await GoldenScenario1Http.ActAsync(player, time);
            }

            if (rows.Contains(time))
            {
                await atRow(time);
            }
        }
    }

    /// <summary>
    /// A snapshot as one line in the form of <see cref="_table"/>: the clock, purse and free workers; the Woodcutter's (2) logs; the Sawmill's (4)
    /// logs and planks or "—" before it is placed; each building with needs as "id: quantity item"; each building as its id and phase followed
    /// by its stall reason when it has one; the research as "active id until end", "completed" and the nodes, or "none".
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The line.</returns>
    private static string Describe(JsonNode snapshot)
    {
        JsonNode city = snapshot["city"]!;
        JsonNode research = city["research"]!;
        JsonArray buildings = city["buildings"]!.AsArray();
        JsonNode? sawmill = buildings.SingleOrDefault(building => (int)building!["id"]! == 4);
        string waiting = string.Join(" | ", buildings
            .Where(building => building!["needs"]!.AsObject().Count > 0)
            .Select(building => $"{(int)building!["id"]!}: " + string.Join(", ", building["needs"]!.AsObject().Select(need => $"{(long)need.Value!} {need.Key}"))));
        IEnumerable<string> phases = buildings.Select(building =>
        {
            string text = $"{(int)building!["id"]!} {(string)building["phase"]!}";
            return building["stallReason"] is { } stall ? text + " " + (string)stall! : text;
        });
        string completed = string.Join(", ", research["completed"]!.AsArray().Select(id => (string)id!));
        string researchText = research["activeId"] is { } active
            ? string.Create(CultureInfo.InvariantCulture, $"active {(string)active!} until {(long)research["endsAt"]!}")
            : completed.Length == 0 ? "none" : "completed " + completed;
        return string.Join(
            " | ",
            [
                string.Create(CultureInfo.InvariantCulture, $"t={(long)city["clock"]!} purse {(long)city["purse"]!} free {(long)city["workers"]!["free"]!}"),
                "woodcutter " + Stored(buildings.Single(building => (int)building!["id"]! == 2), "logs"),
                "sawmill " + (sawmill is null ? "—" : Stored(sawmill, "logs") + " / " + Stored(sawmill, "planks")),
                "waiting " + waiting,
                .. phases,
                researchText,
            ]);
    }

    /// <summary>A building's stored quantity of an item in the snapshot; 0 when its store leaves the item out.</summary>
    /// <param name="building">The building's snapshot.</param>
    /// <param name="item">The item id.</param>
    /// <returns>The quantity.</returns>
    private static long Stored(JsonNode? building, string item) => building!["store"]![item] is { } quantity ? (long)quantity : 0;

    /// <summary>The snapshot's city total of an item (<c>05</c>: every item of the content is listed).</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="item">The item id.</param>
    /// <returns>The total.</returns>
    private static long Total(JsonNode snapshot, string item) => (long)snapshot["city"]!["totals"]![item]!;
}
