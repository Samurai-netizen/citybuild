using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Audit;
using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Golden;
using CityBuilder.IntegrationTests.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.EndToEnd;

/// <summary>
/// The HTTP end-to-end suite (M0-73): one player plays the Era I loop of <see cref="EraOneLoop"/> over the real API and PostgreSQL, with a
/// fake clock and the frozen content <c>content-e2e-v1.json</c>, from the first login through 30 hours away to a host restart. Each test
/// plays the whole script on its own database and checks one property: the stored city is the engine's, refusals change nothing, the ledger
/// and the receipts agree, the upgrade refunds its cycle, the long absence, the restart.
/// </summary>
/// <param name="server">The run's PostgreSQL server; each test gets a fresh database on it.</param>
[Trait("Category", "Integration")]
public sealed class HttpEndToEndTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The player's display name; logging in again with it finds the same player.</summary>
    private const string _name = "e2e";

    /// <summary>After every step the stored city is the city the engine computes from the same script, presence and research times included.</summary>
    [Fact]
    public async Task Play_EraOneLoopOverHttp_StoresTheCityTheEngineComputesAtEveryStep()
    {
        await using WebApplicationFactory<Program> server = WithLoopContent(Factory);
        var compared = new List<long>();

        await PlayAsync(server, afterStep: async (time, player) =>
        {
            CityAggregate stored = await LoadAsync(server, player.PlayerId);
            CityState engine = EraOneLoop.Replay(time);
            long second = PostgresApiFactory.Start.ToUnixTimeSeconds() + time;
            var expected = new CityAggregate(
                stored.CityId, stored.PlayerId, engine, stored.ResearchCompletedAt, second, stored.StateVersion, EraOneLoop.Content.ContentVersion, second);
            Assert.Equal(SampleCities.Describe(expected), SampleCities.Describe(stored));
            compared.Add(time);
        });

        Assert.Equal(EraOneLoop.Steps.Select(step => step.Time), compared);
    }

    /// <summary>Every refused request answers its code and leaves the stored city, the ledger and the receipts as they were.</summary>
    [Fact]
    public async Task Play_EraOneLoopOverHttp_RefusedRequestsAnswerTheirCodeAndChangeNothing()
    {
        await using WebApplicationFactory<Program> server = WithLoopContent(Factory);

        Played played = await PlayAsync(server, aroundRefusal: async (player, refuse) =>
        {
            string before = await StoredAsync(server, player.PlayerId);
            await refuse();
            Assert.Equal(before, await StoredAsync(server, player.PlayerId));
        });

        Assert.Equal(["NO_FREE_BUILDER", "BUILDING_LOCKED", "TILE_OCCUPIED", "INSUFFICIENT_WORKERS"], played.RefusedCodes);
    }

    /// <summary>Each accepted command has exactly one receipt and nothing else has one; the ledger auditor finds no mismatch in the city.</summary>
    [Fact]
    public async Task Play_EraOneLoopOverHttp_OneReceiptPerAcceptedCommandAndTheAuditorFindsNoMismatch()
    {
        await using WebApplicationFactory<Program> server = WithLoopContent(Factory);

        Played played = await PlayAsync(server);

        await using (var db = Database.CreateContext())
        {
            Guid[] receipts = await db.CommandReceipts.Where(receipt => receipt.PlayerId == played.Player.PlayerId)
                .Select(receipt => receipt.RequestId).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(played.Accepted.Order(), receipts.Order());
        }

        await using AsyncServiceScope scope = server.Services.CreateAsyncScope();
        LedgerAuditReport report = await scope.ServiceProvider.GetRequiredService<LedgerAuditor>().AuditAllAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, report.CitiesAudited);
        Assert.True(report.IsClean, string.Join(Environment.NewLine, report.Mismatches));
    }

    /// <summary>
    /// The trading post's upgrade gets its last plank while a cycle runs (at 20040): the cycle ends without output, its 2 planks go back into
    /// the store, and the ledger says so in one <c>SETTLEMENT_REFUNDED</c> row; the post reaches level 2.
    /// </summary>
    [Fact]
    public async Task Play_EraOneLoopOverHttp_UpgradeRefundsTheInterruptedCycle()
    {
        await using WebApplicationFactory<Program> server = WithLoopContent(Factory);

        Played played = await PlayAsync(server);

        Guid cityId = (await LoadAsync(server, played.Player.PlayerId)).CityId;
        LedgerEntryRow[] refunds = [.. (await LedgerAsync(Database, cityId)).Where(row => row.Reason == "SETTLEMENT_REFUNDED")];
        Assert.Equal([("planks", 2L)], refunds.Select(row => (row.ItemId, row.Delta)));
        Assert.Equal(2, (int)Building(played.Last, EraOneLoop.TradingPost)["level"]!);
    }

    /// <summary>
    /// Thirty hours away: the old token has expired (401) and a new login finds the same player; Tally Sticks finished 900 s after it
    /// started, while the player was away; both couriers spent their stamina, rest, and the report names them as tired.
    /// </summary>
    [Fact]
    public async Task Play_EraOneLoopOverHttp_ThirtyHoursAway_ResearchEndsOnTimeAndCouriersRest()
    {
        await using WebApplicationFactory<Program> server = WithLoopContent(Factory);

        Played played = await PlayAsync(server);

        Assert.Equal(HttpStatusCode.Unauthorized, played.ExpiredTokenStatus);
        Assert.Equal(played.FirstPlayerId, played.Player.PlayerId);
        CityAggregate stored = await LoadAsync(server, played.Player.PlayerId);
        Assert.Equal(
            [("frame_saw", stored.ResearchCompletedAt.Single(done => done.Key.Value == "frame_saw").Value), ("sharp_axes", 5400L), ("tally_sticks", EraOneLoop.LeaveTime + 900), ("trade_charter", 630L)],
            stored.ResearchCompletedAt.Select(done => (done.Key.Value, done.Value)).OrderBy(done => done.Item1, StringComparer.Ordinal));
        JsonNode report = played.Last["settlement"]!;
        Assert.Equal(["tally_sticks"], report["completedResearch"]!.AsArray().Select(id => (string)id!));
        Assert.Equal([(6, 1), (6, 2)], report["tiredCouriers"]!.AsArray().Select(tired => ((int)tired!["stationId"]!, (int)tired["slot"]!)));
        Assert.All(played.Last["city"]!["couriers"]!.AsArray(), courier => Assert.Equal(("Resting", 0L), ((string)courier!["state"]!, (long)courier["stamina"]!)));
    }

    /// <summary>After the loop the host restarts on the same database at the same second: a new login finds the same player and the same city.</summary>
    [Fact]
    public async Task Play_EraOneLoopOverHttp_HostRestart_ReloadsTheSameCity()
    {
        WebApplicationFactory<Program> server = WithLoopContent(Factory);
        Played played;
        string stored;
        try
        {
            played = await PlayAsync(server);
            stored = await StoredAsync(server, played.Player.PlayerId);
            played.Player.Dispose();
        }
        finally
        {
            await server.DisposeAsync();
        }

        await using var restartedFactory = new PostgresApiFactory(Database);
        restartedFactory.Clock.SetUtcNow(Factory.Clock.GetUtcNow());
        await using WebApplicationFactory<Program> restarted = WithLoopContent(restartedFactory);
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(restarted, _name);
        JsonNode reloaded = await player.GetCityAsync();

        Assert.Equal(played.Player.PlayerId, player.PlayerId);
        Assert.Equal(played.Last["city"]!.ToJsonString(), reloaded["city"]!.ToJsonString());
        Assert.Equal(stored, await StoredAsync(restarted, player.PlayerId));
    }

    /// <summary>The tests' server with the loop's frozen content; it shares the factory's clock and database.</summary>
    /// <param name="factory">The server on the test's database.</param>
    /// <returns>The server; the caller disposes it.</returns>
    private static WebApplicationFactory<Program> WithLoopContent(PostgresApiFactory factory) =>
        factory.WithWebHostBuilder(host => host.UseSetting("Content:FilePath", EraOneLoop.ContentPath));

    /// <summary>
    /// Plays the whole script over HTTP: the clock moves to each step's time; at <see cref="EraOneLoop.ReturnTime"/> the old token is tried
    /// once and the player logs in again; each act is one request; accepted ones must answer 200, refused ones their code.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="afterStep">Runs after each step's requests, with the step's time and the current player.</param>
    /// <param name="aroundRefusal">Runs around each refused request; it must call the function it is given exactly once.</param>
    /// <returns>What the run saw.</returns>
    private async Task<Played> PlayAsync(
        WebApplicationFactory<Program> server,
        Func<long, GoldenHttpPlayer, Task>? afterStep = null,
        Func<GoldenHttpPlayer, Func<Task>, Task>? aroundRefusal = null)
    {
        GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(server, _name);
        Guid firstPlayerId = player.PlayerId;
        var accepted = new List<Guid>();
        var refusedCodes = new List<string>();
        HttpStatusCode? expiredTokenStatus = null;
        JsonNode last = null!;
        long clock = 0;
        foreach ((long time, LoopAct[] acts) in EraOneLoop.Steps)
        {
            Factory.Clock.Advance(TimeSpan.FromSeconds(time - clock));
            clock = time;
            if (time == EraOneLoop.ReturnTime)
            {
                expiredTokenStatus = await player.GetCityStatusAsync();
                player.Dispose();
                player = await GoldenHttpPlayer.LoginAsync(server, _name);
            }

            foreach (LoopAct act in acts)
            {
                if (act is LookAct)
                {
                    last = await player.GetCityAsync();
                }
                else if (act is RefusedAct refused)
                {
                    GoldenHttpPlayer current = player;
                    Func<Task> refuse = async () =>
                    {
                        (string command, object payload) = EraOneLoop.Request(refused.Act, Guid.NewGuid());
                        (HttpStatusCode status, JsonNode? body) = await current.SendAsync(command, payload);
                        Assert.True((int)status is >= 400 and < 500, $"t={time} {act}: {(int)status} {body?.ToJsonString()}");
                        Assert.Equal(refused.Code, (string?)body?["code"]);
                        refusedCodes.Add(refused.Code);
                    };
                    await (aroundRefusal is null ? refuse() : aroundRefusal(player, refuse));
                }
                else
                {
                    var requestId = Guid.NewGuid();
                    (string command, object payload) = EraOneLoop.Request(act, requestId);
                    (HttpStatusCode status, JsonNode? body) = await player.SendAsync(command, payload);
                    Assert.True(status == HttpStatusCode.OK, $"t={time} {act}: {(int)status} {body?.ToJsonString()}");
                    accepted.Add(requestId);
                    last = body!;
                }
            }

            if (afterStep is not null)
            {
                await afterStep(time, player);
            }
        }

        return new Played(player, firstPlayerId, accepted, refusedCodes, expiredTokenStatus, last);
    }

    /// <summary>The stored city with its ledger and receipts, as text: what a refused request must not change.</summary>
    /// <param name="server">The server.</param>
    /// <param name="playerId">The player.</param>
    /// <returns>The text.</returns>
    private async Task<string> StoredAsync(WebApplicationFactory<Program> server, Guid playerId)
    {
        CityAggregate city = await LoadAsync(server, playerId);
        await using var db = Database.CreateContext();
        int receipts = await db.CommandReceipts.CountAsync(receipt => receipt.PlayerId == playerId, TestContext.Current.CancellationToken);
        return $"{StateOf(city)}\nversion {city.StateVersion}\nreceipts {receipts}\nledger\n{Totals(await LedgerAsync(Database, city.CityId))}";
    }

    /// <summary>A building of a snapshot by id.</summary>
    /// <param name="snapshot">A city snapshot.</param>
    /// <param name="id">The building's id.</param>
    /// <returns>The building's JSON.</returns>
    private static JsonNode Building(JsonNode snapshot, int id) =>
        snapshot["city"]!["buildings"]!.AsArray().Single(building => (int)building!["id"]! == id)!;

    /// <summary>What one run of the script saw.</summary>
    /// <param name="Player">The player at the end (logged in again after the absence); the caller disposes it.</param>
    /// <param name="FirstPlayerId">The player id of the first login.</param>
    /// <param name="Accepted">The request ids of the accepted commands.</param>
    /// <param name="RefusedCodes">The codes of the refused requests, in order.</param>
    /// <param name="ExpiredTokenStatus">The status of the first token's request after the absence.</param>
    /// <param name="Last">The last snapshot: the look after the absence.</param>
    private sealed record Played(
        GoldenHttpPlayer Player, Guid FirstPlayerId, List<Guid> Accepted, List<string> RefusedCodes, HttpStatusCode? ExpiredTokenStatus, JsonNode Last);
}
