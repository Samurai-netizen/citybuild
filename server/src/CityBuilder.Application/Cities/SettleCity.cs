using CityBuilder.Application.Content;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Cities;

/// <summary>
/// Settles a player's city to now (<c>core-instructions/04-simulation-model.md</c>, sections 1, 2 and 7; <c>03</c>, request lifecycle): in
/// the city's transaction (<see cref="ICityTransactionRunner"/>) the server clock, floored to whole seconds, gives the window from the last
/// settled second; the engine advances the city by it (timers for the whole window, cycle starts only during the first
/// <c>MaxOfflineProductionSeconds</c>), and the closing zero-length advance leaves it canonical. The city is saved with the settlement's
/// ledger rows (one per item and direction, <see cref="Settlement.LedgerEntries"/>) only if anything changed, so <c>state_version</c>
/// rises only then.
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
        CityAggregate city = transaction.City;
        GameContent content = _content.Content;
        long settledBefore = city.LastSettledUnixSec;
        string contentBefore = city.ContentVersion;
        var quantitiesBefore = new Dictionary<ItemId, long>(city.State.Inventory);
        BuildingSnapshot[] buildingsBefore = BuildingSnapshot.Of(city.State);

        // Now is read under the lock: a request that waited for it settles from where the one before it ended. ToUnixTimeSeconds rounds
        // down (it divides ticks since year 1, which are never negative): 29.7 s is second 29. Each window starts at the second the last
        // one ended, so the dropped fractions are never lost, only counted by a later settlement.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SettlementWindow window = SettlementWindow.Compute(settledBefore, now.ToUnixTimeSeconds());
        SettlementReport advance = city.Advance(content, window.ElapsedSeconds);
        city.MarkSettled(window.NewLastSettledUnixSec, content.ContentVersion);

        // The closing zero-length advance (04 section 7): after a window longer than the cap it restarts production at once. Commands run
        // their mutation before it (M0-26).
        SettlementReport closing = city.Advance(content, 0);
        var settlement = new Settlement(Guid.CreateVersion7(now), window, advance, closing);

        // With time elapsed the clock moved; without, only the totals or a building can show a change (a cycle without inputs starts unreported).
        bool changed = city.LastSettledUnixSec != settledBefore || city.ContentVersion != contentBefore || !settlement.IsEmpty
            || !buildingsBefore.SequenceEqual(BuildingSnapshot.Of(city.State));
        if (changed)
        {
            CheckStockAddsUp(quantitiesBefore, settlement, city.State);
            IReadOnlyList<LedgerEntry> ledger = settlement.LedgerEntries(quantitiesBefore);
            await transaction.SaveAsync(ledger, receipt: null, cancellationToken);
        }

        // Nothing changed: no save, so state_version, updated_at and the ledger stay; the runner commits the empty transaction.
        return new SettleCityResult(city, settlement, changed);
    }

    /// <summary>
    /// Checks that the settlement accounts for every change of the city's stock (<c>04</c> section 10, conservation): for every item, the
    /// quantity before plus what was produced minus what was consumed is the quantity now. The ledger rows are built from the same totals, so
    /// their sums then equal the stock's deltas and each item's last balance is its stock. A mismatch is an engine bug and must not be stored.
    /// </summary>
    /// <param name="quantitiesBefore">The stock before the settlement.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="city">The settled city.</param>
    /// <exception cref="InvalidOperationException">An item's quantity doesn't follow from the settlement.</exception>
    private static void CheckStockAddsUp(IReadOnlyDictionary<ItemId, long> quantitiesBefore, Settlement settlement, CityState city)
    {
        IEnumerable<ItemId> items = quantitiesBefore.Keys.Concat(city.Inventory.Keys).Concat(settlement.Produced.Keys).Concat(settlement.Consumed.Keys);
        foreach (ItemId item in items.Distinct().Order())
        {
            long expected = checked(quantitiesBefore.GetValueOrDefault(item) + settlement.Produced.GetValueOrDefault(item)
                - settlement.Consumed.GetValueOrDefault(item));
            if (expected != city.GetQuantity(item))
            {
                throw new InvalidOperationException(
                    $"The settlement leaves {item} at {expected}, but the city holds {city.GetQuantity(item)}.");
            }
        }
    }

    /// <summary>
    /// What an advance can change in a building, to tell whether the closing zero-length advance changed anything that its report doesn't
    /// show (a cycle without inputs starting).
    /// </summary>
    /// <param name="Id">The building's id.</param>
    /// <param name="Level">Its level.</param>
    /// <param name="AssignedWorkers">Its workers.</param>
    /// <param name="Phase">Its phase.</param>
    /// <param name="PhaseStartedAt">When its phase started.</param>
    /// <param name="PhaseEndsAt">When its phase ends, or <see langword="null"/>.</param>
    /// <param name="TargetLevel">The level an upgrade leads to.</param>
    /// <param name="CycleLabor">Its cycle's labor.</param>
    /// <param name="LaborRemaining">Its cycle's labor still to do.</param>
    /// <param name="SegmentStartedAt">When its cycle's current labor segment started.</param>
    private readonly record struct BuildingSnapshot(
        int Id,
        int Level,
        int AssignedWorkers,
        BuildingPhase Phase,
        long PhaseStartedAt,
        long? PhaseEndsAt,
        int TargetLevel,
        long CycleLabor,
        long LaborRemaining,
        long SegmentStartedAt)
    {
        /// <summary>The snapshots of a city's buildings, in the city's order (ascending id).</summary>
        /// <param name="city">The city.</param>
        /// <returns>One snapshot per building.</returns>
        public static BuildingSnapshot[] Of(CityState city) =>
            city.Buildings
                .Select(building => new BuildingSnapshot(
                    building.Id, building.Level, building.AssignedWorkers, building.Phase, building.PhaseStartedAt, building.PhaseEndsAt,
                    building.TargetLevel, building.CycleLabor, building.LaborRemaining, building.SegmentStartedAt))
                .ToArray();
    }
}
