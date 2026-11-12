namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_trips</c> (core-instructions/06-data-model.md, CR-0008): a worker's delivery under way (<c>04</c> section 18), on its way
/// to the target with a load or back to the source for the next one. The row goes when the trip ends.
/// </summary>
public sealed class CityTripRow
{
    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>); with <see cref="TripId"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>trip_id</c>: the trip's id in its city, given from <see cref="CityRow.NextTripId"/> and never reused.</summary>
    public int TripId { get; set; }

    /// <summary><c>source_id</c>: the building the goods came from, which takes back what doesn't fit.</summary>
    public int SourceId { get; set; }

    /// <summary><c>target_id</c>: the building the goods go to.</summary>
    public int TargetId { get; set; }

    /// <summary><c>item_id</c>: the good's content id.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>quantity</c>: the load, above 0 on the way to the target, 0 on the way back.</summary>
    public long Quantity { get; set; }

    /// <summary><c>departed_at</c>: the city-clock second the worker began the current leg.</summary>
    public long DepartedAt { get; set; }

    /// <summary><c>arrives_at</c>: the city-clock second the current leg ends, after <see cref="DepartedAt"/>.</summary>
    public long ArrivesAt { get; set; }

    /// <summary><c>remaining</c>: how many of the delivery are still to be fetched from the source by later loads, at least 0 (CR-0008 D21).</summary>
    public long Remaining { get; set; }

    /// <summary><c>leg</c>: <c>ToTarget</c> or <c>ToSource</c>, as the API spells it.</summary>
    public string Leg { get; set; } = "ToTarget";
}
