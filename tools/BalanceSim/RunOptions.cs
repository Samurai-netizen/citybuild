namespace CityBuilder.BalanceSim;

/// <summary>
/// The settings of one simulated run: which strategy plays, when the player checks in, and how long and how finely the run is recorded.
/// These are tool settings, not game numbers: every game number comes from the content (core-instructions/02-principles.md, rule 8).
/// </summary>
/// <param name="Strategy">The strategy that plays.</param>
/// <param name="Sessions">When the player checks in.</param>
/// <param name="RunSeconds">The length of the run on the city clock, above 0.</param>
/// <param name="FirstSessionSeconds">How long the first check-in lasts (the first play session), above 0.</param>
/// <param name="SessionSeconds">How long every later check-in lasts, above 0.</param>
/// <param name="SampleSeconds">The spacing of the timeline rows, above 0; a row is also written at every check-in and at the end.</param>
/// <param name="GraceSeconds">
/// The seconds after a check-in ends until the player counts as absent: the server's <c>Simulation:PresenceGraceSeconds</c> (300) at
/// <c>DevTimeScale</c> 1 (core-instructions/04-simulation-model.md §2).
/// </param>
public sealed record RunOptions(
    Strategies.StrategyKind Strategy,
    SessionPattern Sessions,
    long RunSeconds,
    long FirstSessionSeconds,
    long SessionSeconds,
    long SampleSeconds,
    long GraceSeconds)
{
    /// <summary>The seconds between the strategy's decisions while the player is present.</summary>
    public const long PresentTickSeconds = 10;

    /// <summary>The longest step of the run while the player is away; stall and idle-worker shares are measured at this resolution.</summary>
    public const long AbsentTickSeconds = 60;
}
