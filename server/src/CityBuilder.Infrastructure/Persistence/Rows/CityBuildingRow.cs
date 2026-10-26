namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_buildings</c> (core-instructions/06-data-model.md): one placed building with its phase, workers and labor fields
/// (core-instructions/04-simulation-model.md). Reserved outputs, stall reasons and effective labor are derived on load, never stored.
/// </summary>
public sealed class CityBuildingRow
{
    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>); with <see cref="BuildingId"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>building_id</c>: the building's id inside its city.</summary>
    public int BuildingId { get; set; }

    /// <summary><c>type_id</c>: the building type's content id.</summary>
    public string TypeId { get; set; } = string.Empty;

    /// <summary><c>x</c>: the grid column, 0 or more; no two buildings of a city share a cell.</summary>
    public short X { get; set; }

    /// <summary><c>y</c>: the grid row, 0 or more.</summary>
    public short Y { get; set; }

    /// <summary><c>level</c>: the building level, 1 or more.</summary>
    public short Level { get; set; }

    /// <summary><c>assigned_workers</c>: the workers the player assigned, 0 or more (0 = paused).</summary>
    public short AssignedWorkers { get; set; }

    /// <summary><c>phase</c>: the building's phase (the engine's <c>BuildingPhase</c>) as text.</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary><c>phase_started_at</c>: the city clock second the phase started, or <see langword="null"/>.</summary>
    public long? PhaseStartedAt { get; set; }

    /// <summary><c>phase_ends_at</c>: the city clock second the phase ends, or <see langword="null"/> while the cycle is frozen.</summary>
    public long? PhaseEndsAt { get; set; }

    /// <summary><c>target_level</c>: the level an upgrade leads to, or <see langword="null"/> when none is under way.</summary>
    public short? TargetLevel { get; set; }

    /// <summary><c>cycle_labor</c>: the worker-seconds the current cycle needs, or <see langword="null"/>.</summary>
    public long? CycleLabor { get; set; }

    /// <summary><c>labor_remaining</c>: the worker-seconds still to do in the current cycle, 0 or more, or <see langword="null"/>.</summary>
    public long? LaborRemaining { get; set; }

    /// <summary><c>segment_started_at</c>: the city clock second the current labor segment started, or <see langword="null"/>.</summary>
    public long? SegmentStartedAt { get; set; }
}
