using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Infrastructure.Persistence.Rows;

namespace CityBuilder.Infrastructure.Persistence.Mapping;

/// <summary>
/// Maps the records a city change leaves, ledger entries and command receipts, to their rows (<c>core-instructions/06-data-model.md</c>),
/// explicitly. They are only ever inserted, so there is no way back.
/// </summary>
public static class LedgerMapping
{
    /// <summary>The <c>ledger_entries</c> row of an entry.</summary>
    /// <param name="cityId">The city the entry belongs to.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="createdAt">When it is recorded (UTC).</param>
    /// <returns>The row; its id is numbered by the database.</returns>
    public static LedgerEntryRow ToRow(Guid cityId, LedgerEntry entry, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new LedgerEntryRow
        {
            CityId = cityId,
            ItemId = entry.Item.Value,
            Delta = entry.Delta,
            BalanceAfter = entry.BalanceAfter,
            Reason = ReasonText(entry.Reason),
            RefType = entry.RefType,
            RefId = entry.RefId,
            CreatedAt = createdAt,
        };
    }

    /// <summary>The <c>command_receipts</c> row of a receipt.</summary>
    /// <param name="playerId">The player the receipt belongs to.</param>
    /// <param name="receipt">The receipt.</param>
    /// <param name="createdAt">When the command succeeded (UTC).</param>
    /// <returns>The row.</returns>
    public static CommandReceiptRow ToRow(Guid playerId, CommandReceipt receipt, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return new CommandReceiptRow
        {
            PlayerId = playerId,
            RequestId = receipt.RequestId,
            CommandType = receipt.CommandType,
            PayloadHash = receipt.PayloadHash.ToArray(),
            ResponseJson = receipt.ResponseJson,
            CreatedAt = createdAt,
        };
    }

    /// <summary>The <c>reason</c> text of a ledger reason, as <c>06</c> lists it.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>Its text, for example <c>STARTING_GRANT</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The reason is not defined.</exception>
    public static string ReasonText(LedgerReason reason) => reason switch
    {
        LedgerReason.StartingGrant => "STARTING_GRANT",
        LedgerReason.SettlementProduced => "SETTLEMENT_PRODUCED",
        LedgerReason.SettlementConsumed => "SETTLEMENT_CONSUMED",
        LedgerReason.CostPlace => "COST_PLACE",
        LedgerReason.CostUpgrade => "COST_UPGRADE",
        LedgerReason.RefundUpgrade => "REFUND_UPGRADE",
        LedgerReason.CostResearch => "COST_RESEARCH",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not a defined ledger reason."),
    };
}
