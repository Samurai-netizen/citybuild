#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Display
{
    /// <summary>
    /// Production rates for the player's screens: the preview of a worker count and the net rate of an item. <b>Display only, never authority.</b>
    /// The server decides what is produced through <see cref="Settlement.Simulator"/>; these numbers are estimates of the steady state that
    /// ignore inputs running out, storage filling up and research finishing, and they round down. Rates are in thousandths of an item per
    /// hour (<see cref="ItemRate"/>).
    /// </summary>
    public static class ProductionDisplay
    {
        // One hour in seconds, and the milli scale of ItemRate. Their product multiplies a quantity before the division by the cycle seconds:
        // content bounds a quantity by 10^12 (GameContentFactory.MaxQuantity), so the product is at most 3.6 × 10^18, inside a long.
        private const long _secondsPerHour = 3600;
        private const long _milliScale = 1000;

        /// <summary>
        /// The inputs a producer would use and the outputs it would make per hour with <paramref name="workers"/> workers: each recipe line's
        /// quantity per cycle over the cycle's seconds, <c>CeilDiv(labor now, workers)</c>. The labor includes the reductions of the city's
        /// completed research, because the next cycle gets them. Every rate is 0 with no workers (the cycle would freeze). The building's phase
        /// doesn't matter: this is the preview of its current level, for example for the worker stepper.
        /// </summary>
        /// <param name="city">The city the building is in.</param>
        /// <param name="content">The game content.</param>
        /// <param name="building">The producer.</param>
        /// <param name="workers">The workers to preview, from 0 to the level's maximum.</param>
        /// <returns>The rates, in recipe order.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The building is a house, so it has no recipe.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The workers are below 0 or above the level's maximum.</exception>
        /// <exception cref="InvalidOperationException">The content doesn't know the building's type.</exception>
        public static ProductionRates PerHour(CityState city, GameContent content, Building building, int workers)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (building is null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            BuildingLevelDefinition level = CityQueries.DefinitionOf(content, building.TypeId).GetLevel(building.Level);
            RecipeDefinition recipe = level.Recipe
                ?? throw new ArgumentException(FormattableString.Invariant($"Building type {building.TypeId} has no recipe."), nameof(building));
            if (workers < 0 || workers > level.MaxWorkers)
            {
                throw new ArgumentOutOfRangeException(nameof(workers), workers, FormattableString.Invariant($"The workers must be between 0 and {level.MaxWorkers}."));
            }

            // Zero workers means a frozen cycle: no seconds to divide by, and nothing flows.
            long cycleSeconds = workers == 0
                ? 0
                : IntegerMath.CeilDiv(CityQueries.EffectiveLabor(city, content, building.TypeId, building.Level), workers);
            return new ProductionRates(RatesOf(recipe.Inputs, cycleSeconds), RatesOf(recipe.Outputs, cycleSeconds));
        }

        /// <summary>One recipe line's rate for a cycle of <paramref name="cycleSeconds"/>; 0 when the cycle never runs (0 seconds).</summary>
        internal static long MilliPerHour(long quantity, long cycleSeconds) =>
            cycleSeconds == 0 ? 0 : IntegerMath.FloorDiv(checked(quantity * _secondsPerHour * _milliScale), cycleSeconds);

        private static ItemRate[] RatesOf(IReadOnlyList<ItemQuantity> lines, long cycleSeconds)
        {
            var rates = new ItemRate[lines.Count];
            for (int index = 0; index < lines.Count; index++)
            {
                rates[index] = new ItemRate(lines[index].Item, MilliPerHour(lines[index].Quantity, cycleSeconds));
            }

            return rates;
        }
    }
}
