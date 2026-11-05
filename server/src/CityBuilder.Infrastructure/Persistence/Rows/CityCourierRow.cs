namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_couriers</c> (core-instructions/06-data-model.md, CR-0008): one courier of a courier station's slot (<c>04</c> section 19),
/// with its state, the walk it is on and its stamina; its load is in <see cref="CityCourierLoadRow"/>. The row goes when the station loses
/// the worker.
/// </summary>
public sealed class CityCourierRow
{
    /// <summary><c>city_id</c>: the city; with <see cref="StationId"/> and <see cref="Slot"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>station_id</c>: the courier station's building id (<see cref="CityBuildingRow"/>).</summary>
    public int StationId { get; set; }

    /// <summary><c>slot</c>: the slot in the station, from 1.</summary>
    public short Slot { get; set; }

    /// <summary><c>state</c>: <c>AtStation</c>, <c>Walking</c>, <c>Returning</c> or <c>Resting</c>, as the API spells it.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary><c>to_stop</c>: the stop (1-based) it walks to while walking; 0 otherwise.</summary>
    public short ToStop { get; set; }

    /// <summary><c>departed_at</c>: the city-clock second its walk started; null when it doesn't walk.</summary>
    public long? DepartedAt { get; set; }

    /// <summary><c>arrives_at</c>: the city-clock second it arrives; null when it doesn't walk.</summary>
    public long? ArrivesAt { get; set; }

    /// <summary><c>stamina_seconds</c>: its stamina, at least 0.</summary>
    public long StaminaSeconds { get; set; }
}
