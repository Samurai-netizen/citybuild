using CityBuilder.Application.Content;
using CityBuilder.Application.Persistence;
using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Application.Cities;

/// <summary>
/// Settles a player's city to now (<c>core-instructions/04-simulation-model.md</c>, sections 1 and 2): the server clock, floored to whole
/// seconds, gives the window from the last settled second, and the engine advances the city by it. Timers run for the whole window;
/// production starts cycles only during the first <c>MaxOfflineProductionSeconds</c> of it. It runs in the city's transaction
/// (<see cref="ICityTransactionRunner"/>). A skeleton: M0-23 adds the closing zero-length advance and the ledger rows.
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

    /// <summary>Settles the player's city to now and stores it.</summary>
    /// <param name="playerId">The acting player.</param>
    /// <param name="cancellationToken">Cancels the settle; nothing is committed then.</param>
    /// <returns>The settled city, the settlement report and the window.</returns>
    /// <exception cref="ConcurrencyConflictException">Another request held the city's lock for too long; nothing was committed.</exception>
    public Task<SettleCityResult> ExecuteAsync(Guid playerId, CancellationToken cancellationToken) =>
        _transactions.RunAsync(playerId, SettleAsync, cancellationToken);

    /// <summary>Settles the locked city to now and saves it in its transaction.</summary>
    /// <param name="transaction">The acting player's city, locked.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The settled city, the settlement report and the window.</returns>
    private async Task<SettleCityResult> SettleAsync(CityTransaction transaction, CancellationToken cancellationToken)
    {
        CityAggregate city = transaction.City;

        // Now is read under the lock: a request that waited for it settles from where the one before it ended. ToUnixTimeSeconds rounds
        // down (it divides ticks since year 1, which are never negative): 29.7 s is second 29. Each window starts at the second the last
        // one ended, so the dropped fractions are never lost, only counted by a later settlement.
        long nowUnixSec = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        SettlementWindow window = SettlementWindow.Compute(city.LastSettledUnixSec, nowUnixSec);
        SettlementReport report = city.Advance(_content.Content, window.ElapsedSeconds);
        city.MarkSettled(window.NewLastSettledUnixSec);

        // The settlement's ledger rows come with M0-23.
        await transaction.SaveAsync([], receipt: null, cancellationToken);
        return new SettleCityResult(city, report, window);
    }
}
