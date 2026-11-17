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

        /// <summary>
        /// At the source with a load its full store can't take yet (<c>04</c> section 18, M0-C-3): the worker puts it back as room appears,
        /// then the trip ends. No walk is under way; the departure and arrival stay those of the walk back.
        /// </summary>
        Waiting,
    }
}
