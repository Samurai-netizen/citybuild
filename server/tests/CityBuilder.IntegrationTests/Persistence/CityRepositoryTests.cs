using CityBuilder.Application.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Repositories;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.Simulation;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Persistence.SampleCities;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// What <see cref="CityRepository.Save"/> stages, read from EF's change tracker without a database: a new city is inserted, a loaded one
/// (its rows attached as if loaded) is merged row by row, and ledger entries and the receipt are inserted, all stamped with the server clock.
/// Loading needs PostgreSQL; it is tested with the database (M0-21).
/// </summary>
public sealed class CityRepositoryTests : IDisposable
{
    /// <summary>When the sample city was created, before the tests' now.</summary>
    private static readonly DateTimeOffset _createdAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The server clock's now in every test.</summary>
    private static readonly DateTimeOffset _now = new(2026, 10, 26, 18, 30, 0, TimeSpan.Zero);

    /// <summary>The context under test; it never connects.</summary>
    private readonly CityBuilderDbContext _db = PersistenceTestContext.Create();

    /// <summary>The repository under test, over <see cref="_db"/> and a clock at <see cref="_now"/>.</summary>
    private readonly CityRepository _repository;

    /// <summary>Creates the repository over the test's context.</summary>
    public CityRepositoryTests() => _repository = new CityRepository(_db, new FixedClock(_now));

    /// <summary>Disposes the context.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>
    /// A load for update outside a transaction throws before it reaches the database: without a transaction the row lock would end with the
    /// statement (M0-22).
    /// </summary>
    [Fact]
    public async Task LoadForUpdateAsync_OutsideATransaction_ThrowsWithoutQuerying()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.LoadForUpdateAsync(PlayerId, TestContext.Current.CancellationToken));

        Assert.Contains("transaction", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A city the context doesn't track is inserted with every row, the next state version and now as both wall times.</summary>
    [Fact]
    public void Save_NewCity_InsertsEveryRowWithTheNextVersionAndNow()
    {
        _repository.Save(Full(), [], receipt: null);

        CityRow city = Assert.Single(Rows<CityRow>(EntityState.Added));
        Assert.Equal((8L, _now, _now), (city.StateVersion, city.CreatedAt, city.UpdatedAt));
        Assert.Equal(2, Rows<CityInventoryRow>(EntityState.Added).Count);
        Assert.Equal(6, Rows<CityBuildingRow>(EntityState.Added).Count);
        Assert.Single(Rows<CityResearchRow>(EntityState.Added));
        Assert.All(_db.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Added, entry.State));
    }

    /// <summary>
    /// A loaded city's row is updated in place: the state version goes from the loaded one to the next (the loaded one stays the original, the
    /// concurrency check), <c>updated_at</c> becomes now, <c>created_at</c> stays.
    /// </summary>
    [Fact]
    public void Save_LoadedCity_UpdatesTheCityRowAndKeepsCreatedAt()
    {
        AttachAsLoaded(Full());

        _repository.Save(Later(), [], receipt: null);

        CityRow city = Assert.Single(Rows<CityRow>(EntityState.Modified));
        Assert.Equal(7L, _db.Entry(city).OriginalValues.GetValue<long>(nameof(CityRow.StateVersion)));
        Assert.Equal((8L, _createdAt, _now, 90_000L), (city.StateVersion, city.CreatedAt, city.UpdatedAt, city.ClockSeconds));
        Assert.Equal((null, null, null), (city.ResearchId, city.ResearchStartedAt, city.ResearchEndsAt));
    }

    /// <summary>A changed quantity is written, an item the city used up is set to 0 (never deleted), a new item is inserted.</summary>
    [Fact]
    public void Save_LoadedCity_MergesTheInventory()
    {
        AttachAsLoaded(Full());

        _repository.Save(Later(), [], receipt: null);

        Assert.Equal(
            [("coins", 240L, EntityState.Modified), ("logs", 0L, EntityState.Modified), ("planks", 3L, EntityState.Added)],
            _db.ChangeTracker.Entries<CityInventoryRow>()
                .Select(entry => (entry.Entity.ItemId, entry.Entity.Quantity, entry.State))
                .OrderBy(row => row.ItemId, StringComparer.Ordinal));
    }

    /// <summary>
    /// A changed building is written (a finished cycle clears its cycle columns), a new one inserted, one the state no longer has deleted, and
    /// the others are left alone.
    /// </summary>
    [Fact]
    public void Save_LoadedCity_MergesTheBuildings()
    {
        AttachAsLoaded(Full());

        _repository.Save(Later(), [], receipt: null);

        Dictionary<int, EntityState> states = _db.ChangeTracker.Entries<CityBuildingRow>()
            .ToDictionary(entry => entry.Entity.BuildingId, entry => entry.State);
        Assert.Equal(
            new Dictionary<int, EntityState>
            {
                [ConstructingId] = EntityState.Unchanged,
                [UpgradingId] = EntityState.Unchanged,
                [IdleId] = EntityState.Deleted,
                [ProducingId] = EntityState.Modified,
                [FrozenId] = EntityState.Unchanged,
                [StartedAtZeroId] = EntityState.Unchanged,
                [7] = EntityState.Added,
            },
            states);
        CityBuildingRow finished = Rows<CityBuildingRow>(EntityState.Modified).Single();
        Assert.Equal(("Idle", null, null, null, null, null), (finished.Phase, finished.PhaseStartedAt, finished.PhaseEndsAt, finished.CycleLabor, finished.LaborRemaining, finished.SegmentStartedAt));
    }

    /// <summary>A research completed since the load is inserted with its completion clock; one completed before is left alone.</summary>
    [Fact]
    public void Save_LoadedCity_InsertsOnlyTheNewlyCompletedResearch()
    {
        AttachAsLoaded(Full());

        _repository.Save(Later(), [], receipt: null);

        Assert.Equal(
            [("forestry", 86_700L, EntityState.Added), ("masonry", 120L, EntityState.Unchanged)],
            _db.ChangeTracker.Entries<CityResearchRow>()
                .Select(entry => (entry.Entity.ResearchId, entry.Entity.CompletedAt, entry.State))
                .OrderBy(row => row.ResearchId, StringComparer.Ordinal));
    }

    /// <summary>Saving a loaded city that didn't change writes only the city row's state version and <c>updated_at</c>.</summary>
    [Fact]
    public void Save_UnchangedLoadedCity_ModifiesOnlyTheVersionAndUpdatedAt()
    {
        AttachAsLoaded(Full());

        _repository.Save(Full(), [], receipt: null);

        Assert.All(
            _db.ChangeTracker.Entries().Where(entry => entry.Entity is not CityRow),
            entry => Assert.Equal(EntityState.Unchanged, entry.State));
        string[] modified = _db.ChangeTracker.Entries<CityRow>().Single().Properties
            .Where(property => property.IsModified)
            .Select(property => property.Metadata.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([nameof(CityRow.StateVersion), nameof(CityRow.UpdatedAt)], modified);
    }

    /// <summary>Ledger entries are inserted in order for the city, with the reason's text and now; the receipt for the player, with now.</summary>
    [Fact]
    public void Save_WithLedgerAndReceipt_InsertsThemStampedWithNow()
    {
        var requestId = new Guid("a3c5e000-0000-4000-8000-000000000001");
        byte[] hash = Enumerable.Repeat((byte)7, CommandReceipt.PayloadHashLength).ToArray();
        LedgerEntry[] ledger =
        [
            new(new ItemId("coins"), -10, 240, LedgerReason.CostPlace, "command", requestId.ToString()),
            new(new ItemId("logs"), -7, 0, LedgerReason.CostPlace, "command", requestId.ToString()),
        ];

        _repository.Save(Full(), ledger, new CommandReceipt(requestId, "place-building", hash, "{\"ok\":true}"));

        Assert.Equal(
            [
                (CityId, "coins", -10L, 240L, "COST_PLACE", "command", requestId.ToString(), _now),
                (CityId, "logs", -7L, 0L, "COST_PLACE", "command", requestId.ToString(), _now),
            ],
            Rows<LedgerEntryRow>(EntityState.Added)
                .Select(row => (row.CityId, row.ItemId, row.Delta, row.BalanceAfter, row.Reason, row.RefType, row.RefId, row.CreatedAt)));
        CommandReceiptRow receipt = Assert.Single(Rows<CommandReceiptRow>(EntityState.Added));
        Assert.Equal((PlayerId, requestId, "place-building", "{\"ok\":true}", _now), (receipt.PlayerId, receipt.RequestId, receipt.CommandType, receipt.ResponseJson, receipt.CreatedAt));
        Assert.Equal(hash, receipt.PayloadHash);
    }

    /// <summary>Without ledger entries or a receipt, none are staged.</summary>
    [Fact]
    public void Save_WithoutLedgerOrReceipt_StagesNone()
    {
        _repository.Save(Full(), [], receipt: null);

        Assert.Empty(_db.ChangeTracker.Entries<LedgerEntryRow>());
        Assert.Empty(_db.ChangeTracker.Entries<CommandReceiptRow>());
    }

    /// <summary>Attaches the city's rows unchanged, as <see cref="CityRepository.LoadForUpdateAsync"/> leaves them, created at <see cref="_createdAt"/>.</summary>
    /// <param name="city">The city as it was loaded.</param>
    private void AttachAsLoaded(CityAggregate city)
    {
        CityRows rows = CityMapping.ToRows(city);
        rows.City.CreatedAt = _createdAt;
        rows.City.UpdatedAt = _createdAt;
        _db.Attach(rows.City);
        _db.AttachRange(rows.Inventory);
        _db.AttachRange(rows.Buildings);
        _db.AttachRange(rows.Research);
    }

    /// <summary>The tracked rows of one table in one state.</summary>
    /// <typeparam name="TRow">The row class.</typeparam>
    /// <param name="state">The state.</param>
    /// <returns>The rows, in the order they were tracked.</returns>
    private List<TRow> Rows<TRow>(EntityState state)
        where TRow : class =>
        _db.ChangeTracker.Entries<TRow>().Where(entry => entry.State == state).Select(entry => entry.Entity).ToList();
}
