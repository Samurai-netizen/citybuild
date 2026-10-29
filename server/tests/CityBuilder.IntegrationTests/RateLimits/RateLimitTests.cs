using System.Net;
using CityBuilder.Api.Http;
using CityBuilder.IntegrationTests.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Contracts.ProblemAssert;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.RateLimits;

/// <summary>
/// The rate limits (<c>core-instructions/05-api-contract.md</c>, rate limits; <c>07</c>, spam) over HTTP on the real endpoints over a
/// <see cref="FakeCityStore"/>: a burst and a sustained run of commands are refused with 429 <c>RATE_LIMITED</c> and <c>Retry-After</c>
/// before anything is locked, every command endpoint counts against the same budget, one player's spam leaves another's budget alone, the
/// dev login is counted per address, a player's normal pace is never limited with the default numbers, and every endpoint is in a class.
/// The limits are small numbers in 60-second windows, so a test can't straddle a window by accident. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server with the limits switched off; each test derives a copy with the numbers it needs.</param>
/// <param name="defaults">The in-memory server with the default limits.</param>
public sealed class RateLimitTests(ApiFactory factory, DefaultRateLimitsApiFactory defaults)
    : IClassFixture<ApiFactory>, IClassFixture<DefaultRateLimitsApiFactory>
{
    /// <summary>The first player.</summary>
    private static readonly Guid _playerA = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>The second player.</summary>
    private static readonly Guid _playerB = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");

    /// <summary>The server second the starter cities were last settled at.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The dev login's body that is refused before it touches the database, so a test can send many.</summary>
    private const string _badLogin = """{"displayName":""}""";

    /// <summary>The command endpoints, each with a body that reaches the binding and no further (broken JSON), so a test sends many cheaply.</summary>
    private static readonly string[] _commandPaths =
    [
        "/api/v1/commands/place-building", "/api/v1/commands/upgrade-building", "/api/v1/commands/assign-workers", "/api/v1/commands/start-research",
    ];

    /// <summary>The numbers the contract gives, which the options default to.</summary>
    [Fact]
    public void Defaults_AreTheContractsNumbers()
    {
        var limits = new RateLimitOptions();

        Assert.Equal((10, 1, 120, 60), (limits.Commands.Burst.Limit, limits.Commands.Burst.WindowSeconds, limits.Commands.Sustained.Limit, limits.Commands.Sustained.WindowSeconds));
        Assert.Equal((30, 60), (limits.City.Limit, limits.City.WindowSeconds));
        Assert.Equal((30, 60), (limits.Content.Limit, limits.Content.WindowSeconds));
        Assert.Equal((10, 60), (limits.DevLogin.Limit, limits.DevLogin.WindowSeconds));
        using var server = defaults.WithWebHostBuilder(_ => { });
        RateLimitOptions configured = server.Services.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        Assert.Equal(
            (10, 120, 30, 30, 10),
            (configured.Commands.Burst.Limit, configured.Commands.Sustained.Limit, configured.City.Limit, configured.Content.Limit, configured.DevLogin.Limit));
    }

    /// <summary>A limit or window out of range stops the start, naming the setting.</summary>
    /// <param name="key">The setting.</param>
    /// <param name="value">Its value.</param>
    /// <param name="name">The name the failure gives.</param>
    [Theory]
    [InlineData("RateLimits:City:Limit", "0", "City")]
    [InlineData("RateLimits:City:WindowSeconds", "0", "City")]
    [InlineData("RateLimits:Content:Limit", "0", "Content")]
    [InlineData("RateLimits:Commands:Burst:Limit", "-1", "Commands:Burst")]
    [InlineData("RateLimits:Commands:Sustained:WindowSeconds", "3601", "Commands:Sustained")]
    [InlineData("RateLimits:DevLogin:Limit", "1000001", "DevLogin")]
    public void Start_LimitOutOfRange_FailsValidation(string key, string value, string name)
    {
        using WebApplicationFactory<Program> broken = factory.WithWebHostBuilder(host => host.UseSetting(key, value));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures, failure => failure.StartsWith($"RateLimits:{name} must have a Limit between 1 and 1000000", StringComparison.Ordinal));
    }

    /// <summary>
    /// A run of commands past the burst limit is refused with 429 <c>RATE_LIMITED</c> and a <c>Retry-After</c> in whole seconds that is no
    /// longer than the window, before the body is read or the city is locked: the three that fit ran (the city was locked three times), the
    /// next two did not.
    /// </summary>
    [Fact]
    public async Task Post_CommandsPastTheBurstLimit_Are429BeforeTheCityIsLocked()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(("RateLimits:Commands:Burst:Limit", "3"), ("RateLimits:Commands:Burst:WindowSeconds", "60"));
        await using WebApplicationFactory<Program> served = server;

        var responses = new List<ApiResponse>();
        for (int command = 0; command < 5; command++)
        {
            responses.Add(await ApiClient.PostAsync(server, _commandPaths[3], _playerA, ResearchBody()));
        }

        Assert.Equal([422, 422, 422, 429, 429], responses.Select(response => (int)response.Status));
        Assert.Equal(3, store.LockCount);
        foreach (ApiResponse refused in responses.Skip(3))
        {
            AssertProblem(refused, HttpStatusCode.TooManyRequests, "RATE_LIMITED", requestId: null);
            AssertRetryAfter(refused, maxSeconds: 60);
        }
    }

    /// <summary>The sustained limit holds although the burst limit is not reached.</summary>
    [Fact]
    public async Task Post_CommandsPastTheSustainedLimit_Are429()
    {
        (_, WebApplicationFactory<Program> server) = Serve(("RateLimits:Commands:Sustained:Limit", "4"), ("RateLimits:Commands:Sustained:WindowSeconds", "60"));
        await using WebApplicationFactory<Program> served = server;

        int[] statuses = await StatusesAsync(6, _ => ApiClient.PostAsync(server, _commandPaths[3], _playerA, ResearchBody()));

        Assert.Equal([422, 422, 422, 422, 429, 429], statuses);
    }

    /// <summary>Every command endpoint counts against the same budget, and a request the binding refuses counts as well.</summary>
    [Fact]
    public async Task Post_AllCommandEndpoints_CountTogether()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(("RateLimits:Commands:Burst:Limit", "3"), ("RateLimits:Commands:Burst:WindowSeconds", "60"));
        await using WebApplicationFactory<Program> served = server;

        int[] statuses = await StatusesAsync(5, index => ApiClient.PostAsync(server, _commandPaths[index % _commandPaths.Length], _playerA, "{"));

        Assert.Equal([400, 400, 400, 429, 429], statuses);
        Assert.Equal(0, store.LockCount);
    }

    /// <summary><c>GET /city</c> has its own limit, in Development and Production alike.</summary>
    /// <param name="production">Whether the server runs in Production.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_CityPastItsLimit_Is429BeforeTheCityIsLocked(bool production)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve(production, ("RateLimits:City:Limit", "3"), ("RateLimits:City:WindowSeconds", "60"));
        await using WebApplicationFactory<Program> served = server;

        var all = new List<ApiResponse>();
        for (int poll = 0; poll < 5; poll++)
        {
            all.Add(await ApiClient.GetAsync(server, "/api/v1/city", _playerA));
        }

        Assert.Equal([200, 200, 200, 429, 429], all.Select(response => (int)response.Status));
        Assert.Equal(3, store.LockCount);
        AssertProblem(all[3], HttpStatusCode.TooManyRequests, "RATE_LIMITED", requestId: null);
        AssertRetryAfter(all[3], maxSeconds: 60);
    }

    /// <summary>
    /// One player's spam leaves everyone else's budget alone: after player A is refused, player B polls and commands as usual, and A's own
    /// commands still pass, because <c>GET /city</c> and the commands have separate budgets.
    /// </summary>
    [Fact]
    public async Task Security_OnePlayersSpam_DoesNotBlockAnotherOrTheirOtherBudget()
    {
        (_, WebApplicationFactory<Program> server) = Serve(("RateLimits:City:Limit", "2"), ("RateLimits:City:WindowSeconds", "60"));
        await using WebApplicationFactory<Program> served = server;

        int[] spammer = await StatusesAsync(4, _ => ApiClient.GetAsync(server, "/api/v1/city", _playerA));
        int[] other = await StatusesAsync(2, _ => ApiClient.GetAsync(server, "/api/v1/city", _playerB));
        ApiResponse command = await ApiClient.PostAsync(server, _commandPaths[3], _playerA, ResearchBody());
        int[] otherAfter = await StatusesAsync(1, _ => ApiClient.GetAsync(server, "/api/v1/city", _playerB));

        Assert.Equal([200, 200, 429, 429], spammer);
        Assert.Equal([200, 200], other);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, command.Status);
        Assert.Equal([429], otherAfter);
    }

    /// <summary>
    /// <c>GET /content</c> has a budget of its own (M0-34 decision 6): past it the request is 429, and neither the polls of the city nor
    /// the commands are touched by it, nor it by them.
    /// </summary>
    [Fact]
    public async Task Get_ContentPastItsLimit_Is429AndLeavesTheOtherBudgetsAlone()
    {
        (_, WebApplicationFactory<Program> server) = Serve(("RateLimits:Content:Limit", "2"), ("RateLimits:Content:WindowSeconds", "60"));
        await using WebApplicationFactory<Program> served = server;

        int[] statuses = await StatusesAsync(4, _ => ApiClient.GetAsync(server, "/api/v1/content", _playerA));
        int[] other = await StatusesAsync(1, _ => ApiClient.GetAsync(server, "/api/v1/content", _playerB));
        int[] city = await StatusesAsync(1, _ => ApiClient.GetAsync(server, "/api/v1/city", _playerA));

        Assert.Equal([200, 200, 429, 429], statuses);
        Assert.Equal([200], other);
        Assert.Equal([200], city);
    }

    /// <summary>
    /// A request without a working token is 401 however often it comes: the limiter runs after the token check and counts nothing for a
    /// caller it can't name.
    /// </summary>
    [Fact]
    public async Task Security_RequestsWithoutToken_Are401NotCounted()
    {
        (_, WebApplicationFactory<Program> server) = Serve(("RateLimits:Commands:Burst:Limit", "1"), ("RateLimits:City:Limit", "1"));
        await using WebApplicationFactory<Program> served = server;

        int[] commands = await StatusesAsync(4, _ => ApiClient.PostAsync(server, _commandPaths[0], playerId: null, "{"));
        int[] polls = await StatusesAsync(4, _ => ApiClient.GetAsync(server, "/api/v1/city", playerId: null));

        Assert.Equal([401, 401, 401, 401], commands);
        Assert.Equal([401, 401, 401, 401], polls);
    }

    /// <summary>
    /// The dev login is counted per remote address: a refused body counts, another address has its own budget, and an IPv4 address mapped
    /// into IPv6 is the same address as the IPv4 one.
    /// </summary>
    [Fact]
    public async Task Post_DevLoginPastTheLimit_Is429PerAddress()
    {
        (_, WebApplicationFactory<Program> inner) = Serve(("RateLimits:DevLogin:Limit", "2"), ("RateLimits:DevLogin:WindowSeconds", "60"));
        await using WebApplicationFactory<Program> served = inner;
        await using WebApplicationFactory<Program> server = RemoteAddressFilter.AddTo(inner);

        int[] first = await StatusesAsync(3, _ => LoginFromAsync(server, "10.0.0.1"));
        int[] second = await StatusesAsync(2, _ => LoginFromAsync(server, "10.0.0.2"));
        ApiResponse mapped = await LoginFromAsync(server, "::ffff:10.0.0.1");

        Assert.Equal([400, 400, 429], first);
        Assert.Equal([400, 400], second);
        AssertProblem(mapped, HttpStatusCode.TooManyRequests, "RATE_LIMITED", requestId: null);
    }

    /// <summary>
    /// With the default limits a player's normal pace is never limited: a poll of the city every few seconds for a minute and a run of ten taps
    /// pass, and so does <c>GET /content</c> 30 times. The 31st poll in a minute is refused, the 11th dev login of an
    /// address too: the contract's numbers.
    /// </summary>
    [Fact]
    public async Task Defaults_NormalPlay_IsNeverLimited_AndTheContractsNumbersHold()
    {
        var store = new FakeCityStore();
        store.Add(Starter(ContentOf(defaults), Guid.NewGuid(), _playerA, _settledAt.ToUnixTimeSeconds()));
        await using WebApplicationFactory<Program> server = RemoteAddressFilter.AddTo(store.Serve(defaults, new FakeTimeProvider(_settledAt)));

        int[] taps = await StatusesAsync(10, index => ApiClient.PostAsync(server, _commandPaths[index % _commandPaths.Length], _playerA, "{"));
        int[] content = await StatusesAsync(30, _ => ApiClient.GetAsync(server, "/api/v1/content", _playerA));
        int[] polls = await StatusesAsync(31, _ => ApiClient.GetAsync(server, "/api/v1/city", _playerA));
        int[] logins = await StatusesAsync(11, _ => LoginFromAsync(server, "10.0.0.9"));

        Assert.All(taps, status => Assert.Equal(400, status));
        Assert.All(content, status => Assert.Equal(200, status));
        Assert.All(polls.Take(30), status => Assert.Equal(200, status));
        Assert.Equal(429, polls[30]);
        Assert.All(logins.Take(10), status => Assert.Equal(400, status));
        Assert.Equal(429, logins[10]);
    }

    /// <summary>
    /// Every endpoint of the API is in a limit class, and the class is the one the contract names, so a new endpoint can't be added without
    /// a limit (or a decision to leave it out, which edits this table).
    /// </summary>
    [Fact]
    public void Security_EveryApiEndpoint_IsInItsLimitClass()
    {
        var expected = new Dictionary<string, RateLimitClass?>(StringComparer.Ordinal)
        {
            ["/api/v1/auth/dev-login"] = RateLimitClass.DevLogin,
            ["/api/v1/city"] = RateLimitClass.City,
            ["/api/v1/content"] = RateLimitClass.Content,
            ["/api/v1/commands/place-building"] = RateLimitClass.Commands,
            ["/api/v1/commands/upgrade-building"] = RateLimitClass.Commands,
            ["/api/v1/commands/assign-workers"] = RateLimitClass.Commands,
            ["/api/v1/commands/start-research"] = RateLimitClass.Commands,
        };
        using WebApplicationFactory<Program> server = factory.WithWebHostBuilder(_ => { });

        Dictionary<string, RateLimitClass?> actual = server.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/v1/", StringComparison.Ordinal))
            .ToDictionary(endpoint => endpoint.RoutePattern.RawText!, endpoint => endpoint.Metadata.GetMetadata<RateLimitMetadata>()?.Class);

        Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal), actual.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    /// <summary>The server with a store of two starter cities and the given settings, in Development.</summary>
    /// <param name="settings">Configuration keys and values.</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(params (string Key, string Value)[] settings) =>
        Serve(production: false, settings);

    /// <summary>The server with a store of two starter cities and the given settings.</summary>
    /// <param name="production">Whether the server runs in Production instead of Development.</param>
    /// <param name="settings">Configuration keys and values.</param>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve(bool production, params (string Key, string Value)[] settings)
    {
        var store = new FakeCityStore();
        foreach (Guid player in new[] { _playerA, _playerB })
        {
            store.Add(Starter(ContentOf(factory), Guid.NewGuid(), player, _settledAt.ToUnixTimeSeconds()));
        }

        WebApplicationFactory<Program> limited = factory.WithWebHostBuilder(host =>
        {
            if (production)
            {
                host.UseEnvironment("Production");
            }

            foreach ((string key, string value) in settings)
            {
                host.UseSetting(key, value);
            }
        });
        return (store, store.Serve(limited, new FakeTimeProvider(_settledAt)));
    }

    /// <summary>Sends requests one after the other and collects the statuses.</summary>
    /// <param name="count">How many requests.</param>
    /// <param name="send">Sends the request of that index.</param>
    /// <returns>The statuses, in order.</returns>
    private static async Task<int[]> StatusesAsync(int count, Func<int, Task<ApiResponse>> send)
    {
        var statuses = new List<int>();
        for (int index = 0; index < count; index++)
        {
            statuses.Add((int)(await send(index)).Status);
        }

        return [.. statuses];
    }

    /// <summary>A research command for a node the content doesn't have: it reaches the city and is refused by the rule (422).</summary>
    /// <returns>The body.</returns>
    private static string ResearchBody() => $$"""{"requestId":"{{Guid.NewGuid()}}","researchId":"no_such_node"}""";

    /// <summary>A dev login from a remote address, with a body that is refused before it touches the database.</summary>
    /// <param name="server">The server, with the address step.</param>
    /// <param name="address">The remote address.</param>
    /// <returns>The response.</returns>
    private static Task<ApiResponse> LoginFromAsync(WebApplicationFactory<Program> server, string address) =>
        ApiClient.PostAsync(
            server,
            "/api/v1/auth/dev-login",
            playerId: null,
            new StringContent(_badLogin, System.Text.Encoding.UTF8, "application/json"),
            new Dictionary<string, string> { [RemoteAddressFilter.Header] = address });

    /// <summary>Asserts that the answer has a <c>Retry-After</c> in whole seconds, at least 1 and at most the window.</summary>
    /// <param name="response">The 429 answer.</param>
    /// <param name="maxSeconds">The longest window.</param>
    private static void AssertRetryAfter(ApiResponse response, int maxSeconds)
    {
        string value = Assert.Single(response.Headers.GetValues("Retry-After"));
        Assert.True(int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int seconds), $"Retry-After '{value}' is not whole seconds.");
        Assert.InRange(seconds, 1, maxSeconds);
    }
}
