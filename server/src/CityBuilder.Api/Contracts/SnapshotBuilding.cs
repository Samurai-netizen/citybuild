namespace CityBuilder.Api.Contracts;

/// <summary>
/// One placed building in <see cref="SnapshotCity.Buildings"/> (<c>core-instructions/04-simulation-model.md</c>, section 4). The timer fields are
/// city-clock seconds, and a phase leaves the ones it doesn't use at 0 (<see cref="PhaseEndsAt"/> at <see langword="null"/>). For display, a
/// producing building's progress is <c>done = cycleLabor − (laborRemaining − (clientClock − segmentStartedAt) × assignedWorkers)</c>.
/// </summary>
/// <param name="Id">The building's id in its city, JSON <c>id</c>; given in placement order from 1 and never reused.</param>
/// <param name="TypeId">The building type's id, JSON <c>typeId</c>; see <c>/content</c>.</param>
/// <param name="X">The tile column, JSON <c>x</c>.</param>
/// <param name="Y">The tile row, JSON <c>y</c>.</param>
/// <param name="Level">The level now, JSON <c>level</c>, from 1; while upgrading it is still the level below the target.</param>
/// <param name="AssignedWorkers">The workers assigned, JSON <c>assignedWorkers</c>; 0 for a house or a building under construction.</param>
/// <param name="MaxWorkers">The most workers the building's level takes, JSON <c>maxWorkers</c>; 0 for houses.</param>
/// <param name="Phase">What the building is doing, JSON <c>phase</c>: <c>Constructing</c>, <c>Upgrading</c>, <c>Idle</c> or <c>Producing</c>.</param>
/// <param name="PhaseStartedAt">When the phase (while producing, the cycle) started, JSON <c>phaseStartedAt</c>; 0 when idle and for a construction site.</param>
/// <param name="PhaseEndsAt">When the phase ends, JSON <c>phaseEndsAt</c>; <see langword="null"/> when idle, for a frozen cycle (producing with 0 workers)
/// and for a construction site, which waits for goods (CR-0008).</param>
/// <param name="CycleLabor">The running cycle's labor in worker-seconds, JSON <c>cycleLabor</c>, after research reductions; 0 unless producing.</param>
/// <param name="LaborRemaining">The labor left at <see cref="SegmentStartedAt"/>, JSON <c>laborRemaining</c>; 0 unless producing.</param>
/// <param name="SegmentStartedAt">When the worker count last changed in the running cycle, JSON <c>segmentStartedAt</c>; 0 unless producing.</param>
/// <param name="StallReason">Why the building makes no progress, JSON <c>stallReason</c>: <see langword="null"/>, <c>NoRecipe</c>, <c>NoWorkers</c>,
/// <c>Paused</c>, <c>MissingInput:&lt;item&gt;</c> or <c>StorageFull:&lt;item&gt;</c>, about its own store.</param>
/// <param name="Store">The goods in its store, JSON <c>store</c>: item id to quantity, by item id; only goods it holds (CR-0008).</param>
/// <param name="StoreLimits">The limit of every good its level's store holds, JSON <c>storeLimits</c>: item id to effective limit (the level's
/// limit plus completed research), by item id; empty for a building without a store (CR-0008).</param>
/// <param name="Needs">What its waiting project still needs, JSON <c>needs</c>: item id to quantity, by item id; empty without one (CR-0008).</param>
/// <param name="Project">The project waiting for goods, JSON <c>project</c>: <c>Construction</c> (a construction site), <c>Upgrade</c> (an upgrade
/// waiting while the building keeps working) or <see langword="null"/> (CR-0008).</param>
/// <param name="Paused">Whether the player paused it, JSON <c>paused</c> (CR-0008 D23): a paused producer starts no new cycle, a paused courier
/// station's couriers go home and wait; always <see langword="false"/> for other buildings and while constructing.</param>
public sealed record SnapshotBuilding(
    int Id,
    string TypeId,
    int X,
    int Y,
    int Level,
    int AssignedWorkers,
    int MaxWorkers,
    string Phase,
    long PhaseStartedAt,
    long? PhaseEndsAt,
    long CycleLabor,
    long LaborRemaining,
    long SegmentStartedAt,
    string? StallReason,
    IReadOnlyDictionary<string, long> Store,
    IReadOnlyDictionary<string, long> StoreLimits,
    IReadOnlyDictionary<string, long> Needs,
    string? Project,
    bool Paused);
