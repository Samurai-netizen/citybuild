using CityBuilder.Application.Cities;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The settle use case end to end on PostgreSQL (<see cref="SettleCity"/> through the server's registrations, the server clock a
/// <c>FakeTimeProvider</c>): what a settle stores in <c>cities</c>, <c>city_inventory</c>, <c>city_research</c> and <c>ledger_entries</c>.
/// The cities are on the server's content and were last settled at <see cref="PostgresApiFactory.Start"/>, where the clock starts.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class SettleCityDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The state version of a city as stored before each test: its 0, saved once.</summary>
    private const long _storedVersion = 1;

    /// <summary>The production cap of the server's content (<c>maxOfflineProductionSeconds</c>): 8 hours.</summary>
    private const long _capSeconds = 8 * 3600;

    /// <summary>The first player, owner of <see cref="_cityId"/>.</summary>
    private static readonly Guid _playerId = new("5e771e00-0000-4000-8000-000000000001");

    /// <summary>The first player's city.</summary>
    private static readonly Guid _cityId = new("5e771e00-0000-4000-8000-0000000000c1");

    /// <summary>A second player, with the same city as the first where a test compares two ways of settling it.</summary>
    private static readonly Guid _otherPlayerId = new("5e771e00-0000-4000-8000-000000000002");

    /// <summary>The second player's city.</summary>
    private static readonly Guid _otherCityId = new("5e771e00-0000-4000-8000-0000000000c2");

    /// <summary>
    /// The starter city (no buildings) after 0 s, 30 s and 2 h: the clock and the last settled second move by the elapsed seconds and nothing
    /// is produced, so no ledger rows. After 0 s nothing changed: the city isn't saved and keeps its version and <c>updated_at</c>; otherwise
    /// it is saved once, at the next version.
    /// </summary>
    /// <param name="seconds">The seconds since the city was last settled.</param>
    /// <param name="saved">Whether the settle changes anything.</param>
    [Theory]
    [InlineData(0L, false)]
    [InlineData(30L, true)]
    [InlineData(2 * 3600L, true)]
    public async Task ExecuteAsync_StarterCity_StoresTheNewClockOnlyWhenTimePassed(long seconds, bool saved)
    {
        CityAggregate starter = Starter(ContentOf(Factory), _cityId, _playerId, PostgresApiFactory.Start.ToUnixTimeSeconds());
        await StoreAsync(Factory, new Player(_playerId, "settler"), starter);
        CityRow before = await CityRowAsync(_cityId);
        Factory.Clock.Advance(TimeSpan.FromSeconds(seconds));

        SettleCityResult result = await SettleAsync(Factory, _playerId);

        CityRow after = await CityRowAsync(_cityId);
        Assert.Equal(saved, result.Saved);
        Assert.Equal(saved ? _storedVersion + 1 : _storedVersion, after.StateVersion);
        Assert.Equal((seconds, before.LastSettledUnix + seconds), (after.ClockSeconds, after.LastSettledUnix));
        Assert.Equal(saved, after.UpdatedAt != before.UpdatedAt);
        Assert.Empty(await LedgerAsync(_cityId));
        Assert.Equal(Describe(starter.State.Inventory), Describe(await InventoryAsync(_cityId)));
    }

    /// <summary>
    /// A city with the 1-hour research <c>frame_saw</c> running, left alone for 30 h: the research completes at its end, clock 3600, and is
    /// stored in <c>city_research</c> with that clock; production stops after 8 h: the same city settled once after 8 h 2 min (every cycle
    /// started by the cap has finished by then; the longest takes 90 s) made and used exactly the same, ledger row by ledger row.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ThirtyHoursAwayWithAOneHourResearch_CompletesItOnTimeAndProducesOnlyForEightHours()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await StoreFrameSawCitiesAsync();
        Factory.Clock.Advance(TimeSpan.FromSeconds(_capSeconds + 120));
        SettleCityResult afterCap = await SettleAsync(Factory, _otherPlayerId);
        Factory.Clock.Advance(TimeSpan.FromHours(30) - TimeSpan.FromSeconds(_capSeconds + 120));

        SettleCityResult away = await SettleAsync(Factory, _playerId);

        Assert.Equal((30 * 3600L, _capSeconds), (away.Settlement.ElapsedSeconds, away.Settlement.ProductionSeconds));
        Assert.Equal([FrameSaw], away.Settlement.CompletedResearch);
        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(
            [(FrameSaw.Value, 3600L), (SharpAxes.Value, 0L)],
            (await db.CityResearch.Where(row => row.CityId == _cityId).OrderBy(row => row.ResearchId).ToListAsync(cancellationToken))
                .Select(row => (row.ResearchId, row.CompletedAt)));
        CityRow city = await CityRowAsync(_cityId);
        Assert.Equal((30 * 3600L, null, null, null), (city.ClockSeconds, city.ResearchId, city.ResearchStartedAt, city.ResearchEndsAt));
        Assert.NotEmpty(away.Settlement.Produced);
        Assert.Equal(Totals(await LedgerAsync(_otherCityId)), Totals(await LedgerAsync(_cityId)));
        Assert.Equal(Describe(afterCap.City.State.Inventory), Describe(away.City.State.Inventory));
    }

    /// <summary>
    /// Settled four times (10 min, 1 h, 9 h, 30 h), the city's ledger accounts for every change of its stock: per item the deltas add up to
    /// the stored quantity minus the starting one, every balance follows from the one before, and the last is the stored quantity. The rows
    /// of one settlement name it (<c>ref_type</c> <c>settlement</c>, <c>ref_id</c> its id).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SeveralSettlements_LedgerSumsEqualTheInventoryDeltas()
    {
        GameContent content = ContentOf(Factory);
        CityAggregate city = WithFrameSawRunning(content, _cityId, _playerId, PostgresApiFactory.Start.ToUnixTimeSeconds());
        var start = city.State.Inventory.ToDictionary(stock => stock.Key.Value, stock => stock.Value);
        await StoreAsync(Factory, new Player(_playerId, "settler"), city);
        var settlements = new List<Guid>();

        foreach (TimeSpan at in new[] { TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), TimeSpan.FromHours(9), TimeSpan.FromHours(30) })
        {
            Factory.Clock.Advance(at - (Factory.Clock.GetUtcNow() - PostgresApiFactory.Start));
            settlements.Add((await SettleAsync(Factory, _playerId)).Settlement.Id);
        }

        List<LedgerEntryRow> ledger = await LedgerAsync(_cityId);
        var stored = (await InventoryAsync(_cityId)).ToDictionary(row => row.ItemId, row => row.Quantity);
        Assert.NotEmpty(ledger);
        foreach (IGrouping<string, LedgerEntryRow> item in ledger.GroupBy(row => row.ItemId))
        {
            long quantity = start.GetValueOrDefault(item.Key);
            Assert.Equal(stored[item.Key] - quantity, item.Sum(row => row.Delta));
            foreach (LedgerEntryRow row in item)
            {
                quantity += row.Delta;
                Assert.Equal(quantity, row.BalanceAfter);
            }

            Assert.Equal(stored[item.Key], quantity);
        }

        Assert.All(ledger, row => Assert.Equal(Settlement.LedgerRefType, row.RefType));
        Assert.Subset(settlements.Select(id => id.ToString()).ToHashSet(), ledger.Select(row => row.RefId!).ToHashSet());
    }

    /// <summary>
    /// Settles repeated at the same instant change nothing: after the first, the city row (version, <c>updated_at</c>), the stock and the
    /// ledger stay exactly as they are, and neither later settle saves.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RepeatedAtTheSameInstant_ChangesNothing()
    {
        await StoreFrameSawCitiesAsync();
        Factory.Clock.Advance(TimeSpan.FromHours(1));
        await SettleAsync(Factory, _playerId);
        CityRow city = await CityRowAsync(_cityId);
        string stock = Describe(await InventoryAsync(_cityId));
        int rows = (await LedgerAsync(_cityId)).Count;

        SettleCityResult second = await SettleAsync(Factory, _playerId);
        SettleCityResult third = await SettleAsync(Factory, _playerId);

        Assert.False(second.Saved);
        Assert.False(third.Saved);
        CityRow again = await CityRowAsync(_cityId);
        Assert.Equal((city.StateVersion, city.UpdatedAt, city.ClockSeconds), (again.StateVersion, again.UpdatedAt, again.ClockSeconds));
        Assert.Equal(stock, Describe(await InventoryAsync(_cityId)));
        Assert.Equal(rows, (await LedgerAsync(_cityId)).Count);
    }

    /// <summary>
    /// A settle on a restarted server continues from what the first server stored: a city settled after 1 h on one host and after 2 h on a
    /// new one ends exactly as the same city settled once after 2 h (within the cap, splitting a settle changes nothing), and its ledger
    /// totals are the same.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_AfterAHostRestart_ContinuesAsOneSettleWould()
    {
        await StoreFrameSawCitiesAsync();
        await using (var first = new PostgresApiFactory(Database))
        {
            first.Clock.Advance(TimeSpan.FromHours(1));
            await SettleAsync(first, _playerId);
        }

        await using var restarted = new PostgresApiFactory(Database);
        restarted.Clock.Advance(TimeSpan.FromHours(2));
        SettleCityResult split = await SettleAsync(restarted, _playerId);
        SettleCityResult once = await SettleAsync(restarted, _otherPlayerId);

        CityAggregate stored = await LoadAsync(restarted, _playerId);
        Assert.Equal(StateOf(once.City), StateOf(stored));
        Assert.Equal(StateOf(split.City), StateOf(stored));
        Assert.Equal(_storedVersion + 2, stored.StateVersion);
        Assert.Equal(Totals(await LedgerAsync(_otherCityId)), Totals(await LedgerAsync(_cityId)));
    }

    /// <summary>
    /// Stores the frame saw city (<see cref="DatabaseCities.WithFrameSawRunning"/>) twice: for <see cref="_playerId"/> and, the same city, for
    /// <see cref="_otherPlayerId"/>, both last settled at the clock's start.
    /// </summary>
    /// <returns>A task that completes when both are stored.</returns>
    private async Task StoreFrameSawCitiesAsync()
    {
        GameContent content = ContentOf(Factory);
        long start = PostgresApiFactory.Start.ToUnixTimeSeconds();
        await StoreAsync(Factory, new Player(_playerId, "settler"), WithFrameSawRunning(content, _cityId, _playerId, start));
        await StoreAsync(Factory, new Player(_otherPlayerId, "twin"), WithFrameSawRunning(content, _otherCityId, _otherPlayerId, start));
    }

    /// <summary>The stored row of a city, read by a context of the test's own.</summary>
    /// <param name="cityId">The city.</param>
    /// <returns>Its <c>cities</c> row.</returns>
    private async Task<CityRow> CityRowAsync(Guid cityId)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        return await db.Cities.AsNoTracking().SingleAsync(row => row.Id == cityId, TestContext.Current.CancellationToken);
    }

    /// <summary>The stored stock of a city, by item id.</summary>
    /// <param name="cityId">The city.</param>
    /// <returns>Its <c>city_inventory</c> rows in ordinal item order.</returns>
    private async Task<List<CityInventoryRow>> InventoryAsync(Guid cityId)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        List<CityInventoryRow> rows = await db.CityInventory.AsNoTracking().Where(row => row.CityId == cityId)
            .ToListAsync(TestContext.Current.CancellationToken);
        return [.. rows.OrderBy(row => row.ItemId, StringComparer.Ordinal)];
    }

    /// <summary>The stored ledger rows of a city, in the order they were written.</summary>
    /// <param name="cityId">The city.</param>
    /// <returns>Its <c>ledger_entries</c> rows by ascending id.</returns>
    private async Task<List<LedgerEntryRow>> LedgerAsync(Guid cityId)
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        return await db.LedgerEntries.AsNoTracking().Where(row => row.CityId == cityId).OrderBy(row => row.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A ledger's total delta per item and reason, as text in ordinal order, so two cities' ledgers compare in one assertion.</summary>
    /// <param name="ledger">The rows.</param>
    /// <returns>One line per item and reason.</returns>
    private static string Totals(IEnumerable<LedgerEntryRow> ledger) => string.Join("\n", ledger
        .GroupBy(row => (row.ItemId, row.Reason))
        .OrderBy(group => group.Key.ItemId, StringComparer.Ordinal).ThenBy(group => group.Key.Reason, StringComparer.Ordinal)
        .Select(group => $"{group.Key.ItemId} {group.Key.Reason} {group.Sum(row => row.Delta)}"));

    /// <summary>A stock as text, one item per line, with 0 for an item stored at 0 or left out.</summary>
    /// <param name="stock">The quantities by item.</param>
    /// <returns>The items above 0 in ordinal order with their quantities.</returns>
    private static string Describe(IEnumerable<KeyValuePair<ItemId, long>> stock) =>
        string.Join(", ", stock.Where(line => line.Value > 0).OrderBy(line => line.Key).Select(line => $"{line.Key}={line.Value}"));

    /// <summary>Stored stock rows as text, in the form of <see cref="Describe(IEnumerable{KeyValuePair{ItemId, long}})"/>.</summary>
    /// <param name="rows">The <c>city_inventory</c> rows.</param>
    /// <returns>The items above 0 in ordinal order with their quantities.</returns>
    private static string Describe(IEnumerable<CityInventoryRow> rows) =>
        Describe(rows.Select(row => new KeyValuePair<ItemId, long>(new ItemId(row.ItemId), row.Quantity)));

    /// <summary>
    /// A city's settled state as text, without the ids and the version that tell two players' cities apart: the last settled second and
    /// everything <see cref="SampleCities.Describe"/> lists after its first line.
    /// </summary>
    /// <param name="city">The city.</param>
    /// <returns>The text.</returns>
    private static string StateOf(CityAggregate city) =>
        $"settled {city.LastSettledUnixSec} content {city.ContentVersion}\n" + string.Join("\n", SampleCities.Describe(city).Split('\n').Skip(1));
}
