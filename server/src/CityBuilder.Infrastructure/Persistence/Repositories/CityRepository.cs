using CityBuilder.Application.Cities;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="ICityRepository"/> over <see cref="CityBuilderDbContext"/>: loads a city's rows tracked and maps them with
/// <see cref="CityMapping"/>; <see cref="Save"/> merges the mapped rows into the tracked ones, so EF writes only what changed. Scoped, one
/// per request, like the context. A load locks the city's row (<c>SELECT … FOR UPDATE</c>) for the rest of the request's transaction,
/// which <see cref="Application.Cities.CityTransactionRunner"/> begins. As <see cref="INewCityRepository"/> it inserts a new player's city, the
/// one write without that lock (ADR-0009).
/// </summary>
/// <param name="db">The request's context.</param>
/// <param name="timeProvider">The server clock; it stamps the wall times of the rows a save writes.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class CityRepository(CityBuilderDbContext db, TimeProvider timeProvider) : ICityRepository, INewCityRepository
{
    /// <summary>The request's context; it tracks the loaded rows until the unit of work writes them.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    /// <summary>The server clock for <c>created_at</c> and <c>updated_at</c>.</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is null)
        {
            // Outside a transaction the lock would end with the statement, and the write after it would be unguarded.
            throw new InvalidOperationException("A city is loaded for update only inside the request's transaction (ICityTransactionRunner).");
        }

        if (_db.Cities.Local.Count > 0)
        {
            throw new InvalidOperationException("This request has already loaded a city: a request locks only the acting player's city, once.");
        }

        CityRow city = await LockAsync(playerId, cancellationToken) ?? throw new CityNotFoundException($"Player {playerId} has no city.");
        List<CityBuildingRow> buildings = await _db.CityBuildings.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityStoreRow> stores = await _db.CityStores.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityProjectNeedRow> projectNeeds = await _db.CityProjectNeeds.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityResearchRow> research = await _db.CityResearch.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityResearchNeedRow> researchNeeds = await _db.CityResearchNeeds.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityRoadRow> roads = await _db.CityRoads.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityTripRow> trips = await _db.CityTrips.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityCourierRow> couriers = await _db.CityCouriers.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityCourierLoadRow> courierLoads = await _db.CityCourierLoads.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityRouteActionRow> routeActions = await _db.CityRouteActions.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);

        return CityMapping.ToAggregate(
            new CityRows(city, buildings, stores, projectNeeds, research, researchNeeds, roads, trips, couriers, courierLoads, routeActions));
    }

    /// <summary>
    /// Locks the player's city row (<c>SELECT … FOR UPDATE</c>), waiting at most the transaction's lock timeout, and loads it tracked. The
    /// rows of the city's other tables need no lock of their own: every write to them holds this one.
    /// </summary>
    /// <param name="playerId">The acting player.</param>
    /// <param name="cancellationToken">Cancels the wait and the load.</param>
    /// <returns>The city row, or <see langword="null"/> when the player has no city.</returns>
    /// <exception cref="ConcurrencyConflictException">The lock wasn't granted within the lock timeout.</exception>
    private async Task<CityRow?> LockAsync(Guid playerId, CancellationToken cancellationToken)
    {
        try
        {
            // Not composed further (a SingleOrDefault would wrap it in a subquery): the statement is exactly the one of 06.
            List<CityRow> rows = await _db.Cities
                .FromSql($"SELECT * FROM cities WHERE player_id = {playerId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            return rows.SingleOrDefault();
        }
        catch (Exception exception) when (PostgresErrors.IsLockTimeout(exception))
        {
            throw new ConcurrencyConflictException(
                $"The city of player {playerId} stayed locked by another request for longer than the lock timeout.", exception);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A loaded city's rows are updated in place: changed values are written, new rows inserted, a good a store no longer holds set to 0
    /// (never deleted), and a building, need, research, road or trip the state no longer has deleted (a delivered need, a removed road, an
    /// arrived trip). A city this context doesn't track is inserted with all its rows. Wall times come from one reading of the clock.
    /// </remarks>
    public void Save(CityAggregate city, IReadOnlyList<LedgerEntry> ledger, CommandReceipt? receipt)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(ledger);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        CityRows rows = CityMapping.ToRows(city);
        rows.City.StateVersion = checked(city.StateVersion + 1);
        rows.City.UpdatedAt = now;

        CityRow? tracked = _db.Cities.Local.FirstOrDefault(row => row.Id == city.CityId);
        if (tracked is null)
        {
            rows.City.CreatedAt = now;
            _db.Cities.Add(rows.City);
            _db.CityBuildings.AddRange(rows.Buildings);
            _db.CityStores.AddRange(rows.Stores);
            _db.CityProjectNeeds.AddRange(rows.ProjectNeeds);
            _db.CityResearch.AddRange(rows.Research);
            _db.CityResearchNeeds.AddRange(rows.ResearchNeeds);
            _db.CityRoads.AddRange(rows.Roads);
            _db.CityTrips.AddRange(rows.Trips);
            _db.CityCouriers.AddRange(rows.Couriers);
            _db.CityCourierLoads.AddRange(rows.CourierLoads);
            _db.CityRouteActions.AddRange(rows.RouteActions);
        }
        else
        {
            rows.City.CreatedAt = tracked.CreatedAt;
            _db.Entry(tracked).CurrentValues.SetValues(rows.City);
            Merge(_db.CityBuildings, city.CityId, rows.Buildings, row => row.CityId, row => row.BuildingId);
            MergeStores(city.CityId, rows.Stores);
            Merge(_db.CityProjectNeeds, city.CityId, rows.ProjectNeeds, row => row.CityId, row => (row.BuildingId, row.ItemId));
            Merge(_db.CityResearch, city.CityId, rows.Research, row => row.CityId, row => row.ResearchId);
            Merge(_db.CityResearchNeeds, city.CityId, rows.ResearchNeeds, row => row.CityId, row => row.ItemId);
            Merge(_db.CityRoads, city.CityId, rows.Roads, row => row.CityId, row => (row.X, row.Y));
            Merge(_db.CityTrips, city.CityId, rows.Trips, row => row.CityId, row => row.TripId);
            Merge(_db.CityCouriers, city.CityId, rows.Couriers, row => row.CityId, row => (row.StationId, row.Slot));
            Merge(_db.CityCourierLoads, city.CityId, rows.CourierLoads, row => row.CityId, row => (row.StationId, row.Slot, row.ItemId, row.FromBuildingId));
            Merge(_db.CityRouteActions, city.CityId, rows.RouteActions, row => row.CityId, row => (row.StationId, row.Slot, row.StopIndex, row.ActionIndex));
        }

        _db.LedgerEntries.AddRange(ledger.Select(entry => LedgerMapping.ToRow(city.CityId, entry, now)));
        if (receipt is not null)
        {
            _db.CommandReceipts.Add(LedgerMapping.ToRow(city.PlayerId, receipt, now));
        }
    }

    /// <inheritdoc />
    /// <remarks>The insert of <see cref="Save"/> for a city this context doesn't track.</remarks>
    public void Add(CityAggregate city, IReadOnlyList<LedgerEntry> startingGrant)
    {
        ArgumentNullException.ThrowIfNull(city);
        if (city.StateVersion != 0 || _db.Cities.Local.Any(row => row.Id == city.CityId))
        {
            throw new InvalidOperationException($"City {city.CityId} is not new: it is loaded or staged already, or its state version isn't 0.");
        }

        Save(city, startingGrant, receipt: null);
    }

    /// <summary>
    /// Merges the city's store rows into the tracked ones: changed quantities are written, new goods inserted, and a tracked good missing
    /// from <paramref name="rows"/> (the engine dropped it at 0) is set to 0, never deleted (<c>06</c>, <c>city_stores</c>).
    /// </summary>
    /// <param name="cityId">The city.</param>
    /// <param name="rows">The store rows of the city as it is now.</param>
    private void MergeStores(Guid cityId, IReadOnlyList<CityStoreRow> rows)
    {
        var held = rows.Select(row => (row.BuildingId, row.ItemId)).ToHashSet();
        List<CityStoreRow> emptied = _db.CityStores.Local
            .Where(row => row.CityId == cityId && !held.Contains((row.BuildingId, row.ItemId)))
            .Select(row => new CityStoreRow { CityId = cityId, BuildingId = row.BuildingId, ItemId = row.ItemId, Quantity = 0 })
            .ToList();

        Merge(_db.CityStores, cityId, [.. rows, .. emptied], row => row.CityId, row => (row.BuildingId, row.ItemId));
    }

    /// <summary>
    /// Merges one child table's rows of the city into the tracked ones: a row with a tracked key gets the new values, a new key is inserted,
    /// and a tracked row whose key is missing from <paramref name="rows"/> is deleted (callers that must keep such rows handle them first).
    /// </summary>
    /// <typeparam name="TRow">The row class.</typeparam>
    /// <typeparam name="TKey">The key of a row inside its city.</typeparam>
    /// <param name="set">The table.</param>
    /// <param name="cityId">The city.</param>
    /// <param name="rows">The rows of the city as it is now.</param>
    /// <param name="cityOf">The city id of a row.</param>
    /// <param name="keyOf">The key of a row inside its city.</param>
    private void Merge<TRow, TKey>(
        DbSet<TRow> set, Guid cityId, IReadOnlyList<TRow> rows, Func<TRow, Guid> cityOf, Func<TRow, TKey> keyOf)
        where TRow : class
        where TKey : notnull
    {
        var tracked = set.Local.Where(row => cityOf(row) == cityId).ToDictionary(keyOf);
        var current = new HashSet<TKey>();
        foreach (TRow row in rows)
        {
            TKey key = keyOf(row);
            current.Add(key);
            if (tracked.TryGetValue(key, out TRow? existing))
            {
                _db.Entry(existing).CurrentValues.SetValues(row);
            }
            else
            {
                set.Add(row);
            }
        }

        foreach (KeyValuePair<TKey, TRow> stale in tracked.Where(pair => !current.Contains(pair.Key)))
        {
            set.Remove(stale.Value);
        }
    }
}
