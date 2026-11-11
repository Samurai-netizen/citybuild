namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_delivery_lines</c> (core-instructions/06-data-model.md, CR-0008 amendment 1): one line of a courier's delivery queue
/// (<c>04</c> section 18), "take at the source, put at the target". The row goes when the line ends, is cancelled, or its courier goes.
/// </summary>
public sealed class CityDeliveryLineRow
{
    /// <summary><c>city_id</c>: the city; with <see cref="StationId"/>, <see cref="Slot"/> and <see cref="LineId"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>line_id</c>: the line's id in its city, given from <see cref="CityRow.NextLineId"/> and never reused; its courier runs its lines in this order.</summary>
    public int LineId { get; set; }

    /// <summary><c>station_id</c>: the courier station of the courier that runs it (<see cref="CityCourierRow"/>).</summary>
    public int StationId { get; set; }

    /// <summary><c>slot</c>: that courier's slot, from 1.</summary>
    public short Slot { get; set; }

    /// <summary><c>source_id</c>: the building the goods are taken from.</summary>
    public int SourceId { get; set; }

    /// <summary><c>target_id</c>: the building they are put into, not the source.</summary>
    public int TargetId { get; set; }

    /// <summary><c>item_id</c>: the good's content id.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>quantity</c>: the most the line moves, above 0.</summary>
    public long Quantity { get; set; }
}
