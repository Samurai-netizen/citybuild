using CityBuilder.Application.Cities;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// A fake <see cref="ICityRepository"/> and <see cref="IUnitOfWork"/> in one, for use case tests without a database: it keeps the cities in
/// memory, counts the saves and the writes, and records what happened in <see cref="Events"/> in order. Like the real repository it loads a
/// city only inside a transaction; there is no row to lock and nothing to roll back in memory, so a rollback is only recorded.
/// </summary>
internal sealed class InMemoryCityRepository : ICityRepository, IUnitOfWork
{
    /// <summary>The cities, by player id.</summary>
    private readonly Dictionary<Guid, CityAggregate> _cities = [];

    /// <summary>Whether a transaction is open.</summary>
    private bool _inTransaction;

    /// <summary>How often a city was staged with <see cref="Save"/>.</summary>
    public int SaveCount { get; private set; }

    /// <summary>How often the staged changes were written with <see cref="SaveChangesAsync"/>.</summary>
    public int SaveChangesCount { get; private set; }

    /// <summary>The ledger entries of the last <see cref="Save"/>, or <see langword="null"/> before the first.</summary>
    public IReadOnlyList<LedgerEntry>? LastLedger { get; private set; }

    /// <summary>The receipt of the last <see cref="Save"/>; <see langword="null"/> before the first or when it had none.</summary>
    public CommandReceipt? LastReceipt { get; private set; }

    /// <summary>
    /// What happened, in order: <c>begin</c>, <c>load &lt;player id&gt;</c>, <c>save</c> (staged), <c>write</c>, <c>commit</c>,
    /// <c>rollback</c> (disposed without a commit).
    /// </summary>
    public List<string> Events { get; } = [];

    /// <summary>When set, the next <see cref="SaveChangesAsync"/> throws it instead of writing, as a database error would.</summary>
    public Exception? FailNextWrite { get; set; }

    /// <summary>Adds a player's city before the test runs; a second city for the same player throws.</summary>
    public void Add(CityAggregate city) => _cities.Add(city.PlayerId, city);

    /// <summary>The player's city as stored, read by a test outside any transaction (nothing is locked or recorded).</summary>
    public CityAggregate Stored(Guid playerId) => _cities[playerId];

    /// <summary>
    /// The player's city, recorded as <c>load &lt;player id&gt;</c>. Outside a transaction it throws <see cref="InvalidOperationException"/>,
    /// as the real repository does; a player without a city throws <see cref="KeyNotFoundException"/>.
    /// </summary>
    public Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken)
    {
        if (!_inTransaction)
        {
            throw new InvalidOperationException("Loaded for update outside a transaction.");
        }

        Events.Add($"load {playerId}");
        return Task.FromResult(_cities[playerId]);
    }

    /// <summary>Keeps the city in place of the player's city, remembers the ledger and the receipt, and counts the save in <see cref="SaveCount"/>.</summary>
    public void Save(CityAggregate city, IReadOnlyList<LedgerEntry> ledger, CommandReceipt? receipt)
    {
        _cities[city.PlayerId] = city;
        LastLedger = ledger;
        LastReceipt = receipt;
        SaveCount++;
        Events.Add("save");
    }

    /// <summary>Counts the write in <see cref="SaveChangesCount"/> (the cities are already in memory), or throws <see cref="FailNextWrite"/>.</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (FailNextWrite is { } failure)
        {
            FailNextWrite = null;
            throw failure;
        }

        SaveChangesCount++;
        Events.Add("write");
        return Task.CompletedTask;
    }

    /// <summary>Opens a transaction, recorded as <c>begin</c>; a second one while the first is open throws, as the real unit of work does.</summary>
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_inTransaction)
        {
            throw new InvalidOperationException("The request already has a transaction.");
        }

        Events.Add("begin");
        _inTransaction = true;
        return Task.FromResult<IUnitOfWorkTransaction>(new Transaction(this));
    }

    /// <summary>The fake transaction: records its commit, or a rollback when it is disposed without one, and closes.</summary>
    /// <param name="owner">The repository whose transaction it is.</param>
    private sealed class Transaction(InMemoryCityRepository owner) : IUnitOfWorkTransaction
    {
        /// <summary>Whether <see cref="CommitAsync"/> ran.</summary>
        private bool _committed;

        /// <summary>Records <c>commit</c>.</summary>
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            _committed = true;
            owner.Events.Add("commit");
            return Task.CompletedTask;
        }

        /// <summary>Records <c>rollback</c> unless the transaction was committed, and closes it.</summary>
        public ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                owner.Events.Add("rollback");
            }

            owner._inTransaction = false;
            return ValueTask.CompletedTask;
        }
    }
}
