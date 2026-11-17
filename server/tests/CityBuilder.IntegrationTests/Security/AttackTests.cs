using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.IntegrationTests.EndToEnd;
using CityBuilder.IntegrationTests.Golden;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Mvc.Testing;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Security;

/// <summary>
/// The attack suite (M0-74): the attacks of <c>core-instructions/07-security-model.md</c> sent over HTTP to every command endpoint, against a
/// city played over the real API and PostgreSQL (the Era I loop of <see cref="EraOneLoop"/> up to <see cref="_playedUntil"/>: houses, a
/// scriptorium, a woodcutter, a sawmill, a courier station with routes and a trading post). Each test checks the answer and that the stored
/// city, its ledger and its receipts are unchanged, or changed only as allowed. Attacks that one endpoint's tests already prove on PostgreSQL
/// are not repeated here; <c>docs/security/MVP-SECURITY-BOUNDARIES.md</c> links each attack to its test.
/// </summary>
/// <param name="server">The run's PostgreSQL server; each test gets a fresh database on it.</param>
[Trait("Category", "Integration")]
public sealed class AttackTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The script time the owner's city is played to: every building of the loop stands, the station has two routes.</summary>
    private const long _playedUntil = 1260;

    /// <summary>
    /// One body per command endpoint, with the request id as <c>{0}</c>: a form the endpoint binds, so only an added field can make it 400.
    /// </summary>
    public static TheoryData<string, string> EveryCommand => new()
    {
        { "place-building", """{"requestId":"{0}","buildingTypeId":"house","x":7,"y":7}""" },
        { "upgrade-building", """{"requestId":"{0}","buildingId":1}""" },
        { "assign-workers", """{"requestId":"{0}","buildingId":3,"workers":1}""" },
        { "start-research", """{"requestId":"{0}","researchId":"sharp_axes"}""" },
        { "place-roads", """{"requestId":"{0}","tiles":[{"x":7,"y":7}]}""" },
        { "upgrade-roads", """{"requestId":"{0}","tiles":[{"x":7,"y":7}]}""" },
        { "remove-roads", """{"requestId":"{0}","tiles":[{"x":7,"y":7}]}""" },
        { "start-trip", """{"requestId":"{0}","sourceId":5,"targetId":7,"itemId":"planks","amount":"all"}""" },
        { "set-route", """{"requestId":"{0}","stationId":6,"slot":1,"stops":[{"buildingId":3,"actions":[{"kind":"take","itemId":"logs","amount":"all"}]}]}""" },
        { "set-paused", """{"requestId":"{0}","buildingId":7,"paused":true}""" },
        { "reenergize-couriers", """{"requestId":"{0}"}""" },
        { "cancel-delivery", """{"requestId":"{0}","stationId":6,"slot":1,"lineId":null}""" },
    };

    /// <summary>The fields a client may never send (`02` rule 2), each as the JSON it is added with.</summary>
    private static readonly string[] _forbiddenFields =
    [
        "\"coins\":1000000", "\"resources\":{\"planks\":999}", "\"cost\":{\"coins\":0}", "\"labor\":0", "\"rate\":100", "\"level\":5",
        "\"phaseEndsAt\":0", "\"workers\":99", "\"freeWorkers\":99", "\"playerId\":\"1de9e700-0000-4000-8000-0000000003b2\"", "\"clock\":0",
    ];

    /// <summary>
    /// Every command endpoint refuses a body that also carries a value the server owns (resources, a cost, labor, a rate, a level, a timer,
    /// workers, a player, a clock) with 400, and nothing is stored: not the elapsed minute, not a ledger row, not a receipt.
    /// </summary>
    /// <param name="command">The endpoint.</param>
    /// <param name="body">Its valid body.</param>
    [Theory]
    [MemberData(nameof(EveryCommand))]
    public async Task Security_ValueTheServerOwns_OnEveryCommand_Is400AndStoresNothing(string command, string body)
    {
        await using WebApplicationFactory<Program> api = WithLoopContent(Factory);
        (GoldenHttpPlayer owner, Guid city, _) = await PlayOwnerAsync(api);
        using GoldenHttpPlayer player = owner;
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        string stored = await StoredAsync(api, Database, player.PlayerId, city);

        foreach (string field in _forbiddenFields)
        {
            string attack = Body(body, Guid.NewGuid()).TrimEnd('}') + "," + field + "}";
            (HttpStatusCode status, JsonNode? answer) = await player.SendRawAsync(command, attack);

            Assert.True(status == HttpStatusCode.BadRequest, $"{command} +{field}: {(int)status} {answer?.ToJsonString()}");
        }

        Assert.Equal(stored, await StoredAsync(api, Database, player.PlayerId, city));
    }

    /// <summary>
    /// Every command that names a building, sent by another player with the owner's building ids, is <c>BUILDING_NOT_FOUND</c> (404), never
    /// a sign that the building exists, and neither city changes: the attacker's starter city has no building of those ids.
    /// </summary>
    [Fact]
    public async Task Security_AnotherPlayersBuildings_OnEveryCommandThatNamesOne_Are404AndTouchNeitherCity()
    {
        await using WebApplicationFactory<Program> api = WithLoopContent(Factory);
        (GoldenHttpPlayer owner, Guid ownerCity, _) = await PlayOwnerAsync(api);
        using GoldenHttpPlayer ownerPlayer = owner;
        using GoldenHttpPlayer attacker = await GoldenHttpPlayer.LoginAsync(api, "attacker");
        Guid attackerCity = (await LoadAsync(api, attacker.PlayerId)).CityId;
        string ownerStored = await StoredAsync(api, Database, owner.PlayerId, ownerCity);
        string attackerStored = await StoredAsync(api, Database, attacker.PlayerId, attackerCity);
        var answers = new List<string>();

        foreach ((string command, string body) in _buildingCommands)
        {
            (HttpStatusCode status, JsonNode? answer) = await attacker.SendRawAsync(command, Body(body, Guid.NewGuid()));
            answers.Add($"{command} {(int)status} {(string?)answer?["code"]}");
        }

        Assert.Equal(_buildingCommands.Select(pair => $"{pair.Command} 404 BUILDING_NOT_FOUND"), answers);
        Assert.Equal(ownerStored, await StoredAsync(api, Database, owner.PlayerId, ownerCity));
        await AssertStoredWithRefusalsAsync(
            api, Database, attacker.PlayerId, attackerCity, attackerStored, [.. _buildingCommands.Select(_ => "BUILDING_NOT_FOUND")]);
    }

    /// <summary>
    /// Every accepted request of the played city, sent again later with the same id and body, answers the bytes it answered then and writes
    /// nothing: the replay neither applies twice nor settles the time that passed since.
    /// </summary>
    [Fact]
    public async Task Security_ReplayOfEveryAcceptedRequest_AnswersItsFirstAnswerAndWritesNothing()
    {
        await using WebApplicationFactory<Program> api = WithLoopContent(Factory);
        (GoldenHttpPlayer owner, Guid city, List<Sent> sent) = await PlayOwnerAsync(api);
        using GoldenHttpPlayer player = owner;
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        string stored = await StoredAsync(api, Database, player.PlayerId, city);

        foreach (Sent request in sent)
        {
            (HttpStatusCode status, JsonNode? answer) = await player.SendRawAsync(request.Command, request.Body);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(request.Answer, answer!.ToJsonString());
        }

        Assert.Equal(stored, await StoredAsync(api, Database, player.PlayerId, city));
    }

    /// <summary>
    /// The request id of every accepted request, sent with another command's body, is <c>IDEMPOTENCY_CONFLICT</c> (409) and writes nothing:
    /// a request id names one command and one payload for good.
    /// </summary>
    [Fact]
    public async Task Security_RequestIdOfEveryAcceptedRequest_WithAnotherBody_Is409AndWritesNothing()
    {
        await using WebApplicationFactory<Program> api = WithLoopContent(Factory);
        (GoldenHttpPlayer owner, Guid city, List<Sent> sent) = await PlayOwnerAsync(api);
        using GoldenHttpPlayer player = owner;
        string stored = await StoredAsync(api, Database, player.PlayerId, city);
        var answers = new List<string>();

        foreach (Sent request in sent)
        {
            (string command, string body) = request.Command == "reenergize-couriers"
                ? ("assign-workers", """{"requestId":"{0}","buildingId":3,"workers":0}""")
                : ("reenergize-couriers", """{"requestId":"{0}"}""");
            (HttpStatusCode status, JsonNode? answer) = await player.SendRawAsync(command, Body(body, request.RequestId));
            answers.Add($"{(int)status} {(string?)answer?["code"]}");
        }

        Assert.All(answers, answer => Assert.Equal("409 IDEMPOTENCY_CONFLICT", answer));
        Assert.Equal(stored, await StoredAsync(api, Database, player.PlayerId, city));
    }

    /// <summary>
    /// Commands sent faster than the burst limit (here 3 a minute) are 429 <c>RATE_LIMITED</c> once it is reached, on PostgreSQL with valid
    /// bodies: the three that fit are applied and keep a receipt each, the rest store nothing.
    /// </summary>
    [Fact]
    public async Task Security_SpamPastTheBurstLimit_Is429AndStoresOnlyWhatRan()
    {
        await using WebApplicationFactory<Program> api = WithLoopContent(Factory).WithWebHostBuilder(host => host
            .UseSetting("RateLimits:Commands:Burst:Limit", "3")
            .UseSetting("RateLimits:Commands:Burst:WindowSeconds", "60"));
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(api, "spammer");
        Guid city = (await LoadAsync(api, player.PlayerId)).CityId;
        var statuses = new List<int>();
        string afterThree = "";

        for (int request = 0; request < 6; request++)
        {
            (HttpStatusCode status, JsonNode? answer) = await player.SendRawAsync("reenergize-couriers", Body("""{"requestId":"{0}"}""", Guid.NewGuid()));
            statuses.Add((int)status);
            if (request == 2)
            {
                afterThree = await StoredAsync(api, Database, player.PlayerId, city);
            }
            else if (request > 2)
            {
                Assert.Equal("RATE_LIMITED", (string?)answer?["code"]);
            }
        }

        Assert.Equal([200, 200, 200, 429, 429, 429], statuses);
        Assert.Equal(3, (await ReceiptsAsync(Database, player.PlayerId)).Count);
        Assert.Equal(afterThree, await StoredAsync(api, Database, player.PlayerId, city));
    }

    /// <summary>The commands that name a building, with bodies naming the owner's buildings (request id as <c>{0}</c>).</summary>
    private static readonly (string Command, string Body)[] _buildingCommands =
    [
        ("upgrade-building", """{"requestId":"{0}","buildingId":1}"""),
        ("assign-workers", """{"requestId":"{0}","buildingId":3,"workers":1}"""),
        ("start-trip", """{"requestId":"{0}","sourceId":5,"targetId":7,"itemId":"planks","amount":"all"}"""),
        ("set-route", """{"requestId":"{0}","stationId":6,"slot":1,"stops":[{"buildingId":3,"actions":[{"kind":"take","itemId":"logs","amount":"all"}]}]}"""),
        ("set-paused", """{"requestId":"{0}","buildingId":7,"paused":true}"""),
        ("cancel-delivery", """{"requestId":"{0}","stationId":6,"slot":1,"lineId":null}"""),
    ];

    /// <summary>The server with the suite's frozen content.</summary>
    /// <param name="factory">The test's factory.</param>
    /// <returns>The server.</returns>
    private static WebApplicationFactory<Program> WithLoopContent(PostgresApiFactory factory) =>
        factory.WithWebHostBuilder(host => host.UseSetting("Content:FilePath", EraOneLoop.ContentPath));

    /// <summary>A body with its request id filled in.</summary>
    /// <param name="body">The body, request id as <c>{0}</c>.</param>
    /// <param name="requestId">The request id.</param>
    /// <returns>The JSON.</returns>
    private static string Body(string body, Guid requestId) => body.Replace("{0}", requestId.ToString(), StringComparison.Ordinal);

    /// <summary>
    /// Logs the owner in and plays the accepted acts of the script up to <see cref="_playedUntil"/> over HTTP (refusals and looks left out),
    /// each with a new request id; every one must answer 200.
    /// </summary>
    /// <param name="api">The server.</param>
    /// <returns>The owner (the caller disposes it), the city's id and the accepted requests with their answers.</returns>
    private async Task<(GoldenHttpPlayer Owner, Guid City, List<Sent> Sent)> PlayOwnerAsync(WebApplicationFactory<Program> api)
    {
        GoldenHttpPlayer owner = await GoldenHttpPlayer.LoginAsync(api, "owner");
        var sent = new List<Sent>();
        long clock = 0;
        foreach ((long time, LoopAct[] acts) in EraOneLoop.Steps.Where(step => step.Time <= _playedUntil))
        {
            Factory.Clock.Advance(TimeSpan.FromSeconds(time - clock));
            clock = time;
            foreach (LoopAct act in acts.Where(act => act is not (RefusedAct or LookAct)))
            {
                var requestId = Guid.NewGuid();
                (string command, object payload) = EraOneLoop.Request(act, requestId);
                string body = System.Text.Json.JsonSerializer.Serialize(payload, System.Text.Json.JsonSerializerOptions.Web);
                (HttpStatusCode status, JsonNode? answer) = await owner.SendRawAsync(command, body);
                Assert.True(status == HttpStatusCode.OK, $"t={time} {act}: {(int)status} {answer?.ToJsonString()}");
                sent.Add(new Sent(command, body, requestId, answer!.ToJsonString()));
            }
        }

        return (owner, (await LoadAsync(api, owner.PlayerId)).CityId, sent);
    }

    /// <summary>One accepted request as it was sent.</summary>
    /// <param name="Command">The endpoint.</param>
    /// <param name="Body">The JSON body.</param>
    /// <param name="RequestId">Its request id.</param>
    /// <param name="Answer">The answer's JSON.</param>
    private sealed record Sent(string Command, string Body, Guid RequestId, string Answer);
}
