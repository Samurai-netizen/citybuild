using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;

namespace CityBuilder.Application.Audit;

/// <summary>
/// One <c>ledger_entries</c> row as stored (<c>core-instructions/06-data-model.md</c>), unchecked: the audit reports a bad row instead of
/// refusing to read it, which a <see cref="LedgerEntry"/> would do.
/// </summary>
/// <param name="Id">The row's id: the order it was written in.</param>
/// <param name="Item">The item that changed.</param>
/// <param name="Delta">The change.</param>
/// <param name="BalanceAfter">The item's city total after the change, as written.</param>
/// <param name="Reason">Why it changed, or <see langword="null"/> for a reason the server never writes (<c>DEV_ADJUST</c> of the server
/// panel's goods script, CR-0006). Its delta counts like any other.</param>
public sealed record AuditedLedgerRow(long Id, ItemId Item, long Delta, long BalanceAfter, LedgerReason? Reason);
