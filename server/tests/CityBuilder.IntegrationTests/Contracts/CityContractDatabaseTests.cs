using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CityBuilder.Api.Contracts;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Commands;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// <c>GET /api/v1/city</c> end to end on PostgreSQL, the server's real content and repositories and the server clock a
/// <c>FakeTimeProvider</c> at <see cref="PostgresApiFactory.Start"/>: the raw JSON the client reads is checked against what the request stored,
/// so the snapshot can't show a state the database doesn't hold. The hand-built cases and the exact text are in
/// <see cref="CitySnapshotContractTests"/>, without a database.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CityContractDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>
    /// An hour and 30 seconds after the last settlement, when the city's 1-hour research has ended: the snapshot shows the city the request
    /// stored (its clock, stock, version and buildings), and its settlement block is the sum of the ledger rows the request wrote.
    /// </summary>
    [Fact]
    public async Task Get_AfterTheResearchEnded_ReturnsWhatTheRequestStored()
    {
        (Guid player, Guid city) = await DatabaseCommands.StorePlayerAsync(Factory, "dev1");
        long versionBefore = await StoredVersionAsync();
        Factory.Clock.Advance(TimeSpan.FromSeconds(3630));

        ApiResponse response = await ApiClient.GetAsync(Factory, "/api/v1/city", player);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        JsonNode snapshot = response.Json();
        JsonShape.AssertObject(snapshot, "requestId:string? stateVersion:int serverTimeUtc:string contentVersion:string city:object settlement:object", "$");

        await using CityBuilderDbContext db = Database.CreateContext();
        CityRow row = await db.Cities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        List<CityInventoryRow> stock = await db.CityInventory.AsNoTracking().Where(item => item.Quantity > 0).ToListAsync(TestContext.Current.CancellationToken);
        int buildingRows = await db.CityBuildings.CountAsync(TestContext.Current.CancellationToken);
        List<LedgerEntryRow> ledger = await LedgerAsync(Database, city);

        Assert.Null(snapshot["requestId"]);
        // The request saved the city once, and the snapshot shows the version it stored.
        Assert.Equal(versionBefore + 1, row.StateVersion);
        Assert.Equal(row.StateVersion, (long)snapshot["stateVersion"]!);
        Assert.Equal("2026-10-26T13:00:30Z", (string)snapshot["serverTimeUtc"]!);
        Assert.Equal(row.ContentVersion, (string)snapshot["contentVersion"]!);
        Assert.Equal(3630, row.ClockSeconds);
        Assert.Equal(row.ClockSeconds, (long)snapshot["city"]!["clock"]!);
        Assert.Equal(Map(stock.Select(item => (item.ItemId, item.Quantity))), snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal(buildingRows, snapshot["city"]!["buildings"]!.AsArray().Count);
        Assert.Equal("""{"activeId":null,"startedAt":null,"endsAt":null,"completed":["frame_saw","sharp_axes"]}""", snapshot["city"]!["research"]!.ToJsonString());

        JsonNode settlement = snapshot["settlement"]!;
        Assert.Equal(3630, (long)settlement["elapsedSeconds"]!);
        Assert.Equal("""["frame_saw"]""", settlement["completedResearch"]!.ToJsonString());
        // What the settlement says it made and took is what the request wrote to the ledger.
        Assert.Equal(
            Map(ledger.Where(entry => entry.Reason == "SETTLEMENT_PRODUCED").GroupBy(entry => entry.ItemId).Select(item => (item.Key, item.Sum(entry => entry.Delta)))),
            settlement["produced"]!.ToJsonString());
        Assert.Equal(
            Map(ledger.Where(entry => entry.Reason == "SETTLEMENT_CONSUMED").GroupBy(entry => entry.ItemId).Select(item => (item.Key, -item.Sum(entry => entry.Delta)))),
            settlement["consumed"]!.ToJsonString());
    }

    /// <summary>A second request in the same second stores nothing: the same city and version, an empty settlement, no new ledger rows.</summary>
    [Fact]
    public async Task Get_TwiceInTheSameSecond_StoresNothingTheSecondTime()
    {
        (Guid player, Guid city) = await DatabaseCommands.StorePlayerAsync(Factory, "dev1");
        Factory.Clock.Advance(TimeSpan.FromSeconds(600));
        JsonNode first = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();
        int ledgerRows = (await LedgerAsync(Database, city)).Count;
        long version = await StoredVersionAsync();

        JsonNode second = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal(version, await StoredVersionAsync());
        Assert.Equal(ledgerRows, (await LedgerAsync(Database, city)).Count);
        Assert.Equal((version, version), ((long)first["stateVersion"]!, (long)second["stateVersion"]!));
        Assert.Equal(first["city"]!.ToJsonString(), second["city"]!.ToJsonString());
        Assert.Equal(0, (long)second["settlement"]!["elapsedSeconds"]!);
        Assert.Equal("{}", second["settlement"]!["produced"]!.ToJsonString());
    }

    /// <summary>
    /// The flow of a new player: the dev login creates the starter city, and the first <c>GET /city</c> shows it as stored (version 1, clock 0,
    /// the starting stock, no buildings, nothing elapsed).
    /// </summary>
    [Fact]
    public async Task Get_AfterTheFirstDevLogin_ReturnsTheStarterCity()
    {
        ApiResponse response = await ApiClient.GetAsync(Factory, "/api/v1/city", await LoginAsync("dev1"));

        JsonNode snapshot = response.Json();
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityInventoryRow> stock = await db.CityInventory.AsNoTracking().Where(item => item.Quantity > 0).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal((1L, "2026-10-26T12:00:00Z", 0L), ((long)snapshot["stateVersion"]!, (string)snapshot["serverTimeUtc"]!, (long)snapshot["city"]!["clock"]!));
        Assert.Equal(Map(stock.Select(item => (item.ItemId, item.Quantity))), snapshot["city"]!["inventory"]!.ToJsonString());
        Assert.Equal("[]", snapshot["city"]!["buildings"]!.ToJsonString());
    }

    /// <summary>Each player sees only their own city: the same request with another token shows the other city.</summary>
    [Fact]
    public async Task Security_TwoPlayers_EachSeeOnlyTheirOwnCity()
    {
        (Guid first, _) = await DatabaseCommands.StorePlayerAsync(Factory, "dev1");
        Guid second = await LoginAsync("dev2");

        JsonNode ofFirst = (await ApiClient.GetAsync(Factory, "/api/v1/city", first)).Json();
        JsonNode ofSecond = (await ApiClient.GetAsync(Factory, "/api/v1/city", second)).Json();

        // The first player's city has a house, a woodcutter and a sawmill; the second's is the starter city with nothing built.
        Assert.Equal(3, ofFirst["city"]!["buildings"]!.AsArray().Count);
        Assert.Empty(ofSecond["city"]!["buildings"]!.AsArray());
    }

    /// <summary>Logs a dev name in over HTTP, which creates the player and the starter city on the first login of the name.</summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>The player id in the token the server issued.</returns>
    private async Task<Guid> LoginAsync(string displayName)
    {
        using HttpClient client = Factory.CreateClient();
        using HttpResponseMessage login = await client.PostAsJsonAsync("/api/v1/auth/dev-login", new { displayName }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        DevLoginResponse token = (await login.Content.ReadFromJsonAsync<DevLoginResponse>(TestContext.Current.CancellationToken))!;
        return Guid.Parse(new JsonWebToken(token.AccessToken).Subject);
    }

    /// <summary>The stored <c>state_version</c> of the only city, read by a context of the test's own.</summary>
    /// <returns>The version.</returns>
    private async Task<long> StoredVersionAsync()
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        return (await db.Cities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).StateVersion;
    }

    /// <summary>An item map as the snapshot writes it: a JSON object in ordinal item order with no spaces.</summary>
    /// <param name="amounts">Item id and quantity pairs.</param>
    /// <returns>The JSON text, for example <c>{"coins":10,"logs":3}</c>.</returns>
    private static string Map(IEnumerable<(string Item, long Quantity)> amounts) =>
        new JsonObject(amounts.OrderBy(amount => amount.Item, StringComparer.Ordinal)
            .Select(amount => KeyValuePair.Create(amount.Item, (JsonNode?)JsonValue.Create(amount.Quantity)))).ToJsonString();
}
