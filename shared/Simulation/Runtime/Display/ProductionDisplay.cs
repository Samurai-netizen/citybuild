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
        /// <summary>
        /// One hour in seconds, the time unit of every rate here. A quantity is multiplied by this and by <see cref="_milliScale"/> before the
        /// division by the cycle seconds; content bounds a quantity by 10^12 (<see cref="GameContentFactory.MaxQuantity"/>), so the product is at
        /// most 3.6 × 10^18, inside a <c>long</c>.
        /// </summary>
        private const long _secondsPerHour = 3600;
        /// <summary>Thousandths per item: rates are <see cref="ItemRate.MilliPerHour"/>, so a producer slower than one item per hour still shows a rate.</summary>
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

        /// <summary>
        /// The net rate of an item now: what the buildings that are able to run make of it per hour, minus what they use. A building counts when
        /// it has workers and either runs a cycle or could start one (<see cref="StallReason.Of"/> is null); constructing and upgrading
        /// buildings, houses, idle producers that miss an input or have a full output, and buildings without workers (a frozen cycle too)
        /// contribute 0. Each counts at its assigned workers and the labor a new cycle needs now.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="item">The item.</param>
        /// <returns>Thousandths of an item per hour; negative when the buildings use more than they make, 0 when none touches the item.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">The content doesn't know a building's type.</exception>
        public static long NetPerHour(CityState city, GameContent content, ItemId item)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            long net = 0;
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                Building building = buildings[index];
                if (!IsAbleToRun(city, content, building))
                {
                    continue;
                }

                RecipeDefinition recipe = CityQueries.RecipeOf(content, building.TypeId, building.Level);
                long cycleSeconds = IntegerMath.CeilDiv(CityQueries.EffectiveLabor(city, content, building.TypeId, building.Level), building.AssignedWorkers);
                net = checked(net + SumOf(recipe.Outputs, item, cycleSeconds) - SumOf(recipe.Inputs, item, cycleSeconds));
            }

            return net;
        }

        /// <summary>
        /// Seconds until the stock of a capped item reaches its cap at the net rate of <see cref="NetPerHour"/>, rounded up. It is a rough
        /// estimate: it ignores inputs running out and research finishing, and production stops a little before the stock is exactly at
        /// the cap, because a cycle starts only while its whole output fits.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="item">The item.</param>
        /// <returns>
        /// The seconds, 0 when the stock is at the cap already (refunds may put it above) while something still makes the item; null when the
        /// item is uncapped or its net rate is not above 0.
        /// </returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">The content doesn't know a building's type or research.</exception>
        public static long? SecondsUntilFull(CityState city, GameContent content, ItemId item)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!CityQueries.TryGetEffectiveCap(city, content, item, out long cap))
            {
                return null;
            }

            long net = NetPerHour(city, content, item);
            if (net <= 0)
            {
                return null;
            }

            long missing = cap - city.GetQuantity(item);
            // Items × 3600 s × 1000 over thousandths of an item per hour gives seconds; rounding up never shows the cap sooner than it comes.
            return missing <= 0 ? 0 : IntegerMath.CeilDiv(checked(missing * _secondsPerHour * _milliScale), net);
        }

        /// <summary>One recipe line's rate for a cycle of <paramref name="cycleSeconds"/>; 0 when the cycle never runs (0 seconds).</summary>
        internal static long MilliPerHour(long quantity, long cycleSeconds) =>
            cycleSeconds == 0 ? 0 : IntegerMath.FloorDiv(checked(quantity * _secondsPerHour * _milliScale), cycleSeconds);

        /// <summary>
        /// Whether a building counts toward an item's net rate: it has workers, and its cycle runs or, when idle, it could start the next one now
        /// (no stall reason). Constructing, upgrading and frozen buildings make nothing.
        /// </summary>
        private static bool IsAbleToRun(CityState city, GameContent content, Building building)
        {
            if (building.AssignedWorkers == 0 || (building.Phase != BuildingPhase.Idle && building.Phase != BuildingPhase.Producing))
            {
                return false;
            }

            // A running cycle with workers makes progress; an idle producer counts only when it could start its next cycle.
            return building.Phase == BuildingPhase.Producing || StallReason.Of(city, content, building) is null;
        }

        /// <summary>The rate of one item over the recipe lines that name it, for a cycle of <paramref name="cycleSeconds"/>; 0 when no line names it.</summary>
        private static long SumOf(IReadOnlyList<ItemQuantity> lines, ItemId item, long cycleSeconds)
        {
            long sum = 0;
            for (int index = 0; index < lines.Count; index++)
            {
                if (lines[index].Item == item)
                {
                    sum = checked(sum + MilliPerHour(lines[index].Quantity, cycleSeconds));
                }
            }

            return sum;
        }

        /// <summary>One rate per recipe line, in recipe order, for a cycle of <paramref name="cycleSeconds"/>.</summary>
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
