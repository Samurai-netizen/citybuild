namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_courier_loads</c> (core-instructions/06-data-model.md, CR-0008): one part of a courier's load, a quantity of a good and the
/// building it was taken from (<c>04</c> section 19). The row goes when the part is put or given back.
/// </summary>
public sealed class CityCourierLoadRow
{
    /// <summary><c>city_id</c>: the city; with the station, the slot, the item and the origin the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>station_id</c>: the courier's station.</summary>
    public int StationId { get; set; }

    /// <summary><c>slot</c>: the courier's slot (<see cref="CityCourierRow"/>).</summary>
    public short Slot { get; set; }

    /// <summary><c>item_id</c>: the good's content id.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>from_building_id</c>: the building it was taken from, where it goes back to.</summary>
    public int FromBuildingId { get; set; }

    /// <summary><c>quantity</c>: how many, above 0.</summary>
    public long Quantity { get; set; }
}
