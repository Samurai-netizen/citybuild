#nullable enable
using System;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// Values derived from a city and the content, never stored (<c>core-instructions/04-simulation-model.md</c>, sections 3, 5 and 8). Every
    /// building type and research node the city names must exist in the content; if one doesn't, the city and the content don't belong
    /// together, and the query throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public static class CityQueries
    {
        /// <summary>
        /// The workers the city's houses provide: the sum of their level's workers over every completed house. A house still constructing
        /// provides none; an upgrading house provides its current level's workers until the upgrade completes.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <returns>The workers provided, at least 0.</returns>
        public static long WorkersProvided(CityState city, GameContent content)
        {
            long provided = 0;
            foreach (Building building in city.Buildings)
            {
                if (building.Phase != BuildingPhase.Constructing)
                {
                    // Producers provide 0 workers at every level (content rule 3), so summing every completed building counts the houses only.
                    provided = checked(provided + DefinitionOf(content, building.TypeId).GetLevel(building.Level).WorkersProvided);
                }
            }

            return provided;
        }

        /// <summary>The workers assigned to all buildings, upgrading ones included.</summary>
        /// <param name="city">The city.</param>
        /// <returns>The sum of every building's <see cref="Building.AssignedWorkers"/>.</returns>
        public static long WorkersAssigned(CityState city)
        {
            long assigned = 0;
            foreach (Building building in city.Buildings)
            {
                assigned = checked(assigned + building.AssignedWorkers);
            }

            return assigned;
        }

        /// <summary>The workers the player can still assign: provided minus assigned. Never below 0 in a valid city (<c>04</c> invariant 9).</summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <returns>The free workers.</returns>
        public static long FreeWorkers(CityState city, GameContent content) => checked(WorkersProvided(city, content) - WorkersAssigned(city));

        /// <summary>The builder slots in use: one per building that is constructing or upgrading.</summary>
        /// <param name="city">The city.</param>
        /// <returns>The number of busy builders.</returns>
        public static int BuildersBusy(CityState city)
        {
            int busy = 0;
            foreach (Building building in city.Buildings)
            {
                if (building.Phase == BuildingPhase.Constructing || building.Phase == BuildingPhase.Upgrading)
                {
                    busy++;
                }
            }

            return busy;
        }

        /// <summary>
        /// How much of an item the running cycles will deliver: the sum of the item's outputs over every producing building, frozen cycles
        /// included. Production may start only while stock plus reserved plus the new output fits the cap.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="item">The item.</param>
        /// <returns>The reserved quantity, 0 when no running cycle makes the item.</returns>
        public static long Reserved(CityState city, GameContent content, ItemId item)
        {
            long reserved = 0;
            foreach (Building building in city.Buildings)
            {
                if (building.Phase != BuildingPhase.Producing)
                {
                    continue;
                }

                RecipeDefinition recipe = RecipeOf(content, building.TypeId, building.Level);
                foreach (ItemQuantity output in recipe.Outputs)
                {
                    if (output.Item == item)
                    {
                        reserved = checked(reserved + output.Quantity);
                    }
                }
            }

            return reserved;
        }

        /// <summary>The storage cap of an item now: its base cap plus the bonuses of every completed research. Uncapped items have none.</summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="item">The item.</param>
        /// <param name="cap">The cap, when the item is capped; otherwise 0.</param>
        /// <returns>True when the item is capped.</returns>
        public static bool TryGetEffectiveCap(CityState city, GameContent content, ItemId item, out long cap)
        {
            if (!content.Rules.TryGetBaseStorageCap(item, out cap))
            {
                return false;
            }

            foreach (ResearchId completed in city.CompletedResearch)
            {
                foreach (ResearchEffect effect in ResearchOf(content, completed).Effects)
                {
                    if (effect is StorageCapBonusEffect bonus && bonus.Item == item)
                    {
                        cap = checked(cap + bonus.Amount);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// The labor a cycle that starts now needs, in worker-seconds: the level's recipe labor with the labor reductions of every completed
        /// research applied, <c>IntegerMath.ApplyPermilleReduction(labor, Σ permille)</c>. A running cycle keeps the labor it started with.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="type">A producer type.</param>
        /// <param name="level">A level of that type.</param>
        /// <returns>The effective labor, at least 1 (content rule 7 keeps every cycle at least <see cref="GameRules.MinCycleSeconds"/> long).</returns>
        /// <exception cref="ArgumentException">The type is unknown, or the level has no recipe (a house).</exception>
        /// <exception cref="ArgumentOutOfRangeException">The level is outside the type's levels.</exception>
        public static long EffectiveLabor(CityState city, GameContent content, BuildingTypeId type, int level)
        {
            if (!content.TryGetBuilding(type, out BuildingDefinition? definition))
            {
                throw new ArgumentException(FormattableString.Invariant($"Building type {type} is not in the content."), nameof(type));
            }

            RecipeDefinition? recipe = definition.GetLevel(level).Recipe;
            if (recipe is null)
            {
                throw new ArgumentException(FormattableString.Invariant($"Building type {type} has no recipe."), nameof(type));
            }

            int reductionPermille = 0;
            foreach (ResearchId completed in city.CompletedResearch)
            {
                foreach (ResearchEffect effect in ResearchOf(content, completed).Effects)
                {
                    if (effect is LaborReductionEffect reduction && reduction.Building == type)
                    {
                        reductionPermille = checked(reductionPermille + reduction.Permille);
                    }
                }
            }

            // Content rule 7 keeps the sum within MaxLaborReductionPermille, which is at most 1000.
            return IntegerMath.ApplyPermilleReduction(recipe.Labor, reductionPermille);
        }

        /// <summary>
        /// Whether the city may place a building type: it is available at the start, or the research that unlocks it is completed.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="type">The building type.</param>
        /// <returns>True when the type may be placed; false for a type the content doesn't know.</returns>
        public static bool IsUnlocked(CityState city, GameContent content, BuildingTypeId type)
        {
            if (!content.TryGetBuilding(type, out BuildingDefinition? definition))
            {
                return false;
            }

            ResearchId? unlockedBy = definition.UnlockedByResearch;
            return definition.IsAvailableAtStart || (unlockedBy.HasValue && city.HasCompletedResearch(unlockedBy.Value));
        }

        /// <summary>The definition of a building type the city uses.</summary>
        internal static BuildingDefinition DefinitionOf(GameContent content, BuildingTypeId type) =>
            content.TryGetBuilding(type, out BuildingDefinition? definition)
                ? definition
                : throw new InvalidOperationException(FormattableString.Invariant($"The city has a building of type {type}, which the content doesn't know."));

        /// <summary>The recipe a producer runs at a level; houses have none.</summary>
        internal static RecipeDefinition RecipeOf(GameContent content, BuildingTypeId type, int level) =>
            DefinitionOf(content, type).GetLevel(level).Recipe
            ?? throw new InvalidOperationException(FormattableString.Invariant($"The city has a {type} at level {level} producing, but it has no recipe."));

        private static ResearchDefinition ResearchOf(GameContent content, ResearchId id) =>
            content.TryGetResearch(id, out ResearchDefinition? research)
                ? research
                : throw new InvalidOperationException(FormattableString.Invariant($"The city has completed research {id}, which the content doesn't know."));
    }
}
