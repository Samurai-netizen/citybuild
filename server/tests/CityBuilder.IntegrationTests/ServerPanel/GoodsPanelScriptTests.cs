using System.Globalization;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.ServerPanel;

/// <summary>
/// <c>server-panel/goods.sql</c> (CR-0006; CR-0008 decision 35), run with the container's <c>psql</c> as <c>panel.sh goods</c> runs it: add,
/// remove and set change the coins of the <c>devtest</c> city's purse or one good in the store of one of its buildings, leave one
/// <c>DEV_ADJUST</c> ledger row that ends on the city's total, bump the state version, and touch no other player; a refusal writes nothing;
/// the engine settles a city whose store is far above its limit without clipping it. The cities are <see cref="WithFrameSawRunning"/>'s: 600
/// coins, the sawmill (building 3) with 38 logs in its store once its first cycle took 2, the woodcutter (building 2) with an empty store.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class GoodsPanelScriptTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The run's server, to run the script on the test's database.</summary>
    private readonly PostgresServer _server = server;

    /// <summary>The state version of a city as stored before each test: its 0, saved once.</summary>
    private const long _storedVersion = 1;

    /// <summary>The sawmill of the test cities, whose store holds logs and planks.</summary>
    private const int _sawmill = 3;

    /// <summary>The woodcutter of the test cities, whose store holds logs.</summary>
    private const int _woodcutter = 2;

    /// <summary>The test player the panel works on (OD-14).</summary>
    private static readonly Guid _devtestId = new("c0de0000-0000-4000-8000-000000000001");

    /// <summary>The test player's city.</summary>
    private static readonly Guid _devtestCityId = new("c0de0000-0000-4000-8000-0000000000c1");

    /// <summary>Another player, whose city the panel must never change.</summary>
    private static readonly Guid _otherId = new("c0de0000-0000-4000-8000-000000000002");

    /// <summary>The other player's city.</summary>
    private static readonly Guid _otherCityId = new("c0de0000-0000-4000-8000-0000000000c2");

    /// <summary>
    /// Add, remove and set on the 38 logs in the sawmill's store: the store holds the expected number, one <c>DEV_ADJUST</c> ledger row holds
    /// the change really made and ends on the city's total of logs (the sawmill's are the only ones), and the state version goes up by one.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="expected">The store's logs after.</param>
    [Theory]
    [InlineData("add", 50L, 88L)]
    [InlineData("remove", 5L, 33L)]
    [InlineData("set", 7L, 7L)]
    public async Task Run_AddRemoveSetAGood_ChangesTheBuildingsStoreAndWritesOneLedgerRow(string mode, long amount, long expected)
    {
        await StoreTwoCitiesAsync();
        Assert.Equal(38, await StockAsync(_devtestCityId, _sawmill, "logs"));

        PsqlResult result = await RunAsync(mode, "logs", amount, _sawmill);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"OK logs 38 -> {expected}", result.Stdout);
        Assert.Equal(expected, await StockAsync(_devtestCityId, _sawmill, "logs"));
        LedgerEntryRow row = Assert.Single(await LedgerAsync(Database, _devtestCityId), r => r.Reason == "DEV_ADJUST");
        Assert.Equal(("logs", expected - 38, expected, "dev-panel", mode), (row.ItemId, row.Delta, row.BalanceAfter, row.RefType, row.RefId));
        Assert.Equal(_storedVersion + 1, (await CityRowAsync(_devtestCityId)).StateVersion);
    }

    /// <summary>The currency goes to the purse whatever building is named: 600 coins become 650, and the ledger ends on the purse.</summary>
    [Fact]
    public async Task Run_AddCoins_ChangesThePurse()
    {
        await StoreTwoCitiesAsync();

        PsqlResult result = await RunAsync("add", "coins", 50, building: 0);

        Assert.Equal("OK coins 600 -> 650", result.Stdout);
        Assert.Equal(650, (await CityRowAsync(_devtestCityId)).Purse);
        LedgerEntryRow row = Assert.Single(await LedgerAsync(Database, _devtestCityId), r => r.Reason == "DEV_ADJUST");
        Assert.Equal(("coins", 50L, 650L), (row.ItemId, row.Delta, row.BalanceAfter));
    }

    /// <summary>A remove larger than the store's stock leaves 0, never a negative number, and the ledger row holds what was really removed.</summary>
    [Fact]
    public async Task Run_RemoveMoreThanTheStock_StopsAtZero()
    {
        await StoreTwoCitiesAsync();

        PsqlResult result = await RunAsync("remove", "logs", 999_999, _sawmill);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, await StockAsync(_devtestCityId, _sawmill, "logs"));
        Assert.Equal(-38, Assert.Single(await LedgerAsync(Database, _devtestCityId), r => r.Reason == "DEV_ADJUST").Delta);
    }

    /// <summary>A change that changes nothing (removing from a store that has none) leaves no ledger row.</summary>
    [Fact]
    public async Task Run_RemoveFromAnEmptyStore_WritesNoLedgerRow()
    {
        await StoreTwoCitiesAsync();

        PsqlResult result = await RunAsync("remove", "planks", 5, _sawmill);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(await LedgerAsync(Database, _devtestCityId));
    }

    /// <summary>A good the store has no row for yet is created by an add, and the ledger ends on the city's total of it.</summary>
    [Fact]
    public async Task Run_AddAGoodTheStoreHasNot_CreatesTheRow()
    {
        await StoreTwoCitiesAsync();

        await RunAsync("add", "planks", 12, _woodcutter);

        Assert.Equal(12, await StockAsync(_devtestCityId, _woodcutter, "planks"));
        Assert.Equal(12, (await LedgerAsync(Database, _devtestCityId)).Last(r => r.ItemId == "planks").BalanceAfter);
    }

    /// <summary>The other player's purse, stores, ledger and version stay as they were after every mode.</summary>
    [Fact]
    public async Task Run_AnyMode_LeavesTheOtherPlayersCityAlone()
    {
        await StoreTwoCitiesAsync();
        string stockBefore = await StockTextAsync(_otherCityId);
        long versionBefore = (await CityRowAsync(_otherCityId)).StateVersion;

        await RunAsync("add", "logs", 100, _sawmill);
        await RunAsync("remove", "coins", 10, 0);
        await RunAsync("set", "planks", 1, _sawmill);

        Assert.Equal(stockBefore, await StockTextAsync(_otherCityId));
        Assert.Equal(versionBefore, (await CityRowAsync(_otherCityId)).StateVersion);
        Assert.Empty(await LedgerAsync(Database, _otherCityId));
    }

    /// <summary><c>show</c> prints the purse and the stores and writes nothing.</summary>
    [Fact]
    public async Task Run_Show_PrintsThePurseAndTheStoresAndChangesNothing()
    {
        await StoreTwoCitiesAsync();

        PsqlResult result = await RunAsync("show", "", 0, 0);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("purse", result.Stdout);
        Assert.Contains("building 3 sawmill", result.Stdout);
        Assert.EndsWith("OK show", result.Stdout);
        Assert.Equal(_storedVersion, (await CityRowAsync(_devtestCityId)).StateVersion);
        Assert.Empty(await LedgerAsync(Database, _devtestCityId));
    }

    /// <summary>
    /// A player with no city, a bad mode, amount, item id or building, and a building the city doesn't have are refused with a message, and
    /// nothing is written.
    /// </summary>
    /// <param name="player">The dev name.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="item">The item.</param>
    /// <param name="amount">The amount, as text.</param>
    /// <param name="building">The building id, as text.</param>
    /// <param name="message">The start of the expected message.</param>
    [Theory]
    [InlineData("nobody", "add", "planks", "5", "3", "REFUSED: no city for player nobody")]
    [InlineData("devtest", "give", "planks", "5", "3", "REFUSED: mode must be")]
    [InlineData("devtest", "add", "planks", "-5", "3", "REFUSED: amount must be")]
    [InlineData("devtest", "add", "planks", "5; drop table cities", "3", "REFUSED: amount must be")]
    [InlineData("devtest", "add", "planks", "1000000000000", "3", "REFUSED: amount must be")]
    [InlineData("devtest", "add", "Planks'; --", "5", "3", "REFUSED: the item is not an item id")]
    [InlineData("devtest", "add", "planks", "5", "0", "REFUSED: a good needs the id")]
    [InlineData("devtest", "add", "planks", "5", "3; drop", "REFUSED: a good needs the id")]
    [InlineData("devtest", "add", "planks", "5", "99", "REFUSED: the city has no building 99")]
    public async Task Run_BadInput_IsRefusedAndWritesNothing(string player, string mode, string item, string amount, string building, string message)
    {
        await StoreTwoCitiesAsync();
        string stockBefore = await StockTextAsync(_devtestCityId);
        (string, string)[] variables =
            [("player", player), ("mode", mode), ("item", item), ("amount", amount), ("building", building), ("currency", "coins")];

        PsqlResult result = await _server.RunPsqlFileAsync(Database, ScriptPath, variables);

        Assert.StartsWith(message, result.Stdout);
        Assert.Equal(stockBefore, await StockTextAsync(_devtestCityId));
        Assert.Equal(_storedVersion, (await CityRowAsync(_devtestCityId)).StateVersion);
        Assert.Empty(await LedgerAsync(Database, _devtestCityId));
    }

    /// <summary>
    /// After an add far above the sawmill's store limit, the server settles the city for an hour with its woodcutter and sawmill running:
    /// the store keeps the goods (limits only stop production and deliveries), and the ledger of logs still ends on the city's total.
    /// </summary>
    [Fact]
    public async Task Settle_AfterAddingAboveTheLimit_KeepsTheGoodsAndTheLedgerReconciles()
    {
        await StoreTwoCitiesAsync();
        await RunAsync("add", "logs", 5_000_000, _sawmill);
        Factory.Clock.Advance(TimeSpan.FromHours(1));

        await SettleAsync(Factory, _devtestId);

        long logs = (await StoredTotalsAsync(Database, _devtestCityId, ContentOf(Factory).Currency))["logs"];
        Assert.InRange(logs, 4_000_000, 6_000_000);
        Assert.Equal(logs, (await LedgerAsync(Database, _devtestCityId)).Last(r => r.ItemId == "logs").BalanceAfter);
    }

    /// <summary>The path of the script next to the test assembly (linked in the project file).</summary>
    private static string ScriptPath => Path.Combine(AppContext.BaseDirectory, "server-panel", "goods.sql");

    /// <summary>Stores <c>devtest</c>'s and another player's city, both <see cref="WithFrameSawRunning"/>, last settled when the test clock starts.</summary>
    /// <returns>A task that completes when both are stored.</returns>
    private async Task StoreTwoCitiesAsync()
    {
        long start = PostgresApiFactory.Start.ToUnixTimeSeconds();
        var content = ContentOf(Factory);
        await StoreAsync(Factory, new Player(_devtestId, "devtest"), WithFrameSawRunning(content, _devtestCityId, _devtestId, start));
        await StoreAsync(Factory, new Player(_otherId, "someone"), WithFrameSawRunning(content, _otherCityId, _otherId, start));
    }

    /// <summary>Runs the script for <c>devtest</c> as <c>panel.sh</c> does, with the content's currency, <c>coins</c>.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="item">The item id.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="building">The building whose store gets a good (0 for the currency and show).</param>
    /// <returns>What psql printed.</returns>
    private Task<PsqlResult> RunAsync(string mode, string item, long amount, int building) =>
        _server.RunPsqlFileAsync(
            Database,
            ScriptPath,
            ("player", "devtest"),
            ("mode", mode),
            ("item", item),
            ("amount", amount.ToString(CultureInfo.InvariantCulture)),
            ("building", building.ToString(CultureInfo.InvariantCulture)),
            ("currency", "coins"));

    /// <summary>One good's stock in one building's store; 0 when the store has no row for it.</summary>
    /// <param name="cityId">The city.</param>
    /// <param name="buildingId">The building.</param>
    /// <param name="item">The item id.</param>
    /// <returns>The quantity.</returns>
    private async Task<long> StockAsync(Guid cityId, int buildingId, string item)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        return await db.CityStores.AsNoTracking().Where(s => s.CityId == cityId && s.BuildingId == buildingId && s.ItemId == item)
            .Select(s => s.Quantity).SingleOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A city's purse and every store row as text, in building and item order.</summary>
    /// <param name="cityId">The city.</param>
    /// <returns>The purse, then one <c>building item=quantity</c> per line.</returns>
    private async Task<string> StockTextAsync(Guid cityId)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityStoreRow> rows = await db.CityStores.AsNoTracking().Where(s => s.CityId == cityId)
            .OrderBy(s => s.BuildingId).ThenBy(s => s.ItemId).ToListAsync(TestContext.Current.CancellationToken);
        return $"purse {(await CityRowAsync(cityId)).Purse}\n" + string.Join("\n", rows.Select(s => $"{s.BuildingId} {s.ItemId}={s.Quantity}"));
    }

    /// <summary>A city's stored row.</summary>
    /// <param name="cityId">The city.</param>
    /// <returns>The row.</returns>
    private async Task<CityRow> CityRowAsync(Guid cityId)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        return await db.Cities.AsNoTracking().SingleAsync(c => c.Id == cityId, TestContext.Current.CancellationToken);
    }
}
