#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Display
{
    /// <summary>
    /// What one producer takes in and gives out per hour at a staffing, for display only (<see cref="ProductionDisplay.PerHour"/>). Both lists
    /// follow the recipe's order (ordinal item ids).
    /// </summary>
    public sealed class ProductionRates
    {
        /// <summary>Creates the rates of one producer.</summary>
        /// <param name="inputs">The inputs it uses per hour.</param>
        /// <param name="outputs">The outputs it makes per hour.</param>
        public ProductionRates(IReadOnlyList<ItemRate> inputs, IReadOnlyList<ItemRate> outputs)
        {
            Inputs = inputs;
            Outputs = outputs;
        }

        /// <summary>The inputs used per hour; empty for a raw producer.</summary>
        public IReadOnlyList<ItemRate> Inputs { get; }

        /// <summary>The outputs made per hour.</summary>
        public IReadOnlyList<ItemRate> Outputs { get; }
    }
}
