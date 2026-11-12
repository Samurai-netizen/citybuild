using CityBuilder.Application.Cities;

namespace CityBuilder.Application.Audit;

/// <summary>A stored city and its ledger rows as <see cref="ILedgerAuditSource"/> read them, for <see cref="LedgerAuditor"/>.</summary>
/// <param name="City">The city as stored: not settled, not changed.</param>
/// <param name="Ledger">Every <c>ledger_entries</c> row of the city, in id order (the order they were written).</param>
public sealed record AuditedCity(CityAggregate City, IReadOnlyList<AuditedLedgerRow> Ledger);
