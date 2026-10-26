using CityBuilder.Application.Cities;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="ICityRepository"/> over <see cref="CityBuilderDbContext"/>: loads a city's rows tracked and maps them with
/// <see cref="CityMapping"/>; <see cref="Save"/> merges the mapped rows into the tracked ones, so EF writes only what changed. Scoped, one
/// per request, like the context. The row lock (<c>SELECT … FOR UPDATE</c>) and the transaction come with M0-22.
/// </summary>
/// <param name="db">The request's context.</param>
/// <param name="timeProvider">The server clock; it stamps the wall times of the rows a save writes.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class CityRepository(CityBuilderDbContext db, TimeProvider timeProvider) : ICityRepository
{
    /// <summary>The request's context; it tracks the loaded rows until the unit of work writes them.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    /// <summary>The server clock for <c>created_at</c> and <c>updated_at</c>.</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken)
    {
        CityRow city = await _db.Cities.SingleOrDefaultAsync(row => row.PlayerId == playerId, cancellationToken)
            ?? throw new InvalidOperationException($"Player {playerId} has no city.");
        List<CityInventoryRow> inventory = await _db.CityInventory.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityBuildingRow> buildings = await _db.CityBuildings.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);
        List<CityResearchRow> research = await _db.CityResearch.Where(row => row.CityId == city.Id).ToListAsync(cancellationToken);

        return CityMapping.ToAggregate(new CityRows(city, inventory, buildings, research));
    }

    /// <inheritdoc />
    /// <remarks>
    /// A loaded city's rows are updated in place: changed values are written, new buildings, items and completed research inserted, an
    /// item the city no longer holds set to 0 (never deleted), a building or research the state no longer has deleted. A city this
    /// context doesn't track is inserted with all its rows. Wall times come from one reading of the clock.
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
            _db.CityInventory.AddRange(rows.Inventory);
            _db.CityBuildings.AddRange(rows.Buildings);
            _db.CityResearch.AddRange(rows.Research);
        }
        else
        {
            rows.City.CreatedAt = tracked.CreatedAt;
            _db.Entry(tracked).CurrentValues.SetValues(rows.City);
            MergeInventory(city.CityId, rows.Inventory);
            Merge(_db.CityBuildings, city.CityId, rows.Buildings, row => row.CityId, row => row.BuildingId);
            Merge(_db.CityResearch, city.CityId, rows.Research, row => row.CityId, row => row.ResearchId);
        }

        _db.LedgerEntries.AddRange(ledger.Select(entry => LedgerMapping.ToRow(city.CityId, entry, now)));
        if (receipt is not null)
        {
            _db.CommandReceipts.Add(LedgerMapping.ToRow(city.PlayerId, receipt, now));
        }
    }

    /// <summary>
    /// Merges the city's inventory rows into the tracked ones: changed quantities are written, new items inserted, and a tracked item missing
    /// from <paramref name="rows"/> (the engine dropped it at 0) is set to 0.
    /// </summary>
    /// <param name="cityId">The city.</param>
    /// <param name="rows">The inventory rows of the city as it is now.</param>
    private void MergeInventory(Guid cityId, IReadOnlyList<CityInventoryRow> rows)
    {
        var held = rows.Select(row => row.ItemId).ToHashSet(StringComparer.Ordinal);
        List<CityInventoryRow> usedUp = _db.CityInventory.Local
            .Where(row => row.CityId == cityId && !held.Contains(row.ItemId))
            .Select(row => new CityInventoryRow { CityId = cityId, ItemId = row.ItemId, Quantity = 0 })
            .ToList();

        Merge(_db.CityInventory, cityId, [.. rows, .. usedUp], row => row.CityId, row => row.ItemId);
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
