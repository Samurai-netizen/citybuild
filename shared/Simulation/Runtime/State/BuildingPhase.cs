#nullable enable

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// What a placed building is doing (<c>core-instructions/04-simulation-model.md</c>, section 4). Each phase uses some of the
    /// <see cref="Building"/> timer fields; the ones it doesn't use are 0 (and <see cref="Building.PhaseEndsAt"/> is null), so two buildings
    /// in the same situation are equal field by field.
    /// </summary>
    public enum BuildingPhase
    {
        /// <summary>
        /// Placed, not usable yet; uses a builder slot and provides no workers. Uses <see cref="Building.PhaseStartedAt"/> and
        /// <see cref="Building.PhaseEndsAt"/> (never null).
        /// </summary>
        Constructing,

        /// <summary>
        /// Rising to <see cref="Building.TargetLevel"/>, which is the level plus one; uses a builder slot and produces nothing, but keeps its
        /// workers. Uses <see cref="Building.PhaseStartedAt"/>, <see cref="Building.PhaseEndsAt"/> (never null) and
        /// <see cref="Building.TargetLevel"/>.
        /// </summary>
        Upgrading,

        /// <summary>Ready, not producing. Uses no timer field. Houses are always idle once completed.</summary>
        Idle,

        /// <summary>
        /// Running a production cycle: the inputs are consumed and the outputs reserved. Uses <see cref="Building.PhaseStartedAt"/> (when the
        /// cycle started), <see cref="Building.CycleLabor"/>, <see cref="Building.LaborRemaining"/>, <see cref="Building.SegmentStartedAt"/>
        /// and <see cref="Building.PhaseEndsAt"/>, which is null while the cycle is frozen (no workers).
        /// </summary>
        Producing,
    }
}
