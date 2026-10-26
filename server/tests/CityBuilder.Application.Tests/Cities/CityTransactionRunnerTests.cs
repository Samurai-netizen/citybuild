using CityBuilder.Application.Cities;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Ledger;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// The city transaction's lifecycle (<c>core-instructions/06-data-model.md</c>, write transaction pattern) over the in-memory ports: begin,
/// lock and load, work, write, commit; an exception rolls back; a request runs one transaction and saves its city once. The database side
/// (the lock itself, the rollback of written rows) is in the integration tests.
/// </summary>
public sealed class CityTransactionRunnerTests
{
    /// <summary>A transaction whose work saves the city begins, locks and loads it, saves and writes it, and commits last.</summary>
    [Fact]
    public async Task RunAsync_WorkSaves_WritesThenCommits()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);

        string result = await runner.RunAsync(PlayerId, async (transaction, cancellationToken) =>
        {
            await transaction.SaveAsync([], receipt: null, cancellationToken);
            return "done";
        }, TestContext.Current.CancellationToken);

        Assert.Equal("done", result);
        Assert.Equal(["begin", $"load {PlayerId}", "save", "write", "commit"], repository.Events);
    }

    /// <summary>The work gets the acting player's city, with the ledger the save is given passed on to the repository.</summary>
    [Fact]
    public async Task RunAsync_Work_GetsThePlayersCityAndSavesItWithItsLedger()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);
        LedgerEntry[] ledger = [new(Logs, 1, 1, LedgerReason.SettlementProduced)];

        Guid cityId = await runner.RunAsync(PlayerId, async (transaction, cancellationToken) =>
        {
            await transaction.SaveAsync(ledger, receipt: null, cancellationToken);
            return transaction.City.CityId;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(CityId, cityId);
        Assert.Same(ledger, repository.LastLedger);
    }

    /// <summary>A work that saves nothing still commits (a settle that changed nothing releases the lock); nothing is written.</summary>
    [Fact]
    public async Task RunAsync_WorkSavesNothing_CommitsWithoutWriting()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);

        await runner.RunAsync(PlayerId, (_, _) => Task.FromResult(0), TestContext.Current.CancellationToken);

        Assert.Equal(["begin", $"load {PlayerId}", "commit"], repository.Events);
    }

    /// <summary>An exception after the save rolls the transaction back instead of committing it, and reaches the caller unchanged.</summary>
    [Fact]
    public async Task RunAsync_WorkThrowsAfterSaving_RollsBackAndRethrows()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);
        var failure = new InvalidOperationException("after the save");

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync<int>(
            PlayerId,
            async (transaction, cancellationToken) =>
            {
                await transaction.SaveAsync([], receipt: null, cancellationToken);
                throw failure;
            },
            TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", $"load {PlayerId}", "save", "write", "rollback"], repository.Events);
    }

    /// <summary>A conflict reported by the write (a stale version) rolls back and reaches the caller as it is, so the API can map it.</summary>
    [Fact]
    public async Task RunAsync_WriteReportsAConflict_RollsBackAndRethrowsTheConflict()
    {
        InMemoryCityRepository repository = Repository();
        repository.FailNextWrite = new ConcurrencyConflictException();
        var runner = new CityTransactionRunner(repository, repository);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => runner.RunAsync(
            PlayerId,
            async (transaction, cancellationToken) =>
            {
                await transaction.SaveAsync([], receipt: null, cancellationToken);
                return 0;
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(["begin", $"load {PlayerId}", "save", "rollback"], repository.Events);
    }

    /// <summary>A failed load (a player without a city) rolls back without running the work.</summary>
    [Fact]
    public async Task RunAsync_PlayerWithoutCity_RollsBackWithoutRunningTheWork()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);
        var playerWithoutCity = Guid.NewGuid();
        bool ran = false;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => runner.RunAsync(
            playerWithoutCity, (_, _) => Task.FromResult(ran = true), TestContext.Current.CancellationToken));

        Assert.False(ran);
        Assert.Equal(["begin", $"load {playerWithoutCity}", "rollback"], repository.Events);
    }

    /// <summary>A second save in one transaction throws: a city is saved once per transaction, with one version step.</summary>
    [Fact]
    public async Task SaveAsync_Twice_ThrowsAndRollsBack()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(
            PlayerId,
            async (transaction, cancellationToken) =>
            {
                await transaction.SaveAsync([], receipt: null, cancellationToken);
                await transaction.SaveAsync([], receipt: null, cancellationToken);
                return 0;
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(1, repository.SaveCount);
        Assert.Equal("rollback", repository.Events[^1]);
    }

    /// <summary>The transaction handle is dead once the work has ended: saving through a kept reference throws and writes nothing.</summary>
    [Fact]
    public async Task SaveAsync_AfterTheWorkEnded_Throws()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);
        CityTransaction kept = await runner.RunAsync(PlayerId, (transaction, _) => Task.FromResult(transaction), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => kept.SaveAsync([], receipt: null, TestContext.Current.CancellationToken));

        Assert.Equal(0, repository.SaveCount);
    }

    /// <summary>A runner runs once: a second run in the same request throws before it begins a transaction or locks anything.</summary>
    [Fact]
    public async Task RunAsync_SecondRun_ThrowsBeforeLockingAnything()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);
        await runner.RunAsync(PlayerId, (_, _) => Task.FromResult(0), TestContext.Current.CancellationToken);
        int eventsAfterTheFirst = repository.Events.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(PlayerId, (_, _) => Task.FromResult(0), TestContext.Current.CancellationToken));

        Assert.Equal(eventsAfterTheFirst, repository.Events.Count);
    }

    /// <summary>
    /// A use case can't lock a second city: a run nested in the work (here for another player) throws without loading anything, and the
    /// outer transaction rolls back.
    /// </summary>
    [Fact]
    public async Task RunAsync_NestedRunForAnotherCity_ThrowsAndRollsBackTheOuter()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);
        var otherPlayer = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(
            PlayerId,
            (_, cancellationToken) => runner.RunAsync(otherPlayer, (_, _) => Task.FromResult(0), cancellationToken),
            TestContext.Current.CancellationToken));

        Assert.Equal(["begin", $"load {PlayerId}", "rollback"], repository.Events);
    }

    /// <summary>A null work throws for its parameter, before anything begins.</summary>
    [Fact]
    public async Task RunAsync_NullWork_Throws()
    {
        InMemoryCityRepository repository = Repository();
        var runner = new CityTransactionRunner(repository, repository);

        await Assert.ThrowsAsync<ArgumentNullException>(
            "work", () => runner.RunAsync<int>(PlayerId, null!, TestContext.Current.CancellationToken));

        Assert.Empty(repository.Events);
    }

    /// <summary>Each null dependency throws for its parameter.</summary>
    [Fact]
    public void Constructor_NullDependency_Throws()
    {
        var repository = new InMemoryCityRepository();

        Assert.Throws<ArgumentNullException>("cities", () => new CityTransactionRunner(null!, repository));
        Assert.Throws<ArgumentNullException>("unitOfWork", () => new CityTransactionRunner(repository, null!));
    }
}
