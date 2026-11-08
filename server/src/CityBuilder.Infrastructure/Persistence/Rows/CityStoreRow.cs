namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_stores</c> (core-instructions/06-data-model.md, CR-0008): how much of one good a building's store holds. A row stays at
/// 0 once its good has been in the store, like the old inventory's rows.
/// </summary>
public sealed class CityStoreRow
{
    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>); with <see cref="BuildingId"/> and <see cref="ItemId"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>building_id</c>: the building whose store it is (<see cref="CityBuildingRow"/>).</summary>
    public int BuildingId { get; set; }

    /// <summary><c>item_id</c>: the good's content id (never the currency, which is in <see cref="CityRow.Purse"/>).</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>quantity</c>: the amount held, never negative; it may be above the store's limit after a refund or a returned load.</summary>
    public long Quantity { get; set; }
}
