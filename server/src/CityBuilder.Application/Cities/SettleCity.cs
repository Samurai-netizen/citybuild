using CityBuilder.Application.Content;
using CityBuilder.Application.Persistence;

namespace CityBuilder.Application.Cities;

/// <summary>
/// Settles a player's city to now (<c>core-instructions/04-simulation-model.md</c>, sections 1, 2 and 7; <c>03</c>, request lifecycle): in
/// the city's transaction (<see cref="ICityTransactionRunner"/>) the server clock, floored to whole seconds, gives the window from the last
/// settled second; the engine advances the city by it (timers for the whole window, cycle starts only during the first
/// <c>MaxOfflineProductionSeconds</c>), and the closing zero-length advance leaves it canonical. The city is saved with the settlement's
/// ledger rows (one per item and direction, <see cref="Settlement.LedgerEntries"/>) only if anything changed, so <c>state_version</c>
/// rises only then. The steps are <see cref="CitySettler"/>'s, which the command pipeline runs as well.
/// </summary>
/// <param name="transactions">Runs the settle in the transaction that holds the city's row lock.</param>
/// <param name="content">The game content.</param>
/// <param name="timeProvider">The server clock, the only clock that drives progress.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class SettleCity(ICityTransactionRunner transactions, IContentProvider content, TimeProvider timeProvider)
{
    /// <summary>Locks, loads and stores the acting player's city.</summary>
    private readonly ICityTransactionRunner _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
    /// <summary>The game content every settle runs the engine with; the same for every request.</summary>
    private readonly IContentProvider _content = content ?? throw new ArgumentNullException(nameof(content));
    /// <summary>The server clock, read once per settle; the only clock that drives progress (principle 3).</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>Settles the player's city to now and stores it if anything changed.</summary>
    /// <param name="playerId">The acting player.</param>
    /// <param name="cancellationToken">Cancels the settle; nothing is committed then.</param>
    /// <returns>The settled city, the settlement, and whether the city was saved.</returns>
    /// <exception cref="ConcurrencyConflictException">Another request held the city's lock for too long; nothing was committed.</exception>
    /// <exception cref="InvalidOperationException">The settlement doesn't account for the city's stock (an engine bug); nothing was
    /// committed.</exception>
    public Task<SettleCityResult> ExecuteAsync(Guid playerId, CancellationToken cancellationToken) =>
        _transactions.RunAsync(playerId, SettleAsync, cancellationToken);

    /// <summary>Settles the locked city to now and, if anything changed, saves it with the settlement's ledger rows in its transaction.</summary>
    /// <param name="transaction">The acting player's city, locked.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The settled city, the settlement, and whether the city was saved.</returns>
    private async Task<SettleCityResult> SettleAsync(CityTransaction transaction, CancellationToken cancellationToken)
    {
        // Now is read under the lock: a request that waited for it settles from where the one before it ended.
        CitySettler settler = CitySettler.Begin(transaction.City, _content.Content, _timeProvider.GetUtcNow());
        Settlement settlement = settler.Close();
        bool changed = settler.Changed;
        if (changed)
        {
            await transaction.SaveAsync(settler.LedgerEntries(), receipt: null, cancellationToken);
        }

        // Nothing changed: no save, so state_version, updated_at and the ledger stay; the runner commits the empty transaction.
        return new SettleCityResult(transaction.City, settlement, changed);
    }
}
