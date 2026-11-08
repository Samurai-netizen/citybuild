using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Application.Cities;

/// <summary>
/// What an advance, or a whole settlement, did to the city's totals per item (<c>core-instructions/04-simulation-model.md</c>, sections 10 and
/// 11): produced, consumed, delivered to projects, and refunded by upgrades a delivery started. Goods moved between stores change no total, so
/// they are not here. The ledger writes one row per item and kind from it (<see cref="LedgerWriter.AddSettlement"/>). Immutable.
/// </summary>
/// <param name="Produced">The outputs of the cycles that completed, per item.</param>
/// <param name="Consumed">The inputs of the cycles that started, per item.</param>
/// <param name="Delivered">The goods walkers handed to projects, per item.</param>
/// <param name="Refunded">The inputs given back when an upgrade started at its last delivery, per item.</param>
internal sealed record SettlementTotals(
    IReadOnlyDictionary<ItemId, long> Produced,
    IReadOnlyDictionary<ItemId, long> Consumed,
    IReadOnlyDictionary<ItemId, long> Delivered,
    IReadOnlyDictionary<ItemId, long> Refunded)
{
    /// <summary>The totals of one engine report.</summary>
    /// <param name="report">The report of an advance.</param>
    /// <returns>Its produced, consumed, delivered and refunded quantities.</returns>
    public static SettlementTotals Of(SettlementReport report) => new(report.Produced, report.Consumed, report.Delivered, report.Refunded);
}
