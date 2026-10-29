using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CityBuilder.Api.Contracts;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Players;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Logging;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;
using DatabaseLocks = CityBuilder.IntegrationTests.Persistence.DatabaseLocks;

namespace CityBuilder.IntegrationTests.Auth;

/// <summary>
/// The dev login end to end on PostgreSQL (<c>POST /api/v1/auth/dev-login</c>, the server clock a <c>FakeTimeProvider</c> at
/// <see cref="PostgresApiFactory.Start"/>): the first login of a name stores the player, the starter city and its <c>STARTING_GRANT</c>
/// ledger rows in one transaction; later logins of the name find the same player; the token acts for that player.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class DevLoginDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The dev login's path.</summary>
    private static readonly Uri _devLogin = new("/api/v1/auth/dev-login", UriKind.Relative);

    /// <summary>
    /// The first login of a name stores the player, their starter city (the content's starting city, clock 0, settled at the server's
    /// second, state version 1) and one <c>STARTING_GRANT</c> row per item, whose deltas add up to the stored stock; the token is for that
    /// player and expires 12 hours later.
    /// </summary>
    [Fact]
    public async Task PostDevLogin_NewName_StoresPlayerStarterCityAndStartingGrant()
    {
        DevLoginResponse login = await LoginAsync("dev1");

        Guid playerId = PlayerOf(login);
        await using CityBuilderDbContext db = Database.CreateContext();
        PlayerRow player = await db.Players.SingleAsync(TestContext.Current.CancellationToken);
        CityRow city = await db.Cities.SingleAsync(TestContext.Current.CancellationToken);
        List<CityInventoryRow> inventory = await db.CityInventory.ToListAsync(TestContext.Current.CancellationToken);
        List<LedgerEntryRow> ledger = await LedgerAsync(Database, city.Id);
        CityState expected = CityFactory.CreateStarterCity(ContentOf(Factory));

        Assert.Equal((playerId, "dev1"), (player.Id, player.DevName));
        Assert.Equal(PostgresApiFactory.Start.AddHours(12), login.ExpiresAtUtc);
        Assert.Equal(
            (playerId, (short)expected.Era, 0L, PostgresApiFactory.Start.ToUnixTimeSeconds(), 1L, ContentOf(Factory).ContentVersion),
            (city.PlayerId, city.Era, city.ClockSeconds, city.LastSettledUnix, city.StateVersion, city.ContentVersion));
        Assert.Empty(await db.CityBuildings.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            expected.Inventory.Select(stock => (stock.Key.Value, stock.Value)).Order(),
            inventory.Select(row => (row.ItemId, row.Quantity)).Order());
        Assert.All(ledger, row => Assert.Equal(("STARTING_GRANT", row.Delta, (string?)null, (string?)null), (row.Reason, row.BalanceAfter, row.RefType, row.RefId)));
        Assert.Equal(
            inventory.Select(row => (row.ItemId, row.Quantity)).Order(),
            ledger.GroupBy(row => row.ItemId).Select(item => (item.Key, item.Sum(row => row.Delta))).Order());
    }

    /// <summary>
    /// A second login of the same name returns the same player and stores nothing: no second player or city, no new ledger rows, the city's
    /// state version unchanged.
    /// </summary>
    [Fact]
    public async Task PostDevLogin_SameNameAgain_ReturnsSamePlayerAndStoresNothing()
    {
        DevLoginResponse first = await LoginAsync("dev1");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));

        DevLoginResponse second = await LoginAsync("dev1");

        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(PlayerOf(first), PlayerOf(second));
        Assert.Equal(1, await db.Players.CountAsync(TestContext.Current.CancellationToken));
        CityRow city = await db.Cities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1L, city.StateVersion);
        Assert.Equal(
            CityFactory.CreateStarterCity(ContentOf(Factory)).Inventory.Count(stock => stock.Value > 0),
            (await LedgerAsync(Database, city.Id)).Count);
    }

    /// <summary>Two names are two players, each with a city of their own.</summary>
    [Fact]
    public async Task PostDevLogin_TwoNames_StoresTwoPlayersWithACityEach()
    {
        Guid first = PlayerOf(await LoginAsync("dev1"));
        Guid second = PlayerOf(await LoginAsync("Dev1"));

        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.NotEqual(first, second);
        Assert.Equal(
            new[] { first, second }.Order(),
            (await db.Cities.Select(city => city.PlayerId).ToListAsync(TestContext.Current.CancellationToken)).Order());
    }

    /// <summary>The token the dev login returns lets its bearer act as that player, and only that player.</summary>
    [Fact]
    public async Task PostDevLogin_ReturnedToken_ActsForThePlayer()
    {
        await using WebApplicationFactory<Program> withEndpoint = CurrentPlayerEndpoint.AddTo(Factory);
        using HttpClient client = withEndpoint.CreateClient();
        DevLoginResponse login = await LoginAsync(client, "dev1");
        using var request = new HttpRequestMessage(HttpMethod.Get, CurrentPlayerEndpoint.Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await using CityBuilderDbContext db = Database.CreateContext();
        Guid stored = (await db.Players.SingleAsync(TestContext.Current.CancellationToken)).Id;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(stored.ToString("D"), await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Two first logins of one name race: while another request's insert of the name is not yet committed, the login's own insert waits on
    /// the unique index, fails once the other commits, and the login returns the other's player. One player, one city.
    /// </summary>
    [Fact]
    public async Task PostDevLogin_SameNameCommittedFirstByAnotherRequest_ReturnsThatPlayer()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var winner = new Player(new Guid("de71061e-0000-4000-8000-000000000001"), "dev1");
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        scope.ServiceProvider.GetRequiredService<IPlayerRepository>().Add(winner);
        GameContent content = ContentOf(Factory);
        scope.ServiceProvider.GetRequiredService<INewCityRepository>().Add(
            Starter(content, new Guid("de71061e-0000-4000-8000-0000000000c1"), winner.Id, PostgresApiFactory.Start.ToUnixTimeSeconds()), []);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        Task<DevLoginResponse> login = LoginAsync("dev1");
        await DatabaseLocks.WaitUntilWaitingAsync(Database, 1);
        await transaction.CommitAsync(cancellationToken);

        Assert.Equal(winner.Id, PlayerOf(await login));
        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(1, await db.Players.CountAsync(cancellationToken));
        Assert.Equal(1, await db.Cities.CountAsync(cancellationToken));
    }

    /// <summary>
    /// Each login is logged with the player and whether it created them (event id 4000): new the first time, known the second; neither the
    /// display name nor the token is in any line.
    /// </summary>
    [Fact]
    public async Task Security_PostDevLogin_LogsThePlayerNeverTheNameOrToken()
    {
        var logs = new CapturingLogger();
        await using WebApplicationFactory<Program> server = logs.AddTo(Factory);
        using HttpClient client = server.CreateClient();

        DevLoginResponse first = await LoginAsync(client, "zz_logged_name");
        DevLoginResponse second = await LoginAsync(client, "zz_logged_name");

        IReadOnlyList<CapturingLogger.Entry> logins = logs.Events(4000);
        Assert.Equal(2, logins.Count);
        Assert.Equal(((object?)PlayerOf(first), (object?)true), (logins[0].Values["PlayerId"], logins[0].Values["IsNewPlayer"]));
        Assert.Equal(((object?)PlayerOf(second), (object?)false), (logins[1].Values["PlayerId"], logins[1].Values["IsNewPlayer"]));
        RequestLogTests.AssertNowhere(logs, ["zz_logged_name", .. first.AccessToken.Split('.'), .. second.AccessToken.Split('.')]);
    }

    /// <summary>Logs the name in through the test's server and reads the answer.</summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>The answer.</returns>
    private async Task<DevLoginResponse> LoginAsync(string displayName)
    {
        using HttpClient client = Factory.CreateClient();
        return await LoginAsync(client, displayName);
    }

    /// <summary>Logs the name in through the client and reads the answer; fails unless it is 200.</summary>
    /// <param name="client">A client of the server.</param>
    /// <param name="displayName">The display name.</param>
    /// <returns>The answer.</returns>
    private static async Task<DevLoginResponse> LoginAsync(HttpClient client, string displayName)
    {
        using HttpResponseMessage response =
            await client.PostAsJsonAsync(_devLogin, new { displayName }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DevLoginResponse>(TestContext.Current.CancellationToken))!;
    }

    /// <summary>The player id in the answer's token (its <c>sub</c> claim).</summary>
    /// <param name="login">The dev login's answer.</param>
    /// <returns>The player id.</returns>
    private static Guid PlayerOf(DevLoginResponse login) => Guid.Parse(new JsonWebToken(login.AccessToken).Subject);
}
