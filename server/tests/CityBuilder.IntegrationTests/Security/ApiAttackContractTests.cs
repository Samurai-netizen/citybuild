using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CityBuilder.Api.Http;
using CityBuilder.Application.Cities;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.Simulation.Content;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Security;

/// <summary>
/// The API attacked as a malicious client would (M0-30, <c>core-instructions/07-security-model.md</c>), in memory over a
/// <see cref="FakeCityStore"/>: the real middleware, binding, endpoints and pipeline, no database. Each test names the property it protects
/// and checks that nothing ran: no city locked or saved, no ledger row. The same attacks against stored rows are in
/// <c>ApiAttackDatabaseTests</c>; the fields a body must not have are in <c>PlaceBuildingContractTests</c>.
/// </summary>
/// <param name="factory">The in-memory server (Development, dev login on, no reachable database); a test derives a copy with its city.</param>
public sealed class ApiAttackContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The place-building endpoint.</summary>
    private const string _placeBuilding = "/api/v1/commands/place-building";

    /// <summary>The dev login.</summary>
    private const string _devLogin = "/api/v1/auth/dev-login";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "0199a1b2-c3d4-7e5f-8a9b-0000000003a1";

    /// <summary>The default body limit, <see cref="RequestLimitOptions.MaxBodyBytes"/>.</summary>
    private const int _maxBodyBytes = 4096;

    /// <summary>A megabyte: the attack's body size.</summary>
    private const int _megabyte = 1_048_576;

    /// <summary>The server second the cities were last settled at; the clock stays there, so a refusal saves nothing.</summary>
    private static readonly DateTimeOffset _settledAt = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The player whose token the tests send.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b01");

    /// <summary>Another player, whom an attacker names outside the token.</summary>
    private static readonly Guid _otherPlayerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b02");

    /// <summary>
    /// The bodies above the limit: a megabyte in an unknown field, in the type id, in the display name, and one byte over. Each is refused
    /// with 400 <c>INVALID_ARGUMENT</c> before the body is read or the token checked, so even a request without a token gets that answer.
    /// </summary>
    public static TheoryData<string, int, string, bool> TooLargeBodies => new()
    {
        { _placeBuilding, _megabyte, "junk", true },
        { _placeBuilding, _megabyte, "id", true },
        { _placeBuilding, _maxBodyBytes + 1, "junk", true },
        { _placeBuilding, _megabyte, "junk", false },
        { _devLogin, _megabyte, "name", false },
    };

    /// <summary>
    /// A body larger than the limit is refused with 400 <c>INVALID_ARGUMENT</c> as Problem Details, and nothing runs: no city is locked or
    /// saved. The check comes first, so a request without a token gets the same answer, and the dev login never sees the name.
    /// </summary>
    /// <param name="path">The endpoint.</param>
    /// <param name="size">The body's size in bytes.</param>
    /// <param name="padIn">Where the padding goes: an unknown field <c>junk</c>, the type <c>id</c>, or the display <c>name</c>.</param>
    /// <param name="withToken">Whether the request carries a token.</param>
    [Theory]
    [MemberData(nameof(TooLargeBodies))]
    public async Task Security_BodyLargerThanTheLimit_Is400BeforeAnythingRuns(string path, int size, string padIn, bool withToken)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;
        string body = BodyOfSize(size, padIn);
        Assert.Equal(size, Encoding.UTF8.GetByteCount(body));

        ApiResponse response = await ApiClient.PostAsync(server, path, withToken ? _playerId : null, body);

        AssertInvalidArgument(response);
        Assert.Equal("The body must not be larger than 4096 bytes.", (string)response.Json()["detail"]!);
        AssertNothingRan(store);
    }

    /// <summary>A body of exactly the limit is read: the limit refuses only what is larger, so padding a valid command to it still places it.</summary>
    [Fact]
    public async Task Security_BodyOfExactlyTheLimit_IsRead()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;
        string body = Place("\"woodcutter\"", 2, 3);
        body = body.Insert(1, new string(' ', _maxBodyBytes - body.Length));

        ApiResponse response = await ApiClient.PostAsync(server, _placeBuilding, _playerId, body);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>
    /// Kestrel's own body limit is the setting, not its default of about 30 MB, so a body without a <c>Content-Length</c> (chunked) also
    /// stops being read at the limit; the in-memory test server has no such limit, so this checks the setting Kestrel gets.
    /// </summary>
    /// <param name="setting">The value of <c>RequestLimits:MaxBodyBytes</c>, or <see langword="null"/> for the default.</param>
    /// <param name="expected">Kestrel's limit.</param>
    [Theory]
    [InlineData(null, 4096L)]
    [InlineData("2048", 2048L)]
    public void Security_KestrelBodyLimit_IsTheSetting(string? setting, long expected)
    {
        using WebApplicationFactory<Program> server = setting is null
            ? factory.WithWebHostBuilder(_ => { })
            : factory.WithWebHostBuilder(host => host.UseSetting("RequestLimits:MaxBodyBytes", setting));

        Assert.Equal(expected, server.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize);
    }

    /// <summary>A body limit below 1024 bytes or above a mebibyte fails the options validation at startup, naming the setting.</summary>
    /// <param name="setting">The setting's value.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("1023")]
    [InlineData("1048577")]
    public void Start_MaxBodyBytesOutOfRange_FailsValidation(string setting)
    {
        using WebApplicationFactory<Program> broken = factory.WithWebHostBuilder(host => host.UseSetting("RequestLimits:MaxBodyBytes", setting));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures,
            failure => failure.StartsWith("RequestLimits:MaxBodyBytes must be between 1024 and 1048576", StringComparison.Ordinal));
    }

    /// <summary>
    /// A body that names a property twice is refused (400) and nothing runs, instead of being read as its last value: what a proxy, a log
    /// or the receipt sees of the body is what the server applies. Names differing only in case are the same property.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"x":9,"y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":3,"requestId":"0199a1b2-c3d4-7e5f-8a9b-0000000003a2"}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","buildingTypeId":"sawmill","x":2,"y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"X":9,"y":3}""")]
    public async Task Security_PropertyTwice_Is400AndRunsNothing(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _placeBuilding, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A <c>requestId</c> that isn't a UUID is refused (400) and nothing runs: empty, too short, a wrong character, a number, a boolean, an
    /// object, null, an injection attempt.
    /// </summary>
    /// <param name="requestId">The <c>requestId</c> value, as JSON.</param>
    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"1\"")]
    [InlineData("\"0199a1b2-c3d4-7e5f-8a9b-00000000003\"")]
    [InlineData("\"0199a1b2-c3d4-7e5f-8a9b-00000000003g\"")]
    [InlineData("\"0199a1b2-c3d4-7e5f-8a9b-0000000003a1-0000\"")]
    [InlineData("\"' OR 1=1 --\"")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Security_RequestIdThatIsNoUuid_Is400AndRunsNothing(string requestId)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(
            server, _placeBuilding, _playerId, $$"""{"requestId":{{requestId}},"buildingTypeId":"woodcutter","x":2,"y":3}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A body that isn't valid JSON is refused (400) and nothing runs: cut off, two documents, a trailing comma, single quotes, a comment,
    /// <c>NaN</c>, nesting deeper than the reader allows, a bare string.
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData("{")]
    [InlineData("}{")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":3}{}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":2,"y":3,}""")]
    [InlineData($$"""{'requestId':'{{_requestId}}','buildingTypeId':'woodcutter','x':2,'y':3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}",/* free */"buildingTypeId":"woodcutter","x":2,"y":3}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"woodcutter","x":NaN,"y":3}""")]
    [InlineData("\"woodcutter\"")]
    public async Task Security_MalformedJson_Is400AndRunsNothing(string body)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _placeBuilding, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>A body nested deeper than the JSON reader's limit of 64 is refused (400) without the reader running out of stack, and nothing runs.</summary>
    [Fact]
    public async Task Security_DeeplyNestedJson_Is400AndRunsNothing()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;
        string body = Place("\"woodcutter\"", 2, 3).Replace("\"x\":2", "\"x\":" + new string('[', 1000) + new string(']', 1000), StringComparison.Ordinal);

        ApiResponse response = await ApiClient.PostAsync(server, _placeBuilding, _playerId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// A type id that can't be a content id is <c>INVALID_ARGUMENT</c> (400) before the city is locked: a long one, capitals, a quote and
    /// SQL, a path, a null character. Only an id of the right form costs a settle (and then <c>UNKNOWN_BUILDING_TYPE</c>).
    /// </summary>
    /// <param name="typeId">The type id, as JSON.</param>
    [Theory]
    [InlineData("\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"")]
    [InlineData("\"Woodcutter\"")]
    [InlineData("\"woodcutter'; DROP TABLE cities; --\"")]
    [InlineData("\"../../etc/passwd\"")]
    [InlineData("\"woodcutter\\u0000\"")]
    public async Task Security_TypeIdThatCantBeAContentId_Is400BeforeTheCityIsLocked(string typeId)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _placeBuilding, _playerId, Place(typeId, 2, 3));

        AssertInvalidArgument(response);
        Assert.Equal(_requestId, (string)response.Json()["requestId"]!);
        AssertNothingRan(store);
    }

    /// <summary>
    /// Coordinates outside the 8 × 8 grid, negative or huge (the limits of <c>int</c> and of the stored <c>smallint</c>), are
    /// <c>OUT_OF_BOUNDS</c> (422), and nothing is saved: no building is stored at a wrapped tile.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    [Theory]
    [InlineData(-1, -1)]
    [InlineData(8, 8)]
    [InlineData(int.MinValue, 0)]
    [InlineData(0, int.MaxValue)]
    [InlineData(32767, 0)]
    [InlineData(32768, 0)]
    [InlineData(0, -32769)]
    [InlineData(65538, 65538)]
    public async Task Security_CoordinatesOutsideTheGrid_AreOutOfBoundsAndSaveNothing(int x, int y)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(server, _placeBuilding, _playerId, Place("\"woodcutter\"", x, y));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.Equal("OUT_OF_BOUNDS", (string)response.Json()["code"]!);
        AssertNothingRan(store, lockedOnce: true);
    }

    /// <summary>
    /// A player named outside the token, in the query or in a header, is ignored: the command runs in the token's city, and the other
    /// player's city is untouched.
    /// </summary>
    [Fact]
    public async Task Security_PlayerNamedOutsideTheToken_IsIgnored()
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;
        var headers = new Dictionary<string, string> { ["X-Player-Id"] = _otherPlayerId.ToString(), ["X-User-Id"] = _otherPlayerId.ToString() };

        ApiResponse response = await ApiClient.PostAsync(
            server,
            $"{_placeBuilding}?playerId={_otherPlayerId}",
            _playerId,
            new StringContent(Place("\"woodcutter\"", 2, 3), Encoding.UTF8, "application/json"),
            headers);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Single(store.Stored(_playerId).State.Buildings);
        Assert.Empty(store.Stored(_otherPlayerId).State.Buildings);
        Assert.Equal(0, store.Stored(_otherPlayerId).StateVersion);
    }

    /// <summary>A body that isn't sent as JSON is refused with 415 and nothing runs: plain text, a form, XML.</summary>
    /// <param name="contentType">The media type of the body.</param>
    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("application/xml")]
    public async Task Security_BodyNotSentAsJson_Is415AndRunsNothing(string contentType)
    {
        (FakeCityStore store, WebApplicationFactory<Program> server) = Serve();
        await using WebApplicationFactory<Program> _ = server;

        ApiResponse response = await ApiClient.PostAsync(
            server, _placeBuilding, _playerId, new StringContent(Place("\"woodcutter\"", 2, 3), Encoding.UTF8, contentType), headers: null);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.Status);
        AssertNothingRan(store);
    }

    /// <summary>
    /// The dev login refuses a body with a property its contract doesn't have (400), whatever it claims: a player id, an admin flag. The
    /// in-memory server has no database, so a login that got past the body would fail with 500 instead.
    /// </summary>
    /// <param name="extra">The extra property and its value, as JSON.</param>
    [Theory]
    [InlineData("\"playerId\":\"0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b02\"")]
    [InlineData("\"isAdmin\":true")]
    [InlineData("\"coins\":1000000")]
    public async Task Security_DevLoginWithAFieldTheContractDoesNotHave_Is400(string extra)
    {
        ApiResponse response = await ApiClient.PostAsync(factory, _devLogin, playerId: null, "{\"displayName\":\"dev1\"," + extra + "}");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    /// <summary>
    /// A display name far above the 32 characters allowed, but inside the body limit, is <c>INVALID_ARGUMENT</c> (400) before the database
    /// is reached: no player is looked up or created.
    /// </summary>
    [Fact]
    public async Task Security_DevLoginHugeDisplayName_Is400InvalidArgument()
    {
        ApiResponse response = await ApiClient.PostAsync(factory, _devLogin, playerId: null, "{\"displayName\":\"" + new string('a', 4000) + "\"}");

        AssertInvalidArgument(response);
    }

    /// <summary>A place-building body with the request id and the tile, and the type id as the given JSON.</summary>
    /// <param name="typeIdJson">The <c>buildingTypeId</c> value, as JSON.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The body text.</returns>
    private static string Place(string typeIdJson, int x, int y) =>
        $$"""{"requestId":"{{_requestId}}","buildingTypeId":{{typeIdJson}},"x":{{x}},"y":{{y}}}""";

    /// <summary>A body of exactly the given size in bytes, padded with letters in one place.</summary>
    /// <param name="size">The size in bytes.</param>
    /// <param name="padIn"><c>junk</c> (an unknown field of a place-building body), <c>id</c> (its type id) or <c>name</c> (a dev login's display name).</param>
    /// <returns>The body text, ASCII only.</returns>
    private static string BodyOfSize(int size, string padIn)
    {
        string template = padIn switch
        {
            "junk" => Place("\"woodcutter\"", 2, 3).Replace("}", ",\"junk\":\"{0}\"}", StringComparison.Ordinal),
            "id" => Place("\"{0}\"", 2, 3),
            "name" => "{\"displayName\":\"{0}\"}",
            _ => throw new ArgumentOutOfRangeException(nameof(padIn), padIn, "No such padding."),
        };
        int padding = size - (template.Length - "{0}".Length);
        return template.Replace("{0}", new string('a', padding), StringComparison.Ordinal);
    }

    /// <summary>
    /// A server copy with a store holding two starter cities, the token's player's and another's, settled at <see cref="_settledAt"/>, and
    /// the clock at that second.
    /// </summary>
    /// <returns>The store and the server; the caller disposes the server.</returns>
    private (FakeCityStore Store, WebApplicationFactory<Program> Server) Serve()
    {
        var store = new FakeCityStore();
        GameContent content = ContentOf(factory);
        store.Add(Starter(content, Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c01"), _playerId, _settledAt.ToUnixTimeSeconds()));
        store.Add(Starter(content, Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4c02"), _otherPlayerId, _settledAt.ToUnixTimeSeconds()));
        return (store, store.Serve(factory, new FakeTimeProvider(_settledAt)));
    }

    /// <summary>
    /// Asserts that nothing ran: no city was locked (or, for a rule's refusal, locked once and then left), none was saved, no ledger row was
    /// written, and both cities are as stored, at version 0 with nothing built.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="lockedOnce">Whether the request got as far as the lock (a rule's refusal) instead of being refused before it.</param>
    private static void AssertNothingRan(FakeCityStore store, bool lockedOnce = false)
    {
        Assert.Equal(lockedOnce ? 1 : 0, store.LockCount);
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Ledger);
        foreach (Guid player in new[] { _playerId, _otherPlayerId })
        {
            CityAggregate city = store.Stored(player);
            Assert.Equal(0, city.StateVersion);
            Assert.Empty(city.State.Buildings);
        }
    }

    /// <summary>Asserts a 400 <c>INVALID_ARGUMENT</c> Problem Details answer.</summary>
    /// <param name="response">The answer.</param>
    private static void AssertInvalidArgument(ApiResponse response)
    {
        Assert.Equal((HttpStatusCode.BadRequest, "application/problem+json"), (response.Status, response.ContentType));
        JsonNode problem = response.Json();
        Assert.Equal(("INVALID_ARGUMENT", "urn:citybuilder:error:invalid-argument"), ((string)problem["code"]!, (string)problem["type"]!));
    }
}
