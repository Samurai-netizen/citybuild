#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// What a producer makes in one cycle: it takes the inputs at the start, needs the labor, and gives the outputs at the end
    /// (<c>core-instructions/04-simulation-model.md</c>, sections 3 and 6).
    /// </summary>
    public sealed class RecipeDefinition
    {
        internal RecipeDefinition(IReadOnlyList<ItemQuantity> inputs, IReadOnlyList<ItemQuantity> outputs, long labor)
        {
            Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
            Outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
            Labor = labor;
        }

        /// <summary>What one cycle consumes when it starts, sorted by item id. Empty for a raw producer such as a woodcutter.</summary>
        public IReadOnlyList<ItemQuantity> Inputs { get; }

        /// <summary>What one cycle makes when it ends, sorted by item id. At least one item.</summary>
        public IReadOnlyList<ItemQuantity> Outputs { get; }

        /// <summary>
        /// The work one cycle needs, in worker-seconds, before research reductions. With <c>w</c> workers a cycle takes
        /// <c>CeilDiv(labor, w)</c> seconds.
        /// </summary>
        public long Labor { get; }
    }
}
