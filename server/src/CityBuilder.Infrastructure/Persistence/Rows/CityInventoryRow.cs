namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>A row of <c>city_inventory</c> (core-instructions/06-data-model.md): how much of one item a city holds.</summary>
public sealed class CityInventoryRow
{
    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>); with <see cref="ItemId"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>item_id</c>: the item's content id.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>quantity</c>: the amount held, never negative.</summary>
    public long Quantity { get; set; }
}
