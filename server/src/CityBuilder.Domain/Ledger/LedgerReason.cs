namespace CityBuilder.Domain.Ledger;

/// <summary>
/// Why an item's quantity changed: the M0 ledger reasons of <c>core-instructions/06-data-model.md</c>. Infrastructure stores each one as
/// its text there (for example <c>STARTING_GRANT</c>). Later eras add market, mailbox and charter reasons.
/// </summary>
public enum LedgerReason
{
    /// <summary><c>STARTING_GRANT</c>: the starting inventory of a new city, so the ledger reconciles from the first row.</summary>
    StartingGrant,

    /// <summary><c>SETTLEMENT_PRODUCED</c>: the outputs of the cycles a settlement completed.</summary>
    SettlementProduced,

    /// <summary><c>SETTLEMENT_CONSUMED</c>: the inputs of the cycles a settlement started.</summary>
    SettlementConsumed,

    /// <summary><c>COST_PLACE</c>: the cost of placing a building.</summary>
    CostPlace,

    /// <summary><c>COST_UPGRADE</c>: the cost of upgrading a building.</summary>
    CostUpgrade,

    /// <summary><c>REFUND_UPGRADE</c>: the inputs given back when an upgrade cancels a running cycle.</summary>
    RefundUpgrade,

    /// <summary><c>COST_RESEARCH</c>: the cost of starting a research.</summary>
    CostResearch,
}
