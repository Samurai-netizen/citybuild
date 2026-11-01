using System.Globalization;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.ServerPanel;

/// <summary>
/// <c>server-panel/goods.sql</c> (CR-0006), run with the container's <c>psql</c> as <c>panel.sh goods</c> runs it: add, remove and set change one
/// item of the <c>devtest</c> city, leave one <c>DEV_ADJUST</c> ledger row that ends on the stock, bump the state version, and touch no other
/// player; a refusal writes nothing; the engine settles a city whose stock is above the storage cap without clipping it.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class GoodsPanelScriptTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The run's server, to run the script on the test's database.</summary>
    private readonly PostgresServer _server = server;

    /// <summary>The state version of a city as stored before each test: its 0, saved once.</summary>
    private const long _storedVersion = 1;

    /// <summary>The test player the panel works on (OD-14).</summary>
    private static readonly Guid _devtestId = new("c0de0000-0000-4000-8000-000000000001");

    /// <summary>The test player's city.</summary>
    private static readonly Guid _devtestCityId = new("c0de0000-0000-4000-8000-0000000000c1");

    /// <summary>Another player, whose city the panel must never change.</summary>
    private static readonly Guid _otherId = new("c0de0000-0000-4000-8000-000000000002");

    /// <summary>The other player's city.</summary>
    private static readonly Guid _otherCityId = new("c0de0000-0000-4000-8000-0000000000c2");

    /// <summary>
    /// Add, remove and set on a stock of 20 planks (the sample city after it paid for its research): the stock becomes the expected number, one <c>DEV_ADJUST</c> ledger row holds the change
    /// really made and ends on the stock, and the state version goes up by one.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="expected">The stock after.</param>
    [Theory]
    [InlineData("add", 50L, 70L)]
    [InlineData("remove", 5L, 15L)]
    [InlineData("set", 7L, 7L)]
    public async Task Run_AddRemoveSet_ChangesTheStockAndWritesOneLedgerRow(string mode, long amount, long expected)
    {
        await StoreTwoCitiesAsync();
        Assert.Equal(20, await StockAsync(_devtestCityId, "planks"));

        PsqlResult result = await RunAsync(mode, "planks", amount);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"OK planks 20 -> {expected}", result.Stdout);
        Assert.Equal(expected, await StockAsync(_devtestCityId, "planks"));
        LedgerEntryRow row = Assert.Single(await LedgerAsync(Database, _devtestCityId), r => r.Reason == "DEV_ADJUST");
        Assert.Equal(("planks", expected - 20, expected, "dev-panel", mode), (row.ItemId, row.Delta, row.BalanceAfter, row.RefType, row.RefId));
        Assert.Equal(_storedVersion + 1, (await CityRowAsync(_devtestCityId)).StateVersion);
    }

    /// <summary>A remove larger than the stock leaves 0, never a negative number, and the ledger row holds what was really removed.</summary>
    [Fact]
    public async Task Run_RemoveMoreThanTheStock_StopsAtZero()
    {
        await StoreTwoCitiesAsync();
        long before = await StockAsync(_devtestCityId, "planks");

        PsqlResult result = await RunAsync("remove", "planks", 999_999);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, await StockAsync(_devtestCityId, "planks"));
        Assert.Equal(-before, Assert.Single(await LedgerAsync(Database, _devtestCityId), r => r.Reason == "DEV_ADJUST").Delta);
    }

    /// <summary>A change that changes nothing (removing from a stock that is 0) leaves no ledger row.</summary>
    [Fact]
    public async Task Run_RemoveFromAnEmptyStock_WritesNoLedgerRow()
    {
        await StoreTwoCitiesAsync();
        await RunAsync("set", "planks", 0);
        int rows = (await LedgerAsync(Database, _devtestCityId)).Count;

        PsqlResult result = await RunAsync("remove", "planks", 5);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(rows, (await LedgerAsync(Database, _devtestCityId)).Count);
    }

    /// <summary>An item the city holds no row for yet is created by an add, and the ledger ends on its stock.</summary>
    [Fact]
    public async Task Run_AddAnItemTheCityHasNot_CreatesTheRow()
    {
        await StoreTwoCitiesAsync();

        await RunAsync("add", "stone", 12);

        Assert.Equal(12, await StockAsync(_devtestCityId, "stone"));
        Assert.Equal(12, (await LedgerAsync(Database, _devtestCityId)).Last(r => r.ItemId == "stone").BalanceAfter);
    }

    /// <summary>The other player's stock, ledger and version stay as they were after every mode.</summary>
    [Fact]
    public async Task Run_AnyMode_LeavesTheOtherPlayersCityAlone()
    {
        await StoreTwoCitiesAsync();
        string stockBefore = await StockTextAsync(_otherCityId);
        long versionBefore = (await CityRowAsync(_otherCityId)).StateVersion;

        await RunAsync("add", "logs", 100);
        await RunAsync("remove", "coins", 10);
        await RunAsync("set", "planks", 1);

        Assert.Equal(stockBefore, await StockTextAsync(_otherCityId));
        Assert.Equal(versionBefore, (await CityRowAsync(_otherCityId)).StateVersion);
        Assert.Empty(await LedgerAsync(Database, _otherCityId));
    }

    /// <summary><c>show</c> prints the stock and writes nothing.</summary>
    [Fact]
    public async Task Run_Show_PrintsTheStockAndChangesNothing()
    {
        await StoreTwoCitiesAsync();

        PsqlResult result = await RunAsync("show", "", 0);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("planks", result.Stdout);
        Assert.EndsWith("OK show", result.Stdout);
        Assert.Equal(_storedVersion, (await CityRowAsync(_devtestCityId)).StateVersion);
        Assert.Empty(await LedgerAsync(Database, _devtestCityId));
    }

    /// <summary>A player with no city, a bad mode, a bad amount and a bad item id are refused with a message, and nothing is written.</summary>
    /// <param name="player">The dev name.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="item">The item.</param>
    /// <param name="amount">The amount, as text.</param>
    /// <param name="message">The start of the expected message.</param>
    [Theory]
    [InlineData("nobody", "add", "planks", "5", "REFUSED: no city for player nobody")]
    [InlineData("devtest", "give", "planks", "5", "REFUSED: mode must be")]
    [InlineData("devtest", "add", "planks", "-5", "REFUSED: amount must be")]
    [InlineData("devtest", "add", "planks", "5; drop table cities", "REFUSED: amount must be")]
    [InlineData("devtest", "add", "planks", "1000000000000", "REFUSED: amount must be")]
    [InlineData("devtest", "add", "Planks'; --", "5", "REFUSED: the item is not an item id")]
    public async Task Run_BadInput_IsRefusedAndWritesNothing(string player, string mode, string item, string amount, string message)
    {
        await StoreTwoCitiesAsync();
        string stockBefore = await StockTextAsync(_devtestCityId);
        (string, string)[] variables = [("player", player), ("mode", mode), ("item", item), ("amount", amount)];

        PsqlResult result = await _server.RunPsqlFileAsync(Database, ScriptPath, variables);

        Assert.StartsWith(message, result.Stdout);
        Assert.Equal(stockBefore, await StockTextAsync(_devtestCityId));
        Assert.Equal(_storedVersion, (await CityRowAsync(_devtestCityId)).StateVersion);
        Assert.Empty(await LedgerAsync(Database, _devtestCityId));
    }

    /// <summary>
    /// After an add far above any storage cap, the server settles the city for an hour with its woodcutter and sawmill running: the stock is
    /// kept (caps only limit production), and the ledger of the item still ends on the stock.
    /// </summary>
    [Fact]
    public async Task Settle_AfterAddingAboveTheCap_KeepsTheGoodsAndTheLedgerReconciles()
    {
        await StoreTwoCitiesAsync();
        await RunAsync("add", "logs", 5_000_000);
        Factory.Clock.Advance(TimeSpan.FromHours(1));

        await SettleAsync(Factory, _devtestId);

        long logs = await StockAsync(_devtestCityId, "logs");
        Assert.InRange(logs, 4_000_000, 6_000_000);
        Assert.Equal(logs, (await LedgerAsync(Database, _devtestCityId)).Last(r => r.ItemId == "logs").BalanceAfter);
    }

    /// <summary>The path of the script next to the test assembly (linked in the project file).</summary>
    private static string ScriptPath => Path.Combine(AppContext.BaseDirectory, "server-panel", "goods.sql");

    /// <summary>Stores <c>devtest</c>'s and another player's city, both with a woodcutter and a sawmill, last settled when the test clock starts.</summary>
    /// <returns>A task that completes when both are stored.</returns>
    private async Task StoreTwoCitiesAsync()
    {
        long start = PostgresApiFactory.Start.ToUnixTimeSeconds();
        var content = ContentOf(Factory);
        await StoreAsync(Factory, new Player(_devtestId, "devtest"), WithFrameSawRunning(content, _devtestCityId, _devtestId, start));
        await StoreAsync(Factory, new Player(_otherId, "someone"), WithFrameSawRunning(content, _otherCityId, _otherId, start));
    }

    /// <summary>Runs the script for <c>devtest</c>.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="item">The item id.</param>
    /// <param name="amount">The amount.</param>
    /// <returns>What psql printed.</returns>
    private Task<PsqlResult> RunAsync(string mode, string item, long amount) =>
        _server.RunPsqlFileAsync(Database, ScriptPath, ("player", "devtest"), ("mode", mode), ("item", item), ("amount", amount.ToString(CultureInfo.InvariantCulture)));

    /// <summary>One item's stored stock; 0 when the city has no row for it.</summary>
    /// <param name="cityId">The city.</param>
    /// <param name="item">The item id.</param>
    /// <returns>The quantity.</returns>
    private async Task<long> StockAsync(Guid cityId, string item)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        return await db.CityInventory.AsNoTracking().Where(i => i.CityId == cityId && i.ItemId == item)
            .Select(i => i.Quantity).SingleOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A city's whole stored stock as text, in item order.</summary>
    /// <param name="cityId">The city.</param>
    /// <returns>One <c>item=quantity</c> per line.</returns>
    private async Task<string> StockTextAsync(Guid cityId)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityInventoryRow> rows = await db.CityInventory.AsNoTracking().Where(i => i.CityId == cityId)
            .OrderBy(i => i.ItemId).ToListAsync(TestContext.Current.CancellationToken);
        return string.Join("\n", rows.Select(i => $"{i.ItemId}={i.Quantity}"));
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
