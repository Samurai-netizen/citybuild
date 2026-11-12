using System.Data;
using CityBuilder.Application.Audit;
using CityBuilder.Application.Persistence;
using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="ILedgerAuditSource"/> over <see cref="CityBuilderDbContext"/> (M0-64): reads untracked, without a row lock, each city in its own
/// <c>REPEATABLE READ</c> transaction (one snapshot for the city's tables and its ledger rows), and rolls it back: it never writes. A
/// request that changes the city meanwhile commits its rows and its city together, so the snapshot sees both or neither.
/// </summary>
/// <param name="db">The context; scoped, so use a scope of its own, not a request's.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class LedgerAuditSource(CityBuilderDbContext db) : ILedgerAuditSource
{
    /// <summary>The context.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListCityIdsAsync(CancellationToken cancellationToken) =>
        await _db.Cities.AsNoTracking().OrderBy(city => city.Id).Select(city => city.Id).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<AuditedCity> LoadAsync(Guid cityId, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        CityRow city = await _db.Cities.AsNoTracking().SingleOrDefaultAsync(row => row.Id == cityId, cancellationToken)
            ?? throw new CityNotFoundException($"No city has the id {cityId}.");
        CityRows rows = await CityRowsLoader.LoadAsync(_db, city, tracked: false, cancellationToken);
        List<LedgerEntryRow> ledger = await _db.LedgerEntries.AsNoTracking()
            .Where(row => row.CityId == cityId)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);

        await transaction.RollbackAsync(cancellationToken);
        return new AuditedCity(
            CityMapping.ToAggregate(rows),
            [.. ledger.Select(row => new AuditedLedgerRow(row.Id, new ItemId(row.ItemId), row.Delta, row.BalanceAfter, LedgerMapping.ReasonOf(row.Reason)))]);
    }
}
