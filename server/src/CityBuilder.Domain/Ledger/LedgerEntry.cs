using CityBuilder.Simulation;

namespace CityBuilder.Domain.Ledger;

/// <summary>
/// One change of one item in a city (<c>core-instructions/06-data-model.md</c>, <c>ledger_entries</c>), so every resource change is
/// auditable (<c>core-instructions/02-principles.md</c>, rule 11). The city it belongs to and the wall time are set when it is stored.
/// Immutable.
/// </summary>
public sealed class LedgerEntry
{
    /// <summary>Creates the entry.</summary>
    /// <param name="item">The item that changed.</param>
    /// <param name="delta">The change, positive or negative, never 0 (a non-change is not written).</param>
    /// <param name="balanceAfter">The item's quantity after the change; neither it nor the quantity before is below 0.</param>
    /// <param name="reason">Why it changed; a defined <see cref="LedgerReason"/>.</param>
    /// <param name="refType">The kind of thing that caused it (for example a command), or <see langword="null"/> together with
    /// <paramref name="refId"/> when there is none (a starting grant).</param>
    /// <param name="refId">The id of the thing that caused it, or <see langword="null"/> together with <paramref name="refType"/>.</param>
    /// <exception cref="ArgumentException">A value breaks one of the rules above.</exception>
    public LedgerEntry(ItemId item, long delta, long balanceAfter, LedgerReason reason, string? refType = null, string? refId = null)
    {
        if (string.IsNullOrEmpty(item.Value))
        {
            throw new ArgumentException("A ledger entry needs an item.", nameof(item));
        }

        // The quantity before is balanceAfter - delta; written as a comparison so it can't overflow.
        if (delta == 0 || balanceAfter < 0 || balanceAfter < delta)
        {
            throw new ArgumentException("The delta must not be 0, and the quantity after and before the change must not be below 0.", nameof(delta));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentException("The reason is not a defined ledger reason.", nameof(reason));
        }

        bool hasReference = refType is not null || refId is not null;
        if (hasReference && (string.IsNullOrWhiteSpace(refType) || string.IsNullOrWhiteSpace(refId)))
        {
            throw new ArgumentException("The reference type and id are both set and not blank, or both null.", nameof(refType));
        }

        Item = item;
        Delta = delta;
        BalanceAfter = balanceAfter;
        Reason = reason;
        RefType = refType;
        RefId = refId;
    }

    /// <summary>The item that changed.</summary>
    public ItemId Item { get; }

    /// <summary>The change, positive or negative, never 0.</summary>
    public long Delta { get; }

    /// <summary>The item's quantity after the change, 0 or more.</summary>
    public long BalanceAfter { get; }

    /// <summary>Why it changed.</summary>
    public LedgerReason Reason { get; }

    /// <summary>The kind of thing that caused it, or <see langword="null"/> when there is none.</summary>
    public string? RefType { get; }

    /// <summary>The id of the thing that caused it, or <see langword="null"/> when there is none.</summary>
    public string? RefId { get; }
}
