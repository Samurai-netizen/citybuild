namespace CityBuilder.Application.Audit;

/// <summary>The result of a <see cref="LedgerAuditor"/> run.</summary>
/// <param name="CitiesAudited">How many cities were audited.</param>
/// <param name="Mismatches">Every mismatch found, per city in audit order, then in the order <see cref="LedgerAuditor.Audit"/> finds them.</param>
public sealed record LedgerAuditReport(int CitiesAudited, IReadOnlyList<LedgerMismatch> Mismatches)
{
    /// <summary>Whether every audited city's ledger reconciles with the city.</summary>
    public bool IsClean => Mismatches.Count == 0;
}
