namespace CityBuilder.Application.Audit;

/// <summary>
/// The read-only port <see cref="LedgerAuditor"/> reads the stored cities through (M0-64). It never locks a city for update, settles it or
/// writes anything: the audit compares what is stored, as it is stored.
/// </summary>
public interface ILedgerAuditSource
{
    /// <summary>The id of every stored city, in id order.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The city ids.</returns>
    Task<IReadOnlyList<Guid>> ListCityIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One city and its ledger rows, read from one consistent snapshot of the database, so a write that commits between the two reads can't
    /// show as a mismatch.
    /// </summary>
    /// <param name="cityId">The city.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The city and its ledger.</returns>
    /// <exception cref="Persistence.CityNotFoundException">No city has the id.</exception>
    Task<AuditedCity> LoadAsync(Guid cityId, CancellationToken cancellationToken);
}
