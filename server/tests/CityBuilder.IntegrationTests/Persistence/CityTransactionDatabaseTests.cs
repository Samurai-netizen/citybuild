using CityBuilder.Application.Cities;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Domain.Players;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CityBuilder.IntegrationTests.Persistence.SampleCities;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The city transaction (<see cref="ICityTransactionRunner"/>, <c>core-instructions/06-data-model.md</c>, write transaction pattern) on
/// PostgreSQL, through the server's registrations: an exception after the writes rolls everything back; a <c>state_version</c> changed
/// without the lock is a conflict; two transactions on one city run one after the other, the second seeing what the first committed; a lock
/// wait beyond the lock timeout is a conflict; a request can't lock a second city. Each test starts with the sample city
/// (<see cref="SampleCities.Full"/>) stored once, so at state version 8.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class CityTransactionDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The state version of the sample city as stored before each test: <see cref="SampleCities.Full"/>'s 7, saved once.</summary>
    private const long _storedVersion = 8;

    /// <summary>The lock timeout of the server derived by <see cref="ShortLockTimeout"/>, in milliseconds.</summary>
    private const int _shortLockTimeoutMilliseconds = 200;

    /// <summary>A second player, with a city of their own in the tests that need one.</summary>
    private static readonly Guid _otherPlayerId = new("91a7e400-0000-4000-8000-000000000002");

    /// <summary>
    /// An exception after the city, a ledger entry and a receipt were written (visible inside the transaction) rolls all of it back: the
    /// stored city is unchanged, the ledger and the receipts are empty, and the lock is free for the next request.
    /// </summary>
    [Fact]
    public async Task RunAsync_ExceptionAfterTheWrites_RollsBackEverything()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await StoreSampleCityAsync();
        var failure = new InvalidOperationException("after the writes");
        byte[] hash = Enumerable.Repeat((byte)1, CommandReceipt.PayloadHashLength).ToArray();
        var receipt = new CommandReceipt(Guid.NewGuid(), "place-building", hash, "{\"ok\": true}");

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync<int>(
            Factory, PlayerId, async (transaction, services, ct) =>
            {
                transaction.City.MarkSettled(transaction.City.LastSettledUnixSec + 60, transaction.City.ContentVersion);
                await transaction.SaveAsync([new LedgerEntry(new ItemId("logs"), 1, 8, LedgerReason.SettlementProduced)], receipt, ct);

                // The writes happened: the transaction sees them.
                CityBuilderDbContext db = services.GetRequiredService<CityBuilderDbContext>();
                Assert.Equal(_storedVersion + 1, await db.Cities.AsNoTracking().Select(row => row.StateVersion).SingleAsync(ct));
                Assert.Equal(1, await db.LedgerEntries.CountAsync(ct));
                throw failure;
            }));

        Assert.Same(failure, thrown);
        await using CityBuilderDbContext stored = Database.CreateContext();
        var city = await stored.Cities.Select(row => new { row.StateVersion, row.LastSettledUnix }).SingleAsync(cancellationToken);
        Assert.Equal((_storedVersion, Full().LastSettledUnixSec), (city.StateVersion, city.LastSettledUnix));
        Assert.Equal(0, await stored.LedgerEntries.CountAsync(cancellationToken));
        Assert.Equal(0, await stored.CommandReceipts.CountAsync(cancellationToken));
        await using WebApplicationFactory<Program> shortTimeout = ShortLockTimeout();
        Assert.Equal(_storedVersion, await RunAsync(shortTimeout, PlayerId, (transaction, _, _) => Task.FromResult(transaction.City.StateVersion)));
    }

    /// <summary>
    /// A <c>state_version</c> changed inside the transaction behind the runner's back (as a write path without the lock would) makes the
    /// save fail with a conflict whose inner exception is EF's; everything rolls back, the change behind its back included.
    /// </summary>
    [Fact]
    public async Task RunAsync_VersionChangedWithoutTheLock_ThrowsConflictAndRollsBack()
    {
        await StoreSampleCityAsync();

        ConcurrencyConflictException conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync(
            Factory, PlayerId, async (transaction, services, ct) =>
            {
                await services.GetRequiredService<CityBuilderDbContext>().Database
                    .ExecuteSqlAsync($"UPDATE cities SET state_version = state_version + 1 WHERE id = {CityId}", ct);
                await transaction.SaveAsync([], receipt: null, ct);
                return 0;
            }));

        Assert.IsType<DbUpdateConcurrencyException>(conflict.InnerException);
        Assert.Equal(_storedVersion, await StoredVersionAsync());
    }

    /// <summary>
    /// A rolled-back transaction leaves nothing staged in the request's context: after a save that failed (a conflict), a later save in the
    /// same request writes nothing, so the failed city rows can't reach the database outside the transaction and without the lock.
    /// </summary>
    [Fact]
    public async Task RunAsync_FailedSaveRolledBack_LeavesNothingForALaterSaveToWrite()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await StoreSampleCityAsync();
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        CityBuilderDbContext db = scope.ServiceProvider.GetRequiredService<CityBuilderDbContext>();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.ServiceProvider.GetRequiredService<ICityTransactionRunner>().RunAsync(
            PlayerId,
            async (transaction, ct) =>
            {
                await db.Database.ExecuteSqlAsync($"UPDATE cities SET state_version = state_version + 1 WHERE id = {CityId}", ct);
                transaction.City.MarkSettled(transaction.City.LastSettledUnixSec + 60, transaction.City.ContentVersion);
                await transaction.SaveAsync([new LedgerEntry(new ItemId("logs"), 1, 8, LedgerReason.SettlementProduced)], receipt: null, ct);
                return 0;
            },
            cancellationToken));
        Assert.False(db.ChangeTracker.HasChanges());
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        Assert.Equal(_storedVersion, await StoredVersionAsync());
        await using CityBuilderDbContext stored = Database.CreateContext();
        Assert.Equal(0, await stored.LedgerEntries.CountAsync(cancellationToken));
    }

    /// <summary>
    /// Two transactions on one city run one after the other: while the first holds the lock, the second waits in PostgreSQL for it; once the
    /// first commits, the second gets the city as the first left it (its version and last settled second).
    /// </summary>
    [Fact]
    public async Task RunAsync_TwoTransactionsOnOneCity_TheSecondWaitsAndSeesTheFirstsCommit()
    {
        await StoreSampleCityAsync();
        var firstLocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        long firstSettled = Full().LastSettledUnixSec + 60;

        Task first = RunAsync(Factory, PlayerId, async (transaction, _, ct) =>
        {
            firstLocked.SetResult();
            await releaseFirst.Task.WaitAsync(ct);
            transaction.City.MarkSettled(firstSettled, transaction.City.ContentVersion);
            await transaction.SaveAsync([], receipt: null, ct);
            return 0;
        });
        await firstLocked.Task.WaitAsync(TestContext.Current.CancellationToken);
        Task<(long Version, long LastSettled)> second = RunAsync(
            Factory, PlayerId, (transaction, _, _) => Task.FromResult((transaction.City.StateVersion, transaction.City.LastSettledUnixSec)));

        await DatabaseLocks.WaitUntilWaitingAsync(Database, sessions: 1);
        Assert.False(second.IsCompleted);
        releaseFirst.SetResult();
        await first;

        Assert.Equal((_storedVersion + 1, firstSettled), await second);
    }

    /// <summary>
    /// A request that waits for the lock longer than the lock timeout fails with a conflict whose cause is PostgreSQL's lock timeout
    /// (<c>55P03</c>, wrapped by EF Core); the request that held the lock commits as usual.
    /// </summary>
    [Fact]
    public async Task RunAsync_LockHeldBeyondTheLockTimeout_ThrowsConflict()
    {
        await StoreSampleCityAsync();
        var firstLocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task first = RunAsync(Factory, PlayerId, async (transaction, _, ct) =>
        {
            firstLocked.SetResult();
            await releaseFirst.Task.WaitAsync(ct);
            await transaction.SaveAsync([], receipt: null, ct);
            return 0;
        });
        await firstLocked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await using WebApplicationFactory<Program> shortTimeout = ShortLockTimeout();

        ConcurrencyConflictException conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => RunAsync(shortTimeout, PlayerId, (_, _, _) => Task.FromResult(0)));

        releaseFirst.SetResult();
        await first;
        PostgresException cause = Assert.Single(Causes(conflict).OfType<PostgresException>());
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, cause.SqlState);
        Assert.Equal(_storedVersion + 1, await StoredVersionAsync());
    }

    /// <summary>The transaction runs with the configured lock timeout: 3 s by default (<c>06</c>), set for the transaction only.</summary>
    [Fact]
    public async Task RunAsync_DefaultSettings_RunsWithAThreeSecondLockTimeout()
    {
        await StoreSampleCityAsync();

        string lockTimeout = await RunAsync(Factory, PlayerId, (_, services, ct) => services.GetRequiredService<CityBuilderDbContext>().Database
            .SqlQuery<string>($"SELECT current_setting('lock_timeout') AS \"Value\"").SingleAsync(ct));

        Assert.Equal("3s", lockTimeout);
    }

    /// <summary>
    /// A use case can't lock a second city: a run nested in the work, for another player's city, throws; the outer transaction rolls back;
    /// the other city stays unlocked (a request with a short lock timeout gets it at once).
    /// </summary>
    [Fact]
    public async Task RunAsync_NestedRunForAnotherCity_ThrowsAndLeavesItUnlocked()
    {
        await StoreSampleCityAsync();
        await StoreOtherCityAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(Factory, PlayerId, async (transaction, services, ct) =>
        {
            ICityTransactionRunner sameRequest = services.GetRequiredService<ICityTransactionRunner>();
            return await sameRequest.RunAsync(_otherPlayerId, (_, _) => Task.FromResult(0), ct);
        }));

        await using WebApplicationFactory<Program> shortTimeout = ShortLockTimeout();
        Assert.Equal(_otherPlayerId, await RunAsync(shortTimeout, _otherPlayerId, (transaction, _, _) => Task.FromResult(transaction.City.PlayerId)));
    }

    /// <summary>The repository itself refuses to load a second city in one request's transaction, so a lock can't be taken around the runner.</summary>
    [Fact]
    public async Task LoadForUpdateAsync_SecondCityInOneTransaction_Throws()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await StoreSampleCityAsync();
        await StoreOtherCityAsync();
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ICityRepository cities = scope.ServiceProvider.GetRequiredService<ICityRepository>();
        await using IUnitOfWorkTransaction transaction =
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginTransactionAsync(cancellationToken);
        await cities.LoadForUpdateAsync(PlayerId, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cities.LoadForUpdateAsync(_otherPlayerId, cancellationToken));
    }

    /// <summary>A request has one transaction: beginning a second while the first is open throws.</summary>
    [Fact]
    public async Task BeginTransactionAsync_WhileOneIsOpen_Throws()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.BeginTransactionAsync(cancellationToken));
    }

    /// <summary>Stores the sample city's owner and <see cref="SampleCities.Full"/> (as state version 8), in a scope of its own.</summary>
    private Task StoreSampleCityAsync() => StoreAsync(new Player(PlayerId, "dev1"), Full());

    /// <summary>Stores <see cref="_otherPlayerId"/> with an empty city of their own, in a scope of its own.</summary>
    private Task StoreOtherCityAsync()
    {
        CityAggregate empty = Empty();
        return StoreAsync(
            new Player(_otherPlayerId, "dev2"),
            new CityAggregate(Guid.NewGuid(), _otherPlayerId, empty.State, [], empty.LastSettledUnixSec, 0, empty.ContentVersion));
    }

    /// <summary>Stores a player and their new city (an insert: no lock to take), in a scope of its own.</summary>
    /// <param name="player">The player.</param>
    /// <param name="city">The player's new city.</param>
    private Task StoreAsync(Player player, CityAggregate city) => DatabaseCities.StoreAsync(Factory, player, city);

    /// <summary>The sample city's stored state version, read by a context of the test's own.</summary>
    /// <returns>The stored version.</returns>
    private async Task<long> StoredVersionAsync()
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        return await db.Cities.Where(row => row.Id == CityId).Select(row => row.StateVersion).SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A server like <see cref="PostgresTest.Factory"/> on the same database, with a lock timeout of 200 ms.</summary>
    /// <returns>The derived server; the test disposes it.</returns>
    private WebApplicationFactory<Program> ShortLockTimeout() => DatabaseLocks.WithLockTimeout(Factory, _shortLockTimeoutMilliseconds);

    /// <summary>The exceptions an exception wraps, innermost last.</summary>
    /// <param name="exception">The outer exception.</param>
    /// <returns>Its inner exceptions, one level after the other.</returns>
    private static IEnumerable<Exception> Causes(Exception exception)
    {
        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            yield return inner;
        }
    }

    /// <summary>Runs one request's city transaction on a server: a new scope, its runner, and the work given the scope's services too.</summary>
    /// <typeparam name="TResult">What the work returns.</typeparam>
    /// <param name="factory">The server.</param>
    /// <param name="playerId">The acting player.</param>
    /// <param name="work">The work on the locked city, with the scope's services (its context shares the transaction).</param>
    /// <returns>What the work returned.</returns>
    private static async Task<TResult> RunAsync<TResult>(
        WebApplicationFactory<Program> factory, Guid playerId, Func<CityTransaction, IServiceProvider, CancellationToken, Task<TResult>> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICityTransactionRunner>().RunAsync(
            playerId, (transaction, ct) => work(transaction, scope.ServiceProvider, ct), TestContext.Current.CancellationToken);
    }
}
