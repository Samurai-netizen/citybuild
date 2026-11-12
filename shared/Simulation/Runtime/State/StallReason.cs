#nullable enable
using System;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// Why a building makes no progress, shown to players (<c>core-instructions/04-simulation-model.md</c>, section 4). It is derived from the
    /// city and the content, never stored. Constructing and upgrading buildings make progress, so they never have one; neither has a producer
    /// whose cycle runs with workers (paused or not: it finishes), an idle producer that can start its next cycle, nor an unpaused courier
    /// station with couriers. The inputs and the
    /// room it looks at are the building's own store (<c>04</c> section 16).
    /// </summary>
    public sealed class StallReason
    {
        /// <summary>Holds one reason; <see cref="Of"/> derives them.</summary>
        private StallReason(StallKind kind, ItemId item)
        {
            Kind = kind;
            Item = item;
        }

        /// <summary>The reason.</summary>
        public StallKind Kind { get; }

        /// <summary>The missing input or the full output for <see cref="StallKind.MissingInput"/> and <see cref="StallKind.StorageFull"/>; otherwise the empty id.</summary>
        public ItemId Item { get; }

        /// <summary>
        /// Derives a building's stall reason. A courier station has <see cref="StallKind.Paused"/> when paused, else <see cref="StallKind.NoWorkers"/>
        /// without couriers and none with them. Otherwise the first match wins: <see cref="StallKind.NoRecipe"/> (houses, warehouses, the science
        /// building), <see cref="StallKind.NoWorkers"/> for a frozen cycle, none for a running one, <see cref="StallKind.Paused"/>,
        /// <see cref="StallKind.NoWorkers"/>, <see cref="StallKind.MissingInput"/>, <see cref="StallKind.StorageFull"/>; the item is the first one
        /// in recipe (ordinal item) order (<c>04</c> section 4).
        /// </summary>
        /// <param name="city">The city the building is in.</param>
        /// <param name="content">The game content.</param>
        /// <param name="building">The building.</param>
        /// <returns>The reason, or null when the building makes progress or can start its next cycle.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">The content doesn't know the building's type (the city and the content don't belong together).</exception>
        public static StallReason? Of(CityState city, GameContent content, Building building)
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

            if (building.Phase == BuildingPhase.Constructing || building.Phase == BuildingPhase.Upgrading)
            {
                return null;
            }

            BuildingDefinition definition = CityQueries.DefinitionOf(content, building.TypeId);
            if (definition.Kind == BuildingKind.CourierStation)
            {
                // A station's workers are its couriers (04 section 19): paused or without any it moves nothing.
                if (building.Paused)
                {
                    return new StallReason(StallKind.Paused, default);
                }

                return building.AssignedWorkers == 0 ? new StallReason(StallKind.NoWorkers, default) : null;
            }

            RecipeDefinition? recipe = definition.GetLevel(building.Level).Recipe;
            if (recipe is null)
            {
                return new StallReason(StallKind.NoRecipe, default);
            }

            if (building.Phase == BuildingPhase.Producing)
            {
                // A running cycle finishes even when paused (D23); without workers it is frozen.
                return building.AssignedWorkers == 0 ? new StallReason(StallKind.NoWorkers, default) : null;
            }

            if (building.Paused)
            {
                return new StallReason(StallKind.Paused, default);
            }

            if (building.AssignedWorkers == 0)
            {
                return new StallReason(StallKind.NoWorkers, default);
            }

            if (CityQueries.TryFindMissingInput(building, recipe, out ItemId missing))
            {
                return new StallReason(StallKind.MissingInput, missing);
            }

            return CityQueries.TryFindFullOutput(city, content, building, recipe, out ItemId full) ? new StallReason(StallKind.StorageFull, full) : null;
        }

        /// <summary>The text of the API (<c>05</c>): <c>NoRecipe</c>, <c>NoWorkers</c>, <c>Paused</c>, <c>MissingInput:logs</c> or <c>StorageFull:planks</c>.</summary>
        /// <returns>The kind, followed by a colon and the item when there is one.</returns>
        public override string ToString() => Item == default ? Kind.ToString() : Kind.ToString() + ":" + Item.Value;
    }
}
