using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;

namespace CityBuilder.Application.Cities;

/// <summary>
/// Writes a city change's ledger entries in the order the changes happened (<c>core-instructions/06-data-model.md</c>, <c>ledger_entries</c>):
/// each entry's <c>balance_after</c> runs on from the item's balance after the entry before it, starting at the quantity before the change.
/// A change of 0 is not written. Used by <see cref="Settlement"/> and <see cref="CitySettler"/>.
/// </summary>
internal sealed class LedgerWriter
{
    /// <summary>Each item's balance after the entries written so far.</summary>
    private readonly Dictionary<ItemId, long> _balances;
    /// <summary>The entries written so far, in order.</summary>
    private readonly List<LedgerEntry> _entries = [];

    /// <summary>Starts at the city's stock before the change.</summary>
    /// <param name="quantitiesBefore">The quantity of each item before the change; an item that is missing had 0.</param>
    public LedgerWriter(IReadOnlyDictionary<ItemId, long> quantitiesBefore)
    {
        _balances = new Dictionary<ItemId, long>(quantitiesBefore);
    }

    /// <summary>The entries written so far, in order.</summary>
    public IReadOnlyList<LedgerEntry> Entries => _entries;

    /// <summary>Writes one entry, unless <paramref name="delta"/> is 0.</summary>
    /// <param name="item">The item that changed.</param>
    /// <param name="delta">The change, positive or negative.</param>
    /// <param name="reason">Why it changed.</param>
    /// <param name="refType">The kind of thing that caused it.</param>
    /// <param name="refId">The id of the thing that caused it.</param>
    /// <exception cref="ArgumentException">The balance would drop below 0 (<see cref="LedgerEntry"/>): the changes are out of order.</exception>
    public void Add(ItemId item, long delta, LedgerReason reason, string refType, string refId)
    {
        if (delta == 0)
        {
            return;
        }

        long balance = checked(_balances.GetValueOrDefault(item) + delta);
        _entries.Add(new LedgerEntry(item, delta, balance, reason, refType, refId));
        _balances[item] = balance;
    }

    /// <summary>
    /// Writes what an advance produced and consumed: per item in ordinal order, <c>SETTLEMENT_PRODUCED</c> with the item's output, then
    /// <c>SETTLEMENT_CONSUMED</c> with its input as a negative delta. Produced comes first: an item can be consumed after it was produced in
    /// the same window, and a balance is never below 0 that way.
    /// </summary>
    /// <param name="produced">The outputs per item.</param>
    /// <param name="consumed">The inputs per item.</param>
    /// <param name="refType">The <c>ref_type</c> of the rows.</param>
    /// <param name="refId">The <c>ref_id</c> of the rows.</param>
    public void AddProduction(
        IReadOnlyDictionary<ItemId, long> produced, IReadOnlyDictionary<ItemId, long> consumed, string refType, string refId)
    {
        foreach (ItemId item in produced.Keys.Union(consumed.Keys).Order())
        {
            Add(item, produced.GetValueOrDefault(item), LedgerReason.SettlementProduced, refType, refId);
            Add(item, -consumed.GetValueOrDefault(item), LedgerReason.SettlementConsumed, refType, refId);
        }
    }
}
