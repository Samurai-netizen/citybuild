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

        /// <summary>
        /// At the source, about to fetch the next load, but no walk joins it to the target (a removed bridge, M1-03 decision 4): the worker
        /// carries nothing, the goods to fetch stay in the source's store, and it takes its load and walks from the first second a path exists
        /// (or ends the trip when nothing is left to take). The departure and arrival stay those of the walk that brought it there.
        /// </summary>
        BlockedAtSource,

        /// <summary>
        /// At the target after a delivery, but no walk joins it back to the source (M1-03 decision 4): it waits there with what it carries
        /// back (nothing, with goods still to fetch, or the goods the target didn't take, with none) and walks back from the first second a
        /// path exists. The departure and arrival stay those of the walk that brought it there.
        /// </summary>
        BlockedAtTarget,
    }
}
