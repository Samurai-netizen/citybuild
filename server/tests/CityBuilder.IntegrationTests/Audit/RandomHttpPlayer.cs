using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CityBuilder.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CityBuilder.IntegrationTests.Audit;

/// <summary>
/// A player who plays at random over HTTP for the ledger audit (M0-64): each <see cref="StepAsync"/> sends one of the API's twelve commands,
/// or a settle (<c>GET /api/v1/city</c>), with arguments drawn from a seeded <see cref="Random"/> and the last snapshot (its buildings,
/// roads and couriers), so most commands name things that exist and many succeed. A refusal of the game's rules (a 4xx with an error
/// code) is part of the play; a server error, a missing code, or a refused login or rate limit fails the test with the seed and step.
/// </summary>
internal sealed class RandomHttpPlayer : IDisposable
{
    /// <summary>The commands a step picks from, by their path segment under <c>/api/v1/commands/</c>; a step may also settle instead.</summary>
    public static readonly string[] Commands =
    [
        "place-building", "upgrade-building", "assign-workers", "start-research", "place-roads", "upgrade-roads", "remove-roads",
        "start-trip", "set-route", "reenergize-couriers", "cancel-delivery", "set-paused",
    ];

    /// <summary>
    /// How often a random step picks each command: the four M0 commands and trips most, roads rarely (each tile costs coins, and a city
    /// that spends its purse on roads stops playing).
    /// </summary>
    private static readonly (string Command, int Weight)[] _weights =
    [
        ("place-building", 3), ("upgrade-building", 3), ("assign-workers", 3), ("start-research", 2), ("place-roads", 1),
        ("upgrade-roads", 1), ("remove-roads", 1), ("start-trip", 4), ("set-route", 1), ("reenergize-couriers", 1),
        ("cancel-delivery", 1), ("set-paused", 1),
    ];

    /// <summary>How many times an opening step is sent before the opening moves on without it (the clock moves between tries).</summary>
    private const int _maxOpeningTries = 20;

    /// <summary>The 4xx codes that mean the test or the server is broken, not a move the rules refuse (one player sends one request at a time).</summary>
    private static readonly string[] _brokenCodes = ["AUTHENTICATION_REQUIRED", "RATE_LIMITED", "CONCURRENCY_CONFLICT"];

    /// <summary>The client of the server under test, carrying the player's token.</summary>
    private readonly HttpClient _client;
    /// <summary>The player's dev name, to log in again when the token has expired.</summary>
    private readonly string _displayName;
    /// <summary>The player's own seeded source of every choice.</summary>
    private readonly Random _random;
    /// <summary>The content's building types, research ids and goods (not the currency), from <c>GET /api/v1/content</c>.</summary>
    private readonly (string[] Buildings, string[] Research, string[] Goods) _content;
    /// <summary>The seed, for failure messages.</summary>
    private readonly int _seed;
    /// <summary>The city as the last 200 answer showed it.</summary>
    private JsonNode _city;
    /// <summary>How many steps ran, for failure messages.</summary>
    private int _step;
    /// <summary>How many times the opening's current step was sent.</summary>
    private int _openingTries;
    /// <summary>
    /// The opening the first steps play before the random ones, so the city has an economy to play with (<see cref="Opening"/>). A command
    /// the city can't do yet (still building, still locked, not enough coins) is sent again at the next step, up to
    /// <see cref="_maxOpeningTries"/> times.
    /// </summary>
    private readonly Queue<(string Command, Func<Guid, object> Body)> _opening = new(Opening());

    /// <summary>Wraps a logged-in client.</summary>
    /// <param name="client">The client, carrying the token.</param>
    /// <param name="displayName">The player's dev name.</param>
    /// <param name="seed">The seed of the player's choices.</param>
    /// <param name="content">The content's ids.</param>
    /// <param name="city">The first snapshot.</param>
    private RandomHttpPlayer(HttpClient client, string displayName, int seed, (string[], string[], string[]) content, JsonNode city)
    {
        _client = client;
        _displayName = displayName;
        _seed = seed;
        _random = new Random(seed);
        _content = content;
        _city = city;
    }

    /// <summary>How many times each command succeeded (200), by path segment.</summary>
    public Dictionary<string, int> Succeeded { get; } = Commands.ToDictionary(command => command, _ => 0);

    /// <summary>How many times each command was refused, by path segment and error code, for the message when a command never succeeds.</summary>
    public Dictionary<(string Command, string Code), int> Refused { get; } = [];

    /// <summary>Logs a new dev name in, reads the content and the starter city.</summary>
    /// <param name="server">The server under test.</param>
    /// <param name="displayName">The dev name; a new one makes a new city.</param>
    /// <param name="seed">The seed of the player's choices.</param>
    /// <returns>The player.</returns>
    public static async Task<RandomHttpPlayer> StartAsync(WebApplicationFactory<Program> server, string displayName, int seed)
    {
        HttpClient client = server.CreateClient();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await LogInAsync(client, displayName);
        JsonNode content = JsonNode.Parse(await client.GetStringAsync(new Uri("/api/v1/content", UriKind.Relative), cancellationToken))!;
        string[] buildings = [.. content["buildings"]!.AsArray().Select(building => building!["id"]!.GetValue<string>())];
        string[] research = [.. content["research"]!.AsArray().Select(node => node!["id"]!.GetValue<string>())];
        string[] goods = [.. content["items"]!.AsArray().Where(item => !item!["currency"]!.GetValue<bool>()).Select(item => item!["id"]!.GetValue<string>())];
        JsonNode city = JsonNode.Parse(await client.GetStringAsync(new Uri("/api/v1/city", UriKind.Relative), cancellationToken))!;
        return new RandomHttpPlayer(client, displayName, seed, (buildings, research, goods), city);
    }

    /// <summary>
    /// One step: the opening's current command while there is one (it moves on when the command succeeds or ran out of tries); then a settle about one time in five, otherwise one command picked by
    /// its weight.
    /// </summary>
    /// <returns>A task that completes when the answer is checked.</returns>
    public async Task StepAsync()
    {
        _step++;
        if (_opening.TryPeek(out (string Command, Func<Guid, object> Body) opening))
        {
            _openingTries++;
            if (await CommandAsync(opening.Command, opening.Body(Guid.NewGuid())) || _openingTries == _maxOpeningTries)
            {
                _opening.Dequeue();
                _openingTries = 0;
            }

            return;
        }

        if (_random.Next(5) == 0)
        {
            using HttpResponseMessage settle = await SendAsync(() => _client.GetAsync(new Uri("/api/v1/city", UriKind.Relative), TestContext.Current.CancellationToken));
            await CheckAsync("settle", settle);
            return;
        }

        string command = WeightedCommand();
        await CommandAsync(command, BodyOf(command));
    }

    /// <summary>Disposes the client.</summary>
    public void Dispose() => _client.Dispose();

    /// <summary>Sends a command and counts it when it succeeds.</summary>
    /// <param name="command">The command's path segment.</param>
    /// <param name="body">The body.</param>
    /// <returns>Whether it succeeded.</returns>
    private async Task<bool> CommandAsync(string command, object body)
    {
        using HttpResponseMessage response = await SendAsync(() => _client.PostAsJsonAsync(
            new Uri("/api/v1/commands/" + command, UriKind.Relative), body, TestContext.Current.CancellationToken));
        if (!await CheckAsync(command, response))
        {
            return false;
        }

        Succeeded[command]++;
        return true;
    }

    /// <summary>A command drawn by the weights of <see cref="_weights"/>.</summary>
    /// <returns>Its path segment.</returns>
    private string WeightedCommand()
    {
        int draw = _random.Next(_weights.Sum(weight => weight.Weight));
        foreach ((string command, int weight) in _weights)
        {
            if (draw < weight)
            {
                return command;
            }

            draw -= weight;
        }

        throw new InvalidOperationException("The draw is below the sum of the weights.");
    }

    /// <summary>Logs the dev name in (the same player each time) and puts the new token on the client.</summary>
    /// <param name="client">The client.</param>
    /// <param name="displayName">The dev name.</param>
    /// <returns>A task that completes when the client carries the token.</returns>
    private static async Task LogInAsync(HttpClient client, string displayName)
    {
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/dev-login", UriKind.Relative), new { displayName }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        DevLoginResponse token = (await login.Content.ReadFromJsonAsync<DevLoginResponse>(TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    /// <summary>
    /// Sends a request; when the token has expired (401, the clock jumps hours), logs in again and sends it once more, as the client does. A
    /// refused token never reached the command, so the same request id is safe to send again.
    /// </summary>
    /// <param name="send">Sends the request.</param>
    /// <returns>The answer; the caller disposes it.</returns>
    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        HttpResponseMessage response = await send();
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        await LogInAsync(_client, _displayName);
        return await send();
    }

    /// <summary>A request body for the command, with a new request id and arguments drawn from the last snapshot and the content.</summary>
    /// <param name="command">The command's path segment.</param>
    /// <returns>The body, serialized in camelCase.</returns>
    private object BodyOf(string command)
    {
        Guid requestId = Guid.NewGuid();
        return command switch
        {
            "place-building" => new { requestId, buildingTypeId = Pick(_content.Buildings), x = Coordinate(), y = Coordinate() },
            "upgrade-building" => new { requestId, buildingId = BuildingId() },
            "assign-workers" => AssignWorkers(requestId),
            "start-research" => new { requestId, researchId = Pick(_content.Research) },
            "place-roads" => new { requestId, tiles = Enumerable.Range(0, 1 + _random.Next(3)).Select(_ => new { x = Coordinate(), y = Coordinate() }).ToArray() },
            "upgrade-roads" or "remove-roads" => new { requestId, tiles = new[] { RoadTile() } },
            "start-trip" => StartTrip(requestId),
            "set-route" => SetRoute(requestId),
            "reenergize-couriers" => new { requestId },
            "cancel-delivery" => new { requestId, stationId = StationId(), slot = Slot(), lineId = (int?)null },
            "set-paused" => new { requestId, buildingId = BuildingId(), paused = _random.Next(2) == 0 },
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a command of the list."),
        };
    }

    /// <summary>
    /// An assign-workers body: a building of the city and a count: 0 one time in four, otherwise 1, or up to its maximum one time in three,
    /// so producers keep working and free workers are left for trips.
    /// </summary>
    /// <param name="requestId">The request id.</param>
    /// <returns>The body.</returns>
    private object AssignWorkers(Guid requestId)
    {
        JsonNode? building = Building();
        int max = building?["maxWorkers"]!.GetValue<int>() ?? 1;
        int workers = _random.Next(4) == 0 ? 0 : _random.Next(3) == 0 ? 1 + _random.Next(Math.Max(max, 1)) : 1;
        return new { requestId, buildingId = building?["id"]!.GetValue<int>() ?? 1, workers };
    }

    /// <summary>
    /// A start-trip body: from a building with goods in its store (any building when none has), one of those goods, to another building
    /// whose store takes that good (its <c>storeLimits</c> or <c>needs</c> name it), or to any other building when none does.
    /// </summary>
    /// <param name="requestId">The request id.</param>
    /// <returns>The body.</returns>
    private object StartTrip(Guid requestId)
    {
        JsonNode[] stocked = [.. Buildings().Where(building => Goods(building, "store").Length > 0)];
        JsonNode? source = stocked.Length == 0 ? Building() : stocked[_random.Next(stocked.Length)];
        int sourceId = source?["id"]!.GetValue<int>() ?? 1;
        string[] goods = source is null ? [] : Goods(source, "store");
        string item = goods.Length == 0 ? Pick(_content.Goods) : Pick(goods);
        JsonNode[] others = [.. Buildings().Where(building => building["id"]!.GetValue<int>() != sourceId)];
        JsonNode[] takers = [.. others.Where(building => Goods(building, "storeLimits").Contains(item) || Goods(building, "needs").Contains(item))];
        JsonNode[] targets = takers.Length > 0 ? takers : others;
        int targetId = targets.Length == 0 ? sourceId + 1 : targets[_random.Next(targets.Length)]["id"]!.GetValue<int>();
        return new { requestId, sourceId, targetId, itemId = item, amount = Amount() };
    }

    /// <summary>The goods a building's snapshot map names with a quantity above 0.</summary>
    /// <param name="building">The building's JSON.</param>
    /// <param name="map">The map: <c>store</c>, <c>storeLimits</c> or <c>needs</c>.</param>
    /// <returns>The item ids.</returns>
    private static string[] Goods(JsonNode building, string map) =>
        [.. building[map]!.AsObject().Where(good => good.Value!.GetValue<long>() > 0).Select(good => good.Key)];

    /// <summary>
    /// A set-route body: a station's slot (<see cref="Slot"/>), taking a good at one building and putting it at another; one time in three no stops,
    /// which clears the route and frees the courier for trips.
    /// </summary>
    /// <param name="requestId">The request id.</param>
    /// <returns>The body.</returns>
    private object SetRoute(Guid requestId)
    {
        string item = Pick(_content.Goods);
        var stops = new[]
        {
            new { buildingId = BuildingId(), actions = new[] { new { kind = "take", itemId = item, amount = Amount() } } },
            new { buildingId = BuildingId(), actions = new[] { new { kind = "put", itemId = item, amount = Amount() } } },
        };
        return new { requestId, stationId = StationId(), slot = Slot(), stops = _random.Next(3) == 0 ? [] : stops };
    }

    /// <summary>
    /// The opening's steps: a house (id 1, 4 workers), a woodcutter (2), a scriptorium (3), the research that unlocks the trading post, a
    /// sawmill (4) and a trading post (5) with one worker each, rounds of trips carrying logs to the sawmill and planks to the trading post
    /// (first its site's planks, then planks it turns into coins), a courier station (6) whose route runs that chain by itself, and the
    /// house's upgrade. The ids hold when every placement succeeds.
    /// </summary>
    /// <returns>The steps in order.</returns>
    private static IEnumerable<(string Command, Func<Guid, object> Body)> Opening()
    {
        yield return Place("house", 0, 2);
        yield return Place("woodcutter", 0, 0);
        yield return Place("scriptorium", 4, 0);
        yield return Assign(2);
        yield return ("start-research", requestId => new { requestId, researchId = "trade_charter" });
        yield return Place("sawmill", 2, 0);
        yield return Trip(2, 4, "logs");
        yield return Trip(2, 4, "logs");
        yield return Assign(4);
        yield return Place("trading_post", 2, 2);
        for (int round = 0; round < 8; round++)
        {
            yield return Trip(2, 4, "logs");
            yield return Trip(4, 5, "planks");
        }

        yield return Assign(5);
        for (int round = 0; round < 6; round++)
        {
            yield return Trip(2, 4, "logs");
            yield return Trip(4, 5, "planks");
        }

        yield return Place("courier_station", 3, 2);
        yield return Assign(6);
        yield return ("set-route", requestId => new
        {
            requestId,
            stationId = 6,
            slot = 1,
            stops = new object[]
            {
                new { buildingId = 2, actions = new[] { new { kind = "take", itemId = "logs", amount = "all" } } },
                new
                {
                    buildingId = 4,
                    actions = new[] { new { kind = "put", itemId = "logs", amount = "all" }, new { kind = "take", itemId = "planks", amount = "all" } },
                },
                new { buildingId = 5, actions = new[] { new { kind = "put", itemId = "planks", amount = "all" } } },
            },
        });
        yield return ("upgrade-building", requestId => new { requestId, buildingId = 1 });
    }

    /// <summary>An opening step that places a building.</summary>
    /// <param name="type">The building type.</param>
    /// <param name="x">The tile's column.</param>
    /// <param name="y">The tile's row.</param>
    /// <returns>The command and its body.</returns>
    private static (string, Func<Guid, object>) Place(string type, int x, int y) =>
        ("place-building", requestId => new { requestId, buildingTypeId = type, x, y });

    /// <summary>An opening step that gives a building one worker.</summary>
    /// <param name="buildingId">The building.</param>
    /// <returns>The command and its body.</returns>
    private static (string, Func<Guid, object>) Assign(int buildingId) =>
        ("assign-workers", requestId => new { requestId, buildingId, workers = 1 });

    /// <summary>An opening step that sends a worker with all of a good from one building to another.</summary>
    /// <param name="sourceId">Where the good is.</param>
    /// <param name="targetId">Where it goes.</param>
    /// <param name="itemId">The good.</param>
    /// <returns>The command and its body.</returns>
    private static (string, Func<Guid, object>) Trip(int sourceId, int targetId, string itemId) =>
        ("start-trip", requestId => new { requestId, sourceId, targetId, itemId, amount = "all" });

    /// <summary>A courier slot: mostly 1, which every station has; sometimes 2, which only an upgraded one has.</summary>
    /// <returns>1 or 2.</returns>
    private int Slot() => _random.Next(4) == 0 ? 2 : 1;

    /// <summary>A tile coordinate on the 8 × 8 grid of the content.</summary>
    /// <returns>0 to 7.</returns>
    private int Coordinate() => _random.Next(8);

    /// <summary>
    /// An amount: mostly a preset (always allowed), otherwise a percentage or a number, which only research unlocks (refused until then;
    /// <c>04</c> section 19).
    /// </summary>
    /// <returns>The text.</returns>
    private string Amount() => Pick(["all", "half", "quarter", "all", "half", "quarter", "50%", "5"]);

    /// <summary>The buildings of the last snapshot.</summary>
    /// <returns>Their JSON.</returns>
    private JsonNode[] Buildings() => [.. _city["city"]!["buildings"]!.AsArray().Select(building => building!)];

    /// <summary>A random building of the last snapshot, or <see langword="null"/> when the city has none.</summary>
    /// <returns>The building's JSON.</returns>
    private JsonNode? Building()
    {
        JsonNode[] buildings = Buildings();
        return buildings.Length == 0 ? null : buildings[_random.Next(buildings.Length)];
    }

    /// <summary>The id of a random building of the last snapshot; 1 when there is none (refused: not found).</summary>
    /// <returns>The id.</returns>
    private int BuildingId() => Building()?["id"]!.GetValue<int>() ?? 1;

    /// <summary>The id of a random courier station of the last snapshot, or of any building when there is none (refused).</summary>
    /// <returns>The id.</returns>
    private int StationId()
    {
        JsonNode[] stations = [.. Buildings().Where(building => building["typeId"]!.GetValue<string>() == "courier_station")];
        return stations.Length == 0 ? BuildingId() : stations[_random.Next(stations.Length)]["id"]!.GetValue<int>();
    }

    /// <summary>A random road tile of the last snapshot, or any tile when there is none (refused: no road).</summary>
    /// <returns>The tile.</returns>
    private object RoadTile()
    {
        JsonArray roads = _city["city"]!["roads"]!.AsArray();
        if (roads.Count == 0)
        {
            return new { x = Coordinate(), y = Coordinate() };
        }

        JsonNode road = roads[_random.Next(roads.Count)]!;
        return new { x = road["x"]!.GetValue<int>(), y = road["y"]!.GetValue<int>() };
    }

    /// <summary>A random element.</summary>
    /// <param name="values">The values; not empty.</param>
    /// <returns>One of them.</returns>
    private string Pick(string[] values) => values[_random.Next(values.Length)];

    /// <summary>
    /// Checks an answer: a 200 keeps its snapshot; a 4xx must carry an error code of the rules. Anything else fails the test with the seed,
    /// the step and the body.
    /// </summary>
    /// <param name="what">The command or <c>settle</c>, for the message.</param>
    /// <param name="response">The answer.</param>
    /// <returns>Whether it was a 200.</returns>
    private async Task<bool> CheckAsync(string what, HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string where = $"seed {_seed}, step {_step}, {what}: {(int)response.StatusCode} {body}";
        if (response.StatusCode == HttpStatusCode.OK)
        {
            _city = JsonNode.Parse(body) ?? throw new InvalidOperationException(where);
            return true;
        }

        int status = (int)response.StatusCode;
        string? code = status is >= 400 and < 500 ? JsonNode.Parse(body)?["code"]?.GetValue<string>() : null;
        Assert.True(code is not null && !_brokenCodes.Contains(code), where);
        Refused[(what, code)] = Refused.GetValueOrDefault((what, code)) + 1;
        return false;
    }
}
