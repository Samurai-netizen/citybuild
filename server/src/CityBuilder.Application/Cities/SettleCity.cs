using CityBuilder.Application.Content;
using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Application.Cities;

/// <summary>
/// Settles a player's city to now (<c>core-instructions/04-simulation-model.md</c>, sections 1 and 2): the server clock, floored to whole
/// seconds, gives the window from the last settled second, and the engine advances the city by it. Timers run for the whole window;
/// production starts cycles only during the first <c>MaxOfflineProductionSeconds</c> of it. A skeleton: M0-23 adds the closing
/// zero-length advance, the ledger rows and the transaction (M0-22).
/// </summary>
/// <param name="cities">The stored cities.</param>
/// <param name="content">The game content.</param>
/// <param name="timeProvider">The server clock, the only clock that drives progress.</param>
public sealed class SettleCity(ICityRepository cities, IContentProvider content, TimeProvider timeProvider)
{
    /// <summary>Settles the player's city to now and stores it.</summary>
    /// <param name="playerId">The acting player.</param>
    /// <param name="cancellationToken">Cancels loading and saving.</param>
    /// <returns>The settled city, the settlement report and the window.</returns>
    public async Task<SettleCityResult> ExecuteAsync(Guid playerId, CancellationToken cancellationToken)
    {
        CityAggregate city = await cities.LoadForUpdateAsync(playerId, cancellationToken);

        // ToUnixTimeSeconds rounds down (it divides ticks since year 1, which are never negative): 29.7 s is second 29. Each window starts
        // at the second the last one ended, so the dropped fractions are never lost, only counted by a later settlement.
        long nowUnixSec = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        SettlementWindow window = SettlementWindow.Compute(city.LastSettledUnixSec, nowUnixSec);
        SettlementReport report = Simulator.Advance(city.State, content.Content, window.ElapsedSeconds);
        city.MarkSettled(window.NewLastSettledUnixSec);

        await cities.SaveAsync(city, cancellationToken);
        return new SettleCityResult(city, report, window);
    }
}
