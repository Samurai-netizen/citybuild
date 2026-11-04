namespace CityBuilder.Api.Contracts;

/// <summary>The rules every city follows, the <c>rules</c> object of <see cref="ContentResponse"/> (<c>core-instructions/04-simulation-model.md</c>).</summary>
/// <param name="GridWidth">The width of the city grid in tiles, JSON <c>gridWidth</c>.</param>
/// <param name="GridHeight">The height of the city grid in tiles, JSON <c>gridHeight</c>.</param>
/// <param name="BuilderSlots">How many buildings can be under construction or upgrade at once, JSON <c>builderSlots</c>.</param>
/// <param name="ResearchSlots">How many research nodes can run at once, JSON <c>researchSlots</c>.</param>
/// <param name="MaxOfflineProductionSeconds">The longest window in which cycles can start in one settlement, JSON <c>maxOfflineProductionSeconds</c>.</param>
/// <param name="MinCycleSeconds">The shortest a production cycle can take after research, JSON <c>minCycleSeconds</c>.</param>
/// <param name="MaxLaborReductionPermille">The most research can reduce a cycle's labor, in permille, JSON <c>maxLaborReductionPermille</c>.</param>
/// <param name="GroundTileMillis">How long a walker takes to cross one tile of open ground, in milliseconds, JSON <c>groundTileMillis</c>.</param>
/// <param name="CrossingSpeedPermille">How fast a walker crosses another building's tile, in permille of the ground speed, JSON <c>crossingSpeedPermille</c>.</param>
/// <param name="MinWalkSeconds">The shortest walk the content allows, in seconds, JSON <c>minWalkSeconds</c>.</param>
/// <param name="MaxWalkBonusPermille">The most research can shorten a walk, in permille, JSON <c>maxWalkBonusPermille</c>.</param>
/// <param name="TripCarryLimit">The most goods one manual trip carries, JSON <c>tripCarryLimit</c> (CR-0008).</param>
/// <param name="MaxRouteStops">The most stops a courier's route has, JSON <c>maxRouteStops</c> (CR-0008).</param>
/// <param name="MaxStopActions">The most actions one stop of a route has, JSON <c>maxStopActions</c> (CR-0008).</param>
/// <param name="TimeScale">How many city-clock seconds pass per server second, JSON <c>timeScale</c>; always 1 outside Development. The client's
/// display clock is <c>city.clock + secondsSinceSnapshotReceived × timeScale</c> (<c>05</c>, snapshot).</param>
public sealed record ContentRules(
    int GridWidth,
    int GridHeight,
    int BuilderSlots,
    int ResearchSlots,
    long MaxOfflineProductionSeconds,
    long MinCycleSeconds,
    int MaxLaborReductionPermille,
    long GroundTileMillis,
    int CrossingSpeedPermille,
    long MinWalkSeconds,
    int MaxWalkBonusPermille,
    long TripCarryLimit,
    int MaxRouteStops,
    int MaxStopActions,
    int TimeScale);
