using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Players;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CityBuilder.IntegrationTests.Persistence.SampleCities;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The server's repositories and unit of work on PostgreSQL: a city written in one request scope (one context) is what a later scope, or a
/// context of the test's own, reads back, in every table; a loaded city's changes merge; a stale state version is refused. A city is loaded
/// for update only inside a transaction (M0-22), so the loads here begin one; the transaction runner itself is tested in
/// <see cref="CityTransactionDatabaseTests"/>.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CityRepositoryDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The request id of the sample command.</summary>
    private static readonly Guid _requestId = new("a3c5e000-0000-4000-8000-000000000001");

    /// <summary>
    /// The starter city of the real content (its era and stock, clock 0, no buildings or research), saved for a new player with its
    /// starting grants, loads in a new scope with every value equal and state version 1; the ledger holds one grant per item.
    /// </summary>
    [Fact]
    public async Task LoadForUpdateAsync_SavedStarterCity_IsTheStarterCity()
    {
        GameContent content = Factory.Services.GetRequiredService<IContentProvider>().Content;
        var starter = new CityAggregate(
            CityId, PlayerId, CityFactory.CreateStarterCity(content), [], Factory.Clock.GetUtcNow().ToUnixTimeSeconds(), 0, content.ContentVersion);
        LedgerEntry[] grants = [.. starter.State.Inventory.Select(stock => new LedgerEntry(stock.Key, stock.Value, stock.Value, LedgerReason.StartingGrant))];
        await AddPlayerAsync();

        await SaveAsync(starter, grants, receipt: null);

        CityAggregate loaded = await LoadAsync();
        Assert.Equal(Describe(Saved(starter)), Describe(loaded));
        Assert.NotEmpty(loaded.State.Inventory);
        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(
            starter.State.Inventory.Select(stock => (stock.Key.ToString(), stock.Value, stock.Value, "STARTING_GRANT")),
            (await db.LedgerEntries.OrderBy(row => row.Id).ToListAsync(TestContext.Current.CancellationToken))
                .Select(row => (row.ItemId, row.Delta, row.BalanceAfter, row.Reason)));
    }

    /// <summary>
    /// The full sample city (every phase, a frozen cycle, completed and running research, era 2) loads in a new scope with every value
    /// equal and the next state version.
    /// </summary>
    [Fact]
    public async Task LoadForUpdateAsync_SavedFullCity_IsTheSameCity()
    {
        await AddPlayerAsync();

        await SaveAsync(Full(), [], receipt: null);

        Assert.Equal(Describe(Saved(Full())), Describe(await LoadAsync()));
    }

    /// <summary>
    /// A save writes every table: a context of the test's own, outside the server, reads the city row with the server clock as both wall
    /// times, the inventory, building and research rows, the ledger entries in order and the receipt.
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_FullCityWithLedgerAndReceipt_WritesEveryTable()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTimeOffset now = Factory.Clock.GetUtcNow();
        byte[] hash = Enumerable.Repeat((byte)7, CommandReceipt.PayloadHashLength).ToArray();
        LedgerEntry[] ledger =
        [
            new(new ItemId("coins"), -10, 250, LedgerReason.CostPlace, "command", _requestId.ToString()),
            new(new ItemId("logs"), 7, 7, LedgerReason.SettlementProduced),
        ];
        await AddPlayerAsync();

        // jsonb stores the response in its own form (one space after a colon); this one is already in that form.
        await SaveAsync(Full(), ledger, new CommandReceipt(_requestId, "place-building", hash, "{\"ok\": true}"));

        await using CityBuilderDbContext db = Database.CreateContext();
        CityRow city = await db.Cities.SingleAsync(cancellationToken);
        Assert.Equal((CityId, PlayerId, 8L, now, now), (city.Id, city.PlayerId, city.StateVersion, city.CreatedAt, city.UpdatedAt));
        Assert.Equal(2, await db.CityInventory.CountAsync(cancellationToken));
        Assert.Equal(6, await db.CityBuildings.CountAsync(cancellationToken));
        Assert.Equal(["masonry"], await db.CityResearch.Select(row => row.ResearchId).ToListAsync(cancellationToken));
        Assert.Equal(
            [
                (CityId, "coins", -10L, 250L, "COST_PLACE", (string?)"command", (string?)_requestId.ToString(), now),
                (CityId, "logs", 7L, 7L, "SETTLEMENT_PRODUCED", null, null, now),
            ],
            (await db.LedgerEntries.OrderBy(row => row.Id).ToListAsync(cancellationToken))
                .Select(row => (row.CityId, row.ItemId, row.Delta, row.BalanceAfter, row.Reason, row.RefType, row.RefId, row.CreatedAt)));
        CommandReceiptRow receipt = await db.CommandReceipts.SingleAsync(cancellationToken);
        Assert.Equal(
            (PlayerId, _requestId, "place-building", "{\"ok\": true}", now),
            (receipt.PlayerId, receipt.RequestId, receipt.CommandType, receipt.ResponseJson, receipt.CreatedAt));
        Assert.Equal(hash, receipt.PayloadHash);
    }

    /// <summary>
    /// A loaded city saved with changes is the changed city on the next load: a changed and a new item, a finished cycle, a deleted and a new
    /// building, a newly completed research, the running research gone; the used-up item stays as a row with quantity 0.
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_LoadedCityChanged_MergesEveryTable()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await AddPlayerAsync();
        await SaveAsync(Full(), [], receipt: null);

        await InTransactionAsync(async services =>
        {
            CityAggregate loaded = await services.GetRequiredService<ICityRepository>().LoadForUpdateAsync(PlayerId, cancellationToken);
            services.GetRequiredService<ICityRepository>().Save(Later(loaded.StateVersion), [], receipt: null);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        });

        Assert.Equal(Describe(Saved(Later(stateVersion: 8))), Describe(await LoadAsync()));
        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(0L, (await db.CityInventory.SingleAsync(row => row.ItemId == "logs", cancellationToken)).Quantity);
        Assert.False(await db.CityBuildings.AnyAsync(row => row.BuildingId == IdleId, cancellationToken));
    }

    /// <summary>
    /// Of two scopes that loaded the same version, the first save wins and the second fails with a concurrency conflict whose inner exception
    /// is EF's (<c>state_version</c> is the concurrency token, <c>06</c>); the stored city is the first save's. The row lock keeps two
    /// transactions from holding the same version, so the stale scope loads first and lets its transaction (and lock) go before the first
    /// save, as a write path without the lock (a bug) would.
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_StaleStateVersion_ThrowsAndKeepsTheFirstSave()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await AddPlayerAsync();
        await SaveAsync(Full(), [], receipt: null);
        await using AsyncServiceScope stale = Factory.Services.CreateAsyncScope();
        CityAggregate staleLoad;
        await using (await stale.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginTransactionAsync(cancellationToken))
        {
            staleLoad = await stale.ServiceProvider.GetRequiredService<ICityRepository>().LoadForUpdateAsync(PlayerId, cancellationToken);
        }

        await InTransactionAsync(async first =>
        {
            CityAggregate firstLoad = await first.GetRequiredService<ICityRepository>().LoadForUpdateAsync(PlayerId, cancellationToken);
            first.GetRequiredService<ICityRepository>().Save(Later(firstLoad.StateVersion), [], receipt: null);
            await first.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        });
        await using IUnitOfWorkTransaction staleTransaction =
            await stale.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginTransactionAsync(cancellationToken);
        stale.ServiceProvider.GetRequiredService<ICityRepository>().Save(staleLoad, [], receipt: null);

        ConcurrencyConflictException conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => stale.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));

        Assert.IsType<DbUpdateConcurrencyException>(conflict.InnerException);
        Assert.Equal(Describe(Saved(Later(stateVersion: 8))), Describe(await LoadAsync()));
    }

    /// <summary>Adds the sample cities' owner as a player, in a scope of its own.</summary>
    private Task AddPlayerAsync() => InScopeAsync(async services =>
    {
        services.GetRequiredService<IPlayerRepository>().Add(new Player(PlayerId, "dev1"));
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
    });

    /// <summary>Saves a city through the server's repository and unit of work, in a scope of its own.</summary>
    /// <param name="city">The city.</param>
    /// <param name="ledger">The ledger entries of the save.</param>
    /// <param name="receipt">The receipt of the save, if any.</param>
    private Task SaveAsync(CityAggregate city, IReadOnlyList<LedgerEntry> ledger, CommandReceipt? receipt) => InScopeAsync(async services =>
    {
        services.GetRequiredService<ICityRepository>().Save(city, ledger, receipt);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
    });

    /// <summary>
    /// Loads the sample cities' owner's city through the server's repository, in a new scope, so with a new context, inside a transaction
    /// that is rolled back after the load.
    /// </summary>
    /// <returns>The loaded city.</returns>
    private async Task<CityAggregate> LoadAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        await using IUnitOfWorkTransaction transaction =
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginTransactionAsync(cancellationToken);
        return await scope.ServiceProvider.GetRequiredService<ICityRepository>().LoadForUpdateAsync(PlayerId, cancellationToken);
    }

    /// <summary>Runs work in a new request scope inside a transaction, committed when the work returns, as the transaction runner does.</summary>
    /// <param name="work">The work, given the scope's services.</param>
    private Task InTransactionAsync(Func<IServiceProvider, Task> work) => InScopeAsync(async services =>
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using IUnitOfWorkTransaction transaction = await services.GetRequiredService<IUnitOfWork>().BeginTransactionAsync(cancellationToken);
        await work(services);
        await transaction.CommitAsync(cancellationToken);
    });

    /// <summary>Runs work in a new request scope of the server, as one request would: its own context, repositories and unit of work.</summary>
    /// <param name="work">The work, given the scope's services.</param>
    private async Task InScopeAsync(Func<IServiceProvider, Task> work)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }
}
