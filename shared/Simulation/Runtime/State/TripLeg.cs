#nullable enable

namespace CityBuilder.Simulation.State
{
    /// <summary>Which way a worker's delivery (a <see cref="Trip"/>) walks now (<c>core-instructions/04-simulation-model.md</c>, section 18, CR-0008 D21).</summary>
    public enum TripLeg
    {
        /// <summary>From the source to the target, carrying a load.</summary>
        ToTarget,

        /// <summary>Back from the target to the source for the next load, carrying nothing.</summary>
        ToSource,
    }
}
