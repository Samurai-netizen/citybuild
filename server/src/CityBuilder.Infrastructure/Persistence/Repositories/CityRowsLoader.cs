using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// Loads the rows of every city table for one city row (<c>core-instructions/06-data-model.md</c>), for <see cref="CityMapping.ToAggregate"/>:
/// <see cref="CityRepository"/> loads them tracked, to save the city later; <see cref="LedgerAuditSource"/> untracked, to read it only.
/// </summary>
internal static class CityRowsLoader
{
    /// <summary>The city's rows of every table.</summary>
    /// <param name="db">The context.</param>
    /// <param name="city">The city row, already loaded (tracked or not, as the caller needs).</param>
    /// <param name="tracked">Whether the context tracks the rows it loads.</param>
    /// <param name="cancellationToken">Cancels the loads.</param>
    /// <returns>The rows.</returns>
    public static async Task<CityRows> LoadAsync(CityBuilderDbContext db, CityRow city, bool tracked, CancellationToken cancellationToken)
    {
        Guid cityId = city.Id;
        return new CityRows(
            city,
            await Of(db.CityBuildings).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityStores).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityProjectNeeds).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityResearch).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityResearchNeeds).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityRoads).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityTrips).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityCouriers).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityCourierLoads).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityRouteActions).Where(row => row.CityId == cityId).ToListAsync(cancellationToken),
            await Of(db.CityDeliveryLines).Where(row => row.CityId == cityId).ToListAsync(cancellationToken));

        // The table as a query, tracked or not.
        IQueryable<TRow> Of<TRow>(DbSet<TRow> rows)
            where TRow : class => tracked ? rows : rows.AsNoTracking();
    }
}
