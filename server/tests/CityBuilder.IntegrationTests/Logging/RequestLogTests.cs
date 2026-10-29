using System.Net;
using System.Net.Http.Headers;
using CityBuilder.Api.Auth;
using CityBuilder.IntegrationTests.Auth;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Errors;
using CityBuilder.IntegrationTests.RateLimits;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Logging;

/// <summary>
/// The log lines of authentication, rate limits and errors (core-instructions/07-security-model.md, logging policy; docs/api/README.md):
/// each event with its id, level and named values, the request's trace id on every line (the same as the answer's <c>traceId</c>), and never
/// a token, a body, a display name or a remote address in any line. Over HTTP on the real pipeline with a <see cref="FakeCityStore"/> and a
/// <see cref="CapturingLogger"/>. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server (Development, limits relaxed); each test derives a copy with its store, clock and logger.</param>
public sealed class RequestLogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The player of the tests.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>The instant the fake clock starts at, which is also when the starter city was last settled.</summary>
    private static readonly DateTimeOffset _start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The server of a test: a store with the player's starter city, a fake clock, the logger, and extra settings.</summary>
    /// <param name="logs">Collects what the server logs.</param>
    /// <param name="settings">Settings to override, such as a small rate limit.</param>
    /// <returns>The server; the caller disposes it.</returns>
    private WebApplicationFactory<Program> Serve(CapturingLogger logs, params (string Key, string Value)[] settings)
    {
        WebApplicationFactory<Program> configured = factory.WithWebHostBuilder(host =>
        {
            foreach ((string key, string value) in settings)
            {
                host.UseSetting(key, value);
            }
        });
        var store = new FakeCityStore();
        store.Add(Starter(ContentOf(factory), Guid.NewGuid(), _playerId, _start.ToUnixTimeSeconds()));
        return logs.AddTo(store.Serve(configured, new FakeTimeProvider(_start)));
    }

    /// <summary>A request without a token is refused with a warning that names the reason <c>NoToken</c>, the method and the path.</summary>
    [Fact]
    public async Task Get_WithoutToken_LogsTheRefusalAsNoToken()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/city", playerId: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        CapturingLogger.Entry refused = Assert.Single(logs.Events(4003));
        Assert.Equal(LogLevel.Warning, refused.Level);
        Assert.Equal(("GET", "/api/v1/city", "NoToken"), (refused.Values["Method"], refused.Values["Path"], refused.Values["Reason"]));
    }

    /// <summary>
    /// A token that doesn't work is refused with the kind of its failure (the validation exception's type), and neither the token nor any
    /// part of it is in any log line.
    /// </summary>
    [Fact]
    public async Task Security_RefusedToken_LogsTheKindOfFailureNeverTheToken()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);
        string expired = TestTokens.Build(_playerId, _start.AddHours(-2), TestTokens.SigningKey);
        string forged = TestTokens.Build(_playerId, _start, TestTokens.OtherSigningKey);

        ApiResponse first = await ApiClient.GetAsync(server, "/api/v1/city", new AuthenticationHeaderValue("Bearer", expired));
        ApiResponse second = await ApiClient.GetAsync(server, "/api/v1/city", new AuthenticationHeaderValue("Bearer", forged));

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (first.Status, second.Status));
        IReadOnlyList<CapturingLogger.Entry> refused = logs.Events(4003);
        Assert.Equal(2, refused.Count);
        Assert.Equal("SecurityTokenInvalidLifetimeException", refused[0].Values["Reason"]);
        Assert.StartsWith("SecurityToken", (string)refused[1].Values["Reason"]!, StringComparison.Ordinal);
        AssertNowhere(logs, expired.Split('.'));
        AssertNowhere(logs, forged.Split('.'));
    }

    /// <summary>An accepted token is logged at Debug with its player (the default level, Information, leaves it out); the token is not.</summary>
    [Fact]
    public async Task Get_WithToken_LogsTheAcceptedPlayerAtDebug()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs, ("Logging:LogLevel:CityBuilder", "Debug"));
        string token = server.Services.GetRequiredService<AccessTokens>().Issue(_playerId).Token;

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/content", new AuthenticationHeaderValue("Bearer", token));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        CapturingLogger.Entry accepted = Assert.Single(logs.Events(4002));
        Assert.Equal((LogLevel.Debug, (object?)_playerId), (accepted.Level, accepted.Values["PlayerId"]));
        AssertNowhere(logs, token.Split('.'));
    }

    /// <summary>With the default levels an accepted token writes no line: every game request has one.</summary>
    [Fact]
    public async Task Get_WithTokenAtDefaultLevels_LogsNoAcceptance()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);

        await ApiClient.GetAsync(server, "/api/v1/content", _playerId);

        Assert.Empty(logs.Events(4002));
    }

    /// <summary>A dev login refused for its name logs the code, never the name that was sent.</summary>
    [Fact]
    public async Task Security_DevLoginRefused_LogsTheCodeNotTheName()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs);

        ApiResponse response = await ApiClient.PostAsync(server, "/api/v1/auth/dev-login", playerId: null, """{"displayName":"Secret Name!"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        CapturingLogger.Entry refused = Assert.Single(logs.Events(4001));
        Assert.Equal("INVALID_ARGUMENT", refused.Values["Code"]);
        AssertNowhere(logs, "Secret Name");
    }

    /// <summary>A request over a limit is logged as a warning with the limit class, the player and the <c>Retry-After</c>.</summary>
    [Fact]
    public async Task Get_CityPastItsLimit_LogsTheHitWithClassAndPlayer()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs, ("RateLimits:City:Limit", "1"), ("RateLimits:City:WindowSeconds", "60"));

        await ApiClient.GetAsync(server, "/api/v1/city", _playerId);
        ApiResponse refused = await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.Status);
        CapturingLogger.Entry hit = Assert.Single(logs.Events(3001));
        Assert.Equal(LogLevel.Warning, hit.Level);
        Assert.Equal("City", hit.Values["LimitClass"]);
        Assert.Equal(_playerId, hit.Values["PlayerId"]);
        Assert.Equal(int.Parse(Assert.Single(refused.Headers.GetValues("Retry-After")), System.Globalization.CultureInfo.InvariantCulture), hit.Values["RetryAfterSeconds"]);
    }

    /// <summary>A dev login over its limit is logged without a player and without the remote address it was counted by.</summary>
    [Fact]
    public async Task Security_DevLoginPastItsLimit_LogsNoAddress()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> inner = Serve(logs, ("RateLimits:DevLogin:Limit", "1"), ("RateLimits:DevLogin:WindowSeconds", "60"));
        await using WebApplicationFactory<Program> server = RemoteAddressFilter.AddTo(inner);
        var headers = new Dictionary<string, string> { [RemoteAddressFilter.Header] = "203.0.113.77" };

        for (int attempt = 0; attempt < 2; attempt++)
        {
            await ApiClient.PostAsync(
                server, "/api/v1/auth/dev-login", null, new StringContent("""{"displayName":""}""", System.Text.Encoding.UTF8, "application/json"), headers);
        }

        CapturingLogger.Entry hit = Assert.Single(logs.Events(3001));
        Assert.Equal("DevLogin", hit.Values["LimitClass"]);
        Assert.Null(hit.Values["PlayerId"]);
        AssertNowhere(logs, "203.0.113.77");
    }

    /// <summary><c>GET /city</c> that can't get its city's lock logs the conflict as a warning with the player (ADR-0009, Watch).</summary>
    [Fact]
    public async Task Get_CityConflict_LogsTheConflictWithThePlayer()
    {
        var logs = new CapturingLogger();
        var store = new FakeCityStore { ConflictOnLock = true };
        await using WebApplicationFactory<Program> server = logs.AddTo(store.Serve(factory, new FakeTimeProvider(_start)));

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        CapturingLogger.Entry conflict = Assert.Single(logs.Events(3002));
        Assert.Equal((LogLevel.Warning, (object?)"GET", (object?)"/api/v1/city", (object?)_playerId),
            (conflict.Level, conflict.Values["Method"], conflict.Values["Path"], conflict.Values["PlayerId"]));
        Assert.Empty(logs.Events(3000));
    }

    /// <summary>An unexpected exception's line names the player of the request's token.</summary>
    [Fact]
    public async Task Throw_Unexpected_LogsThePlayer()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = ThrowingEndpoint.AddTo(Serve(logs));

        await ApiClient.GetAsync(server, "/test/throw?kind=internal", _playerId);

        Assert.Equal(_playerId, Assert.Single(logs.Events(3000)).Values["PlayerId"]);
    }

    /// <summary>
    /// Every line a request writes carries its trace id in the <c>TraceId</c> scope: the middle part of the answer's <c>traceId</c>
    /// (W3C <c>00-traceId-spanId-flags</c>), which is how a player's report is found in the log (docs/api/README.md).
    /// </summary>
    [Fact]
    public async Task Get_RefusedRequests_LinesCarryTheAnswersTraceId()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(logs, ("RateLimits:City:Limit", "1"), ("RateLimits:City:WindowSeconds", "60"));

        ApiResponse unauthorized = await ApiClient.GetAsync(server, "/api/v1/city", playerId: null);
        await ApiClient.GetAsync(server, "/api/v1/city", _playerId);
        ApiResponse limited = await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        Assert.Equal(TraceIdOf(unauthorized), Assert.Single(logs.Events(4003)).Scope("TraceId"));
        Assert.Equal(TraceIdOf(limited), Assert.Single(logs.Events(3001)).Scope("TraceId"));
        Assert.NotEqual(TraceIdOf(unauthorized), TraceIdOf(limited));
    }

    /// <summary>The trace id of an answer: the second part of its Problem Details' <c>traceId</c>.</summary>
    /// <param name="response">An error answer.</param>
    /// <returns>The 32 hex digits of the trace.</returns>
    internal static string TraceIdOf(ApiResponse response)
    {
        string[] parts = ((string)response.Json()["traceId"]!).Split('-');
        Assert.Equal(4, parts.Length);
        return parts[1];
    }

    /// <summary>Asserts that no text is in any line the server logged.</summary>
    /// <param name="logs">The lines.</param>
    /// <param name="texts">The texts that must not appear.</param>
    internal static void AssertNowhere(CapturingLogger logs, params string[] texts)
    {
        foreach (CapturingLogger.Entry entry in logs.Entries)
        {
            foreach (string text in texts.Where(text => text.Length > 0))
            {
                Assert.DoesNotContain(text, entry.AllText(), StringComparison.Ordinal);
            }
        }
    }
}
