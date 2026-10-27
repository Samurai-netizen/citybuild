using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// A fake <see cref="ICityRepository"/>, <see cref="IUnitOfWork"/> and <see cref="ICommandReceiptRepository"/> in one, for use case tests
/// without a database: it keeps the cities and the receipts in memory, counts the saves and the writes, and records what happened in
/// <see cref="Events"/> in order. Like the real repository it loads a city only inside a transaction; there is no row to lock and nothing to
/// roll back in memory, so a rollback is only recorded (and drops the staged receipts). A receipt becomes visible when its write succeeds,
/// and a write of a receipt whose key is stored already fails with <see cref="DuplicateKeyException"/>, as the primary key would.
/// </summary>
internal sealed class InMemoryCityRepository : ICityRepository, IUnitOfWork, ICommandReceiptRepository
{
    /// <summary>The cities, by player id.</summary>
    private readonly Dictionary<Guid, CityAggregate> _cities = [];

    /// <summary>The written receipts, by player and request id.</summary>
    private readonly Dictionary<(Guid PlayerId, Guid RequestId), CommandReceipt> _receipts = [];

    /// <summary>The receipts staged by <see cref="Save"/> since the last write, with their player.</summary>
    private readonly List<(Guid PlayerId, CommandReceipt Receipt)> _stagedReceipts = [];

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
    /// What happened, in order: <c>begin</c>, <c>load &lt;player id&gt;</c>, <c>find receipt</c>, <c>save</c> (staged), <c>write</c>,
    /// <c>commit</c>, <c>rollback</c> (disposed without a commit); tests add their own (a response writer's <c>respond</c>).
    /// </summary>
    public List<string> Events { get; } = [];

    /// <summary>When set, the next <see cref="SaveChangesAsync"/> throws it instead of writing, as a database error would.</summary>
    public Exception? FailNextWrite { get; set; }

    /// <summary>
    /// When set, the next <see cref="SaveChangesAsync"/> runs it first, once: what another request commits between this one's reads and its
    /// write (for example <see cref="AddReceipt"/>, the winner of a receipt race).
    /// </summary>
    public Action? BeforeNextWrite { get; set; }

    /// <summary>Stores a receipt as if a request had committed it.</summary>
    /// <param name="playerId">The player the receipt belongs to.</param>
    /// <param name="receipt">The receipt.</param>
    public void AddReceipt(Guid playerId, CommandReceipt receipt) => _receipts.Add((playerId, receipt.RequestId), receipt);

    /// <summary>The written receipt of a player's request id, or <see langword="null"/>; read by a test, not recorded.</summary>
    /// <param name="playerId">The player.</param>
    /// <param name="requestId">The request id.</param>
    /// <returns>The receipt, or <see langword="null"/>.</returns>
    public CommandReceipt? StoredReceipt(Guid playerId, Guid requestId) => _receipts.GetValueOrDefault((playerId, requestId));

    /// <summary>The written receipt of a player's request id, recorded as <c>find receipt</c>.</summary>
    public Task<CommandReceipt?> FindAsync(Guid playerId, Guid requestId, CancellationToken cancellationToken)
    {
        Events.Add("find receipt");
        return Task.FromResult(StoredReceipt(playerId, requestId));
    }

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
        if (receipt is not null)
        {
            _stagedReceipts.Add((city.PlayerId, receipt));
        }

        SaveCount++;
        Events.Add("save");
    }

    /// <summary>
    /// Runs <see cref="BeforeNextWrite"/>, then writes the staged receipts and counts the write in <see cref="SaveChangesCount"/> (the cities
    /// are already in memory); throws <see cref="FailNextWrite"/> instead, or <see cref="DuplicateKeyException"/> for a receipt whose key is
    /// stored already.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (BeforeNextWrite is { } before)
        {
            BeforeNextWrite = null;
            before();
        }

        if (FailNextWrite is { } failure)
        {
            FailNextWrite = null;
            throw failure;
        }

        if (_stagedReceipts.Any(staged => _receipts.ContainsKey((staged.PlayerId, staged.Receipt.RequestId))))
        {
            _stagedReceipts.Clear();
            throw new DuplicateKeyException("pk_command_receipts");
        }

        foreach ((Guid playerId, CommandReceipt receipt) in _stagedReceipts)
        {
            _receipts.Add((playerId, receipt.RequestId), receipt);
        }

        _stagedReceipts.Clear();

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
                owner._stagedReceipts.Clear();
            }

            owner._inTransaction = false;
            return ValueTask.CompletedTask;
        }
    }
}
