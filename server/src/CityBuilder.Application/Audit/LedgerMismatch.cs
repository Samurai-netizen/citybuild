using System.Globalization;
using CityBuilder.Simulation;

namespace CityBuilder.Application.Audit;

/// <summary>What kind of disagreement <see cref="LedgerAuditor"/> found in a city.</summary>
public enum LedgerMismatchKind
{
    /// <summary>The city's first ledger row is not <c>STARTING_GRANT</c>, or the city has no ledger row: its ledger doesn't start at the purse.</summary>
    MissingStartingGrant,

    /// <summary>A row's <c>balance_after</c> is not the item's balance after the row before it (0 before the first) plus the row's delta.</summary>
    BalanceChainBroken,

    /// <summary>The sum of the item's deltas is not the item's city total (stores, walkers' loads and, for the currency, the purse).</summary>
    TotalMismatch,
}

/// <summary>One disagreement <see cref="LedgerAuditor"/> found between a city's ledger and the city.</summary>
/// <param name="CityId">The city.</param>
/// <param name="Kind">What disagrees.</param>
/// <param name="Item">The item; <see langword="null"/> for <see cref="LedgerMismatchKind.MissingStartingGrant"/>.</param>
/// <param name="EntryId">The ledger row: the broken one for <see cref="LedgerMismatchKind.BalanceChainBroken"/>, the first one for
/// <see cref="LedgerMismatchKind.MissingStartingGrant"/> (null when there is none); null for <see cref="LedgerMismatchKind.TotalMismatch"/>.</param>
/// <param name="Expected">What the ledger says: the balance the chain runs to, or the sum of the deltas; 0 for a missing grant.</param>
/// <param name="Actual">What was found: the row's <c>balance_after</c>, or the city total; 0 for a missing grant.</param>
public sealed record LedgerMismatch(Guid CityId, LedgerMismatchKind Kind, ItemId? Item, long? EntryId, long Expected, long Actual)
{
    /// <summary>One line for a person: the city, the kind, the item and row, and both numbers.</summary>
    /// <returns>The line.</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"city {CityId}: {Kind} item={Item?.Value ?? "-"} entry={EntryId?.ToString(CultureInfo.InvariantCulture) ?? "-"} ledger={Expected} actual={Actual}");
}
