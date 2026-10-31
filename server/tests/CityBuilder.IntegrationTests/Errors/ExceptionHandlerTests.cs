using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CityBuilder.Api.Auth;
using CityBuilder.Api.Errors;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Contracts.ProblemAssert;

namespace CityBuilder.IntegrationTests.Errors;

/// <summary>
/// The global exception handler and the status code pages (<c>core-instructions/05-api-contract.md</c>, errors; <c>07</c>, information
/// leakage): whatever a request throws is the catalogue's Problem Details, no answer holds a stack trace, outside Development none says
/// anything about the exception, the exception is in the log, and the answers the framework writes empty (no route, wrong method, wrong
/// content type) have the catalogue's Problem Details with its code. The paths are the real endpoints over a <see cref="FakeCityStore"/> and, for
/// exceptions no endpoint throws, <see cref="ThrowingEndpoint"/>. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; each test derives a copy.</param>
public sealed class ExceptionHandlerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>A valid place-building body, so that what fails is not the body.</summary>
    private const string _placeWoodcutter = """{"requestId":"0199a1b2-c3d4-7e5f-8a9b-00000000000a","buildingTypeId":"woodcutter","x":2,"y":3}""";

    /// <summary>The request id of <see cref="_placeWoodcutter"/>.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-00000000000a";

    /// <summary>The player the tokens are for: one the store has no city for.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>The endpoints that read a body, with the body shapes none of them can bind.</summary>
    public static TheoryData<string, string> UnbindableBodies
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (string path in new[] { _commandPaths[0], _commandPaths[1], _commandPaths[2], _commandPaths[3], _devLoginPath })
            {
                foreach (string body in new[]
                {
                    "{", """{"requestId":"not a uuid"}""", """{"requestId":"0199a1b2-c3d4-7e5f-8a9b-00000000000a","cost":0}""", "[]", "\"text\"",
                    """{"requestId":"0199a1b2-c3d4-7e5f-8a9b-00000000000a","requestId":"0199a1b2-c3d4-7e5f-8a9b-00000000000b"}""",
                })
                {
                    data.Add(path, body);
                }
            }

            return data;
        }
    }

    /// <summary>The command endpoints.</summary>
    private static readonly string[] _commandPaths =
    [
        "/api/v1/commands/place-building", "/api/v1/commands/upgrade-building", "/api/v1/commands/assign-workers",
        "/api/v1/commands/start-research",
    ];

    /// <summary>The dev login, which exists only in Development.</summary>
    private const string _devLoginPath = "/api/v1/auth/dev-login";

    /// <summary>The server with the store, the clock, the environment and the log provider of a test.</summary>
    /// <param name="production">Whether the server runs in Production instead of Development (the factory's default).</param>
    /// <param name="logs">Collects what the server logs.</param>
    /// <param name="store">The cities, or <see langword="null"/> for an empty store.</param>
    /// <returns>The server; the caller disposes it.</returns>
    private WebApplicationFactory<Program> Serve(bool production, CapturingLogger logs, FakeCityStore? store = null)
    {
        WebApplicationFactory<Program> server = production ? factory.WithWebHostBuilder(host => host.UseEnvironment("Production")) : factory;
        server = (store ?? new FakeCityStore()).Serve(server, new FakeTimeProvider(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero)));
        return ThrowingEndpoint.AddTo(logs.AddTo(server));
    }

    /// <summary>
    /// An unexpected exception is 500 <c>INTERNAL_ERROR</c> with the contract's exact members. In Development the detail names the exception
    /// to help the developer; there is never a stack trace.
    /// </summary>
    [Fact]
    public async Task Throw_Unexpected_IsInternalErrorAndDevelopmentNamesTheException()
    {
        await using WebApplicationFactory<Program> server = Serve(production: false, new CapturingLogger());

        ApiResponse response = await ApiClient.GetAsync(server, "/test/throw?kind=internal", playerId: null);

        AssertProblem(response, HttpStatusCode.InternalServerError, "INTERNAL_ERROR", requestId: null);
        string detail = (string)response.Json()["detail"]!;
        Assert.Contains(nameof(InvalidOperationException), detail, StringComparison.Ordinal);
        Assert.Contains(ThrowingEndpoint.SecretMessage, detail, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", response.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Outside Development nothing of the exception reaches the client: not its message, its type, a path or a stack, only the catalogue's
    /// text. The exception is in the log with its stack, where an operator looks.
    /// </summary>
    [Fact]
    public async Task Security_Throw_InProduction_SaysNothingAboutTheException()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(production: true, logs);

        ApiResponse response = await ApiClient.GetAsync(server, "/test/throw?kind=internal", playerId: null);

        AssertProblem(response, HttpStatusCode.InternalServerError, "INTERNAL_ERROR", requestId: null);
        Assert.Equal(ErrorCatalogue.InternalError.Detail, (string)response.Json()["detail"]!);
        foreach (string leaked in new[] { ThrowingEndpoint.SecretMessage, "db.internal", nameof(InvalidOperationException), "   at ", ".cs" })
        {
            Assert.DoesNotContain(leaked, response.Body, StringComparison.Ordinal);
        }

        Assert.Contains(
            logs.Entries,
            entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException { Message: ThrowingEndpoint.SecretMessage });
    }

    /// <summary>A lock timeout or a changed city that no endpoint caught is 409 <c>CONCURRENCY_CONFLICT</c>: safe to retry.</summary>
    [Fact]
    public async Task Throw_ConcurrencyConflict_Is409()
    {
        await using WebApplicationFactory<Program> server = Serve(production: true, new CapturingLogger());

        ApiResponse response = await ApiClient.GetAsync(server, "/test/throw?kind=conflict", playerId: null);

        AssertProblem(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", requestId: null);
    }

    /// <summary>
    /// A body the framework refuses is 400 <c>INVALID_ARGUMENT</c>; one Kestrel stops at the size limit (413 in the framework) is the same
    /// 400 with the limit named, as the <c>Content-Length</c> check answers it.
    /// </summary>
    /// <param name="kind">What the test path throws.</param>
    /// <param name="detail">What the detail says outside Development.</param>
    [Theory]
    [InlineData("bad400", "The body must be one JSON object with exactly the fields of the command.")]
    [InlineData("bad413", "The body must not be larger than 4096 bytes.")]
    public async Task Throw_BadRequest_Is400InvalidArgument(string kind, string detail)
    {
        await using WebApplicationFactory<Program> server = Serve(production: true, new CapturingLogger());

        ApiResponse response = await ApiClient.GetAsync(server, "/test/throw?kind=" + kind, playerId: null);

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId: null);
        Assert.Equal(detail, (string)response.Json()["detail"]!);
    }

    /// <summary>
    /// A body Kestrel stops at its limit ends the request with an empty 413 inside the binding, where no exception reaches the handler (found
    /// by hand on the real server: it was 413 with no code). The status code pages answer it as the contract's 400 <c>INVALID_ARGUMENT</c>, the
    /// same answer the <c>Content-Length</c> check gives.
    /// </summary>
    [Fact]
    public async Task Request_BodyStoppedByKestrel_Is400InvalidArgumentNot413()
    {
        await using WebApplicationFactory<Program> server = Serve(production: true, new CapturingLogger());

        ApiResponse response = await ApiClient.GetAsync(server, "/test/throw?kind=status413", playerId: null);

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId: null);
        Assert.Equal("The body must not be larger than 4096 bytes.", (string)response.Json()["detail"]!);
    }

    /// <summary>
    /// A valid token for a player the store has no city for (a reset development database) reaches the city lock, which finds nothing: the
    /// same 401 as every other token that can't be used, with its <c>WWW-Authenticate</c> header, so the client logs in again. Until CR-0004 this
    /// was a 500 until the token expired. It is no server fault, so it logs a refused token and no error, in Development and Production alike.
    /// </summary>
    /// <param name="production">Whether the server runs in Production.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Security_Get_City_OfAPlayerWithoutACity_IsAuthenticationRequired(bool production)
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = Serve(production, logs);

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        AssertProblem(response, HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED", requestId: null);
        Assert.Equal(ErrorCatalogue.AuthenticationRequired.Detail, (string)response.Json()["detail"]!);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
        Assert.Empty(logs.Events(3000));
        Assert.Contains(logs.Events(4003), entry => entry.Message.Contains("NoCity", StringComparison.Ordinal));
    }

    /// <summary>A command of a player without a city is the same 401: the lock finds nothing, and nothing is stored.</summary>
    [Fact]
    public async Task Security_Post_Command_OfAPlayerWithoutACity_IsAuthenticationRequired()
    {
        var store = new FakeCityStore();
        await using WebApplicationFactory<Program> server = Serve(production: true, new CapturingLogger(), store);

        ApiResponse response = await ApiClient.PostAsync(server, "/api/v1/commands/place-building", _playerId, _placeWoodcutter);

        AssertProblem(response, HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED", requestId: null);
        Assert.Equal(1, store.LockCount);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// A body without a <c>Content-Type</c> is 415 like a body of another type: the framework's refusal of it is an exception with status
    /// 415, unlike the routing's for a wrong type (found on a running server: it was 400).
    /// </summary>
    [Fact]
    public async Task Post_BodyWithoutAContentType_Is415()
    {
        await using WebApplicationFactory<Program> server = Serve(production: true, new CapturingLogger());
        using var content = new StringContent(_placeWoodcutter, Encoding.UTF8);
        content.Headers.ContentType = null;

        ApiResponse response = await ApiClient.PostAsync(server, "/api/v1/commands/place-building", _playerId, content, headers: null);

        AssertProblem(response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE", requestId: null);
    }

    /// <summary>A command whose city lock isn't granted answers 409 <c>CONCURRENCY_CONFLICT</c> with the command's request id, to retry.</summary>
    [Fact]
    public async Task Post_PlaceBuilding_LockNotGranted_IsConflictWithTheRequestId()
    {
        var store = new FakeCityStore { ConflictOnLock = true };
        await using WebApplicationFactory<Program> server = Serve(production: true, new CapturingLogger(), store);

        ApiResponse response = await ApiClient.PostAsync(server, "/api/v1/commands/place-building", _playerId, _placeWoodcutter);

        AssertProblem(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", _requestId);
        Assert.Equal(1, store.LockCount);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary><c>GET /city</c> whose city lock isn't granted is the same 409, with no request id, because a read is no command.</summary>
    [Fact]
    public async Task Get_City_LockNotGranted_IsConflictWithoutARequestId()
    {
        await using WebApplicationFactory<Program> server = Serve(production: true, new CapturingLogger(), new FakeCityStore { ConflictOnLock = true });

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/city", _playerId);

        AssertProblem(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", requestId: null);
    }

    /// <summary>
    /// A body that doesn't bind (broken JSON, a wrong type, a field the contract doesn't have, a property twice, no object) is 400
    /// <c>INVALID_ARGUMENT</c> with a <c>code</c>, in Development as well as Production, on every endpoint that reads a body.
    /// </summary>
    /// <param name="path">The endpoint.</param>
    /// <param name="body">The body.</param>
    [Theory]
    [MemberData(nameof(UnbindableBodies))]
    public async Task Post_BodyThatDoesNotBind_IsInvalidArgument(string path, string body)
    {
        // The dev login is mapped in Development only (07).
        foreach (bool production in path == _devLoginPath ? new[] { false } : new[] { false, true })
        {
            await using WebApplicationFactory<Program> server = Serve(production, new CapturingLogger());

            ApiResponse response = await ApiClient.PostAsync(server, path, _playerId, body);

            AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId: null);
        }
    }

    /// <summary>
    /// The 400 for a body that doesn't bind never echoes the request headers, the bearer token above all. In Development the framework's own
    /// answer is the developer exception page, which does (M0-32 follow-up); the handler replaces it.
    /// </summary>
    [Fact]
    public async Task Security_BodyThatDoesNotBind_InDevelopment_DoesNotEchoTheRequestHeaders()
    {
        await using WebApplicationFactory<Program> server = Serve(production: false, new CapturingLogger());
        string token = server.Services.GetRequiredService<AccessTokens>().Issue(_playerId).Token;
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        ApiResponse response = await ApiClient.PostAsync(
            server,
            "/api/v1/commands/place-building",
            playerId: null,
            content,
            new Dictionary<string, string> { ["Authorization"] = "Bearer " + token, ["X-Probe"] = "probe-value-3141" });

        AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", requestId: null);
        foreach (string echoed in new[] { token, "Bearer", "Authorization", "probe-value-3141", "   at " })
        {
            Assert.DoesNotContain(echoed, response.Body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A path no endpoint has, a method an endpoint doesn't take and a body that isn't JSON get the framework's empty answers; the status
    /// code pages give them the catalogue's Problem Details with its code (M0-34 decision 3).
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path.</param>
    /// <param name="contentType">The content type of the body to send, or <see langword="null"/> for none.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData("GET", "/api/v1/nothing-here", null, HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData("GET", "/api/v1/commands/place-building", null, HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED")]
    [InlineData("POST", "/api/v1/commands/place-building", "text/plain", HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE")]
    public async Task Request_NoEndpointMethodOrMediaType_HasTheCataloguesProblem(
        string method, string path, string? contentType, HttpStatusCode status, string code)
    {
        await using WebApplicationFactory<Program> server = Serve(production: true, new CapturingLogger());
        using HttpClient client = server.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Services.GetRequiredService<AccessTokens>().Issue(_playerId).Token);
        if (contentType is not null)
        {
            request.Content = new StringContent(_placeWoodcutter, Encoding.UTF8, contentType);
        }

        using HttpResponseMessage raw = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await raw.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        AssertProblem(new ApiResponse(raw.StatusCode, raw.Content.Headers.ContentType?.MediaType, raw.Headers, body), status, code, requestId: null);
    }
}
