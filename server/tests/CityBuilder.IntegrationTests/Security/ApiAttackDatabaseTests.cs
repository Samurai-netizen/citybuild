using System.Net;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Security;

/// <summary>
/// The API attacked as a malicious client would (M0-30, <c>core-instructions/07-security-model.md</c>), end to end on PostgreSQL over HTTP
/// with the server's real content and a starter city per player (700 coins, 40 logs, 10 planks, nothing built). Every test checks what was
/// stored, not only the answer: the city (version, clock, buildings, stock), its ledger rows and the receipts, or the players. Most attacks
/// wait five minutes first, so a request that got as far as the settle would have committed that time: a city stored unchanged proves
/// nothing ran. The in-memory versions, with more cases each, are in <see cref="ApiAttackContractTests"/>.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class ApiAttackDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The place-building endpoint.</summary>
    private const string _path = "/api/v1/commands/place-building";

    /// <summary>The dev login.</summary>
    private const string _devLogin = "/api/v1/auth/dev-login";

    /// <summary>The request id the tests send.</summary>
    private const string _requestId = "1de9e700-0000-4000-8000-0000000003b1";

    /// <summary>A valid body: a house (50 coins, 30 s) on tile (0, 0).</summary>
    private const string _placeHouse = $$"""{"requestId":"{{_requestId}}","buildingTypeId":"house","x":0,"y":0}""";

    /// <summary>
    /// A body with a field the client may not set is refused (400), whatever the field claims, and nothing is stored: not the elapsed five
    /// minutes, not a building, not a ledger row or a receipt. The fields of the attack list: a cost, coins, a level, labor, workers, a
    /// player, a timer.
    /// </summary>
    /// <param name="extra">The extra property and its value, as JSON.</param>
    [Theory]
    [InlineData("\"cost\":{\"coins\":0}")]
    [InlineData("\"coins\":1000000")]
    [InlineData("\"level\":5")]
    [InlineData("\"labor\":0")]
    [InlineData("\"cycleLabor\":1")]
    [InlineData("\"workers\":99")]
    [InlineData("\"playerId\":\"1de9e700-0000-4000-8000-0000000003b2\"")]
    [InlineData("\"phaseEndsAt\":0")]
    public async Task Security_PostWithAFieldTheContractDoesNotHave_Is400AndStoresNothing(string extra)
    {
        (Guid player, Guid city) = await StoreStarterAsync("attacker");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(Factory, _path, player, _placeHouse.Replace("}", "," + extra + "}", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A body the contract refuses for its form is 400 and stores nothing, five minutes after the last settle: a <c>requestId</c> that is no
    /// UUID, broken JSON, a property twice, and a type id that can't be a content id (refused before the lock, so the elapsed time is not
    /// settled either).
    /// </summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData("""{"requestId":"' OR 1=1 --","buildingTypeId":"house","x":0,"y":0}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"house","x":0,""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"house","x":0,"x":1,"y":0}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"House","x":0,"y":0}""")]
    [InlineData($$"""{"requestId":"{{_requestId}}","buildingTypeId":"house'; DELETE FROM cities; --","x":0,"y":0}""")]
    public async Task Security_BodyOfTheWrongForm_Is400AndStoresNothing(string body)
    {
        (Guid player, Guid city) = await StoreStarterAsync("attacker");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(Factory, _path, player, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A megabyte body (the type id) is <c>INVALID_ARGUMENT</c> (400) before it is read, five minutes after the last settle, and nothing is
    /// stored: without the limit it would have locked the city and committed the five minutes before <c>UNKNOWN_BUILDING_TYPE</c>.
    /// </summary>
    [Fact]
    public async Task Security_MegabyteBody_Is400AndStoresNothing()
    {
        (Guid player, Guid city) = await StoreStarterAsync("attacker");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);
        string body = $$"""{"requestId":"{{_requestId}}","buildingTypeId":"{{new string('a', 1_048_576)}}","x":0,"y":0}""";

        ApiResponse response = await ApiClient.PostAsync(Factory, _path, player, body);

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_ARGUMENT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A negative coordinate (past the <c>smallint</c> columns the tile is stored in, down to the limits of <c>int</c>) is
    /// <c>INVALID_ARGUMENT</c> (400) before the city is locked (owner directive OD-13), five minutes after the last settle, and nothing is
    /// stored, not even the five minutes: no building row at a wrapped or clipped tile, no cost paid.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, -32769)]
    [InlineData(int.MinValue, int.MaxValue)]
    public async Task Security_NegativeCoordinates_Are400AndStoreNothing(int x, int y)
    {
        (Guid player, Guid city) = await StoreStarterAsync("attacker");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(
            Factory, _path, player, $$"""{"requestId":"{{_requestId}}","buildingTypeId":"house","x":{{x}},"y":{{y}}}""");

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_ARGUMENT"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// Coordinates past the 8 × 8 grid (past the <c>smallint</c> columns the tile is stored in, and the limits of <c>int</c>) are
    /// <c>OUT_OF_BOUNDS</c> (422) by the rule, and nothing is stored: no building row at a wrapped or clipped tile, no cost paid.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    [Theory]
    [InlineData(8, 0)]
    [InlineData(32768, 0)]
    [InlineData(65536, 65536)]
    [InlineData(0, int.MaxValue)]
    public async Task Security_CoordinatesPastTheGrid_AreOutOfBoundsAndStoreNothing(int x, int y)
    {
        (Guid player, Guid city) = await StoreStarterAsync("attacker");
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(
            Factory, _path, player, $$"""{"requestId":"{{_requestId}}","buildingTypeId":"house","x":{{x}},"y":{{y}}}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.Equal("OUT_OF_BOUNDS", (string)response.Json()["code"]!);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A replay storm applies the command once: twenty identical requests at once, then thirty more one by one while the clock moves a
    /// second each, all answer 200 with the same bytes; the city holds one house, the cost is paid once (one ledger row), there is one
    /// receipt, and after the first answer nothing more was stored (the replays settle nothing).
    /// </summary>
    [Fact]
    public async Task Security_ReplayStorm_AppliesTheCommandOnce()
    {
        (Guid player, Guid city) = await StoreStarterAsync("attacker");

        ApiResponse[] burst = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => ApiClient.PostAsync(Factory, _path, player, _placeHouse)));
        string stored = await StoredAsync(Factory, Database, player, city);
        var sequential = new List<ApiResponse>();
        for (int i = 0; i < 30; i++)
        {
            Factory.Clock.Advance(TimeSpan.FromSeconds(1));
            sequential.Add(await ApiClient.PostAsync(Factory, _path, player, _placeHouse));
        }

        ApiResponse[] all = [.. burst, .. sequential];
        Assert.All(all, response => Assert.Equal(HttpStatusCode.OK, response.Status));
        Assert.Single(all.Select(response => response.Body).Distinct(StringComparer.Ordinal));
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
        Assert.Single((await LoadAsync(Factory, player)).State.Buildings);
        Assert.Single(await LedgerAsync(Database, city));
        Assert.Single(await ReceiptsAsync(Database, player));
    }

    /// <summary>A command without a token is 401 and stores nothing in any city, also after time has passed.</summary>
    [Fact]
    public async Task Security_PostWithoutToken_Is401AndStoresNothing()
    {
        (Guid player, Guid city) = await StoreStarterAsync("victim");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        string stored = await StoredAsync(Factory, Database, player, city);

        ApiResponse response = await ApiClient.PostAsync(Factory, _path, playerId: null, _placeHouse);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal(stored, await StoredAsync(Factory, Database, player, city));
    }

    /// <summary>
    /// A valid token of a player deleted from the database (a reset development database) is 401 on PostgreSQL too, on the read and on a command:
    /// the real city lock finds no row. The login that follows makes a new player with a new city (CR-0004).
    /// </summary>
    [Fact]
    public async Task Security_TokenOfAPlayerWithoutACity_Is401ThenANewLoginWorks()
    {
        Guid deleted = Guid.CreateVersion7();

        ApiResponse read = await ApiClient.GetAsync(Factory, "/api/v1/city", deleted);
        ApiResponse command = await ApiClient.PostAsync(Factory, _path, deleted, _placeHouse);
        ApiResponse login = await ApiClient.PostAsync(Factory, _devLogin, playerId: null, """{"displayName":"back-again"}""");

        Assert.Equal(HttpStatusCode.Unauthorized, read.Status);
        Assert.Equal("AUTHENTICATION_REQUIRED", (string)read.Json()["code"]!);
        Assert.Equal(HttpStatusCode.Unauthorized, command.Status);
        Assert.Equal("AUTHENTICATION_REQUIRED", (string)command.Json()["code"]!);
        Assert.Equal(HttpStatusCode.OK, login.Status);
    }

    /// <summary>
    /// The dev login refuses what its contract doesn't allow, and no player or city is created: a field it doesn't have (a player id) next to
    /// a valid name, a display name of 4000 characters, a megabyte body.
    /// </summary>
    /// <param name="nameLength">The display name's length; the name is that many <c>a</c>s.</param>
    /// <param name="extra">More properties after the name, as JSON with a leading comma, or empty.</param>
    [Theory]
    [InlineData(4, ",\"playerId\":\"1de9e700-0000-4000-8000-0000000003b2\"")]
    [InlineData(4000, "")]
    [InlineData(1_048_576, "")]
    public async Task Security_DevLoginRefusedBody_StoresNoPlayer(int nameLength, string extra)
    {
        string body = "{\"displayName\":\"" + new string('a', nameLength) + "\"" + extra + "}";

        ApiResponse response = await ApiClient.PostAsync(Factory, _devLogin, playerId: null, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(0, await db.Players.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await db.Cities.CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Stores a player with the starter city, last settled at the factory's start.</summary>
    /// <param name="name">The player's dev name.</param>
    /// <returns>The player and their city.</returns>
    private async Task<(Guid Player, Guid City)> StoreStarterAsync(string name)
    {
        (Guid player, Guid city) = (Guid.NewGuid(), Guid.NewGuid());
        await StoreAsync(Factory, new Player(player, name), Starter(ContentOf(Factory), city, player, PostgresApiFactory.Start.ToUnixTimeSeconds()));
        return (player, city);
    }
}
