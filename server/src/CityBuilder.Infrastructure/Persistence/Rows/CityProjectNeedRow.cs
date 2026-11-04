namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_project_needs</c> (core-instructions/06-data-model.md, CR-0008): how much of one good a building's waiting project (a
/// construction site, an upgrade waiting for goods) still needs. No rows for a building: no project waits there.
/// </summary>
public sealed class CityProjectNeedRow
{
    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>); with <see cref="BuildingId"/> and <see cref="ItemId"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>building_id</c>: the building whose project it is (<see cref="CityBuildingRow"/>).</summary>
    public int BuildingId { get; set; }

    /// <summary><c>item_id</c>: the good's content id.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>quantity</c>: what is still missing, above 0; the row goes when it is delivered.</summary>
    public long Quantity { get; set; }
}
