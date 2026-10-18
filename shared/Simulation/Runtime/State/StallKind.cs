#nullable enable

namespace CityBuilder.Simulation.State
{
    /// <summary>Why a building makes no progress (<c>core-instructions/04-simulation-model.md</c>, section 4), in the order the checks run.</summary>
    public enum StallKind
    {
        /// <summary>The building has no recipe: a house. Houses provide workers and never produce.</summary>
        NoRecipe,

        /// <summary>A producer with 0 workers: idle, or a frozen cycle.</summary>
        NoWorkers,

        /// <summary>An idle producer with workers that lacks an input for its next cycle; <see cref="StallReason.Item"/> names it.</summary>
        MissingInput,

        /// <summary>
        /// An idle producer with workers and its inputs, but an output doesn't fit the storage cap (stock + reserved + the cycle's quantity);
        /// <see cref="StallReason.Item"/> names it.
        /// </summary>
        StorageFull,
    }
}
