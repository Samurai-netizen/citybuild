#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// Values derived from a city and the content, never stored (<c>core-instructions/04-simulation-model.md</c>, sections 3, 5, 8, 16 and 17). Every
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
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                Building building = buildings[index];
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
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                assigned = checked(assigned + buildings[index].AssignedWorkers);
            }

            return assigned;
        }

        /// <summary>The workers the player can still assign: provided minus assigned. Never below 0 in a valid city (<c>04</c> invariant 9).</summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <returns>The free workers.</returns>
        public static long FreeWorkers(CityState city, GameContent content) => checked(WorkersProvided(city, content) - WorkersAssigned(city));

        /// <summary>The builder slots in use: one per building that is constructing (a construction site too) or upgrading, or whose upgrade waits for goods (<c>04</c> section 8).</summary>
        /// <param name="city">The city.</param>
        /// <returns>The number of busy builders.</returns>
        public static int BuildersBusy(CityState city)
        {
            int busy = 0;
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                BuildingPhase phase = buildings[index].Phase;
                if (phase == BuildingPhase.Constructing || phase == BuildingPhase.Upgrading || buildings[index].HasUpgradeWaiting)
                {
                    busy++;
                }
            }

            return busy;
        }

        /// <summary>
        /// How much of a good a building has reserved: the output of its own running cycle (frozen ones included), which will enter its store
        /// when the cycle completes (<c>04</c> section 16). Derived from the phase, never stored. An idle building reserves nothing.
        /// </summary>
        /// <param name="content">The game content.</param>
        /// <param name="building">The building.</param>
        /// <param name="item">The good.</param>
        /// <returns>The reserved quantity, 0 when the building isn't producing the good.</returns>
        public static long Reserved(GameContent content, Building building, ItemId item)
        {
            if (building.Phase != BuildingPhase.Producing)
            {
                return 0;
            }

            long reserved = 0;
            IReadOnlyList<ItemQuantity> outputs = RecipeOf(content, building.TypeId, building.Level).Outputs;
            for (int index = 0; index < outputs.Count; index++)
            {
                if (outputs[index].Item == item)
                {
                    reserved = checked(reserved + outputs[index].Quantity);
                }
            }

            return reserved;
        }

        /// <summary>
        /// A building store's effective limit for a good (<c>04</c> section 16): its level's limit plus the <see cref="StorageCapBonusEffect"/>s
        /// of the city's completed research for that good.
        /// </summary>
        /// <param name="city">The city, whose completed research raises the limits.</param>
        /// <param name="content">The game content.</param>
        /// <param name="building">The building.</param>
        /// <param name="item">The good.</param>
        /// <param name="limit">The effective limit; 0 when the store doesn't hold the good.</param>
        /// <returns>True when the building's store holds the good at its level.</returns>
        public static bool TryGetEffectiveLimit(CityState city, GameContent content, Building building, ItemId item, out long limit)
        {
            if (!DefinitionOf(content, building.TypeId).GetLevel(building.Level).TryGetStorageLimit(item, out limit))
            {
                return false;
            }

            IReadOnlyList<ResearchId> completed = city.CompletedResearch;
            for (int index = 0; index < completed.Count; index++)
            {
                IReadOnlyList<ResearchEffect> effects = ResearchOf(content, completed[index]).Effects;
                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    if (effects[effectIndex] is StorageCapBonusEffect bonus && bonus.Item == item)
                    {
                        limit = checked(limit + bonus.Amount);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// How much more of a good a building's store takes (<c>04</c> section 16): effective limit − stock − reserved, at least 0 (a refund or a
        /// returned load may have put the stock above the limit); 0 when the store doesn't hold the good.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="building">The building.</param>
        /// <param name="item">The good.</param>
        /// <returns>The room for the good.</returns>
        public static long Room(CityState city, GameContent content, Building building, ItemId item) =>
            TryGetEffectiveLimit(city, content, building, item, out long limit)
                ? Math.Max(0, checked(limit - building.GetStock(item) - Reserved(content, building, item)))
                : 0;

        /// <summary>
        /// The city's total of an item (<c>04</c> section 16): for the currency the purse; for a good the sum of every building's store (from
        /// CR-0008-T1 part E2 also the loads of walkers).
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="item">The item.</param>
        /// <returns>The total, 0 or more.</returns>
        public static long Total(CityState city, GameContent content, ItemId item)
        {
            if (item == content.Currency)
            {
                return city.Purse;
            }

            long total = 0;
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                total = checked(total + buildings[index].GetStock(item));
            }

            return total;
        }

        /// <summary>Every item's city total (<see cref="Total"/>) in the content's item order (ordinal ids), 0 included: the HUD's and the snapshot's totals.</summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <returns>One quantity per item of the content.</returns>
        public static IReadOnlyList<ItemQuantity> Totals(CityState city, GameContent content)
        {
            var totals = new ItemQuantity[content.Items.Count];
            for (int index = 0; index < totals.Length; index++)
            {
                ItemId item = content.Items[index].Id;
                totals[index] = new ItemQuantity(item, Total(city, content, item));
            }

            return Array.AsReadOnly(totals);
        }

        /// <summary>How many buildings of a type the city has, in any phase (a construction site included): what <c>maxPerCity</c> limits.</summary>
        /// <param name="city">The city.</param>
        /// <param name="type">The building type.</param>
        /// <returns>The count.</returns>
        public static int CountOfType(CityState city, BuildingTypeId type)
        {
            int count = 0;
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                if (buildings[index].TypeId == type)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The city's completed science building, where research happens and its goods are delivered (<c>04</c> section 16), or null when it has
        /// none yet (a construction site or a building still constructing doesn't count). Content rule 10 allows one per city.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <returns>The science building, or null.</returns>
        public static Building? ScienceBuilding(CityState city, GameContent content)
        {
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                Building building = buildings[index];
                if (building.Phase != BuildingPhase.Constructing && DefinitionOf(content, building.TypeId).Kind == BuildingKind.Science)
                {
                    return building;
                }
            }

            return null;
        }

        /// <summary>
        /// The city's walk bonus in permille (<c>04</c> section 17): the sum of the <see cref="WalkSpeedBonusEffect"/>s of its completed research,
        /// at most <see cref="GameRules.MaxWalkBonusPermille"/> by content rule 12.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <returns>The bonus, 0 to 900.</returns>
        public static int WalkBonusPermille(CityState city, GameContent content)
        {
            int bonus = 0;
            IReadOnlyList<ResearchId> completed = city.CompletedResearch;
            for (int index = 0; index < completed.Count; index++)
            {
                IReadOnlyList<ResearchEffect> effects = ResearchOf(content, completed[index]).Effects;
                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    if (effects[effectIndex] is WalkSpeedBonusEffect walk)
                    {
                        bonus = checked(bonus + walk.Permille);
                    }
                }
            }

            return bonus;
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
            IReadOnlyList<ResearchId> completed = city.CompletedResearch;
            for (int index = 0; index < completed.Count; index++)
            {
                IReadOnlyList<ResearchEffect> effects = ResearchOf(content, completed[index]).Effects;
                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    if (effects[effectIndex] is LaborReductionEffect reduction && reduction.Building == type)
                    {
                        reductionPermille = checked(reductionPermille + reduction.Permille);
                    }
                }
            }

            // Content rule 7 keeps the sum within MaxLaborReductionPermille, which is at most 1000.
            return IntegerMath.ApplyPermilleReduction(recipe.Labor, reductionPermille);
        }

        /// <summary>
        /// Whether the city may place a building type: the type belongs to the city's era or an earlier one (owner directive OD-10), and it is
        /// available at the start or the research that unlocks it is completed.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="type">The building type.</param>
        /// <returns>True when the type may be placed; false for a type of a later era or one the content doesn't know.</returns>
        public static bool IsUnlocked(CityState city, GameContent content, BuildingTypeId type)
        {
            if (!content.TryGetBuilding(type, out BuildingDefinition? definition) || definition.Era > city.Era)
            {
                return false;
            }

            ResearchId? unlockedBy = definition.UnlockedByResearch;
            return definition.IsAvailableAtStart || (unlockedBy.HasValue && city.HasCompletedResearch(unlockedBy.Value));
        }

        /// <summary>What lies on a tile (<c>04</c> section 17): a building (in any phase), a road, or nothing. A road and a building never share a tile.</summary>
        /// <param name="city">The city.</param>
        /// <param name="x">The tile column.</param>
        /// <param name="y">The tile row.</param>
        /// <returns>What the tile holds; <see cref="TileContent.Ground"/> also for a tile outside the grid, which the caller checks.</returns>
        public static TileContent TileAt(CityState city, int x, int y)
        {
            if (city.TryGetBuildingAt(x, y, out _))
            {
                return TileContent.Building;
            }

            return city.TryGetRoad(x, y, out _) ? TileContent.Road : TileContent.Ground;
        }

        /// <summary>The first input of the recipe, in recipe (ordinal item) order, of which the building's own store holds less than the cycle needs.</summary>
        internal static bool TryFindMissingInput(Building building, RecipeDefinition recipe, out ItemId item)
        {
            IReadOnlyList<ItemQuantity> inputs = recipe.Inputs;
            for (int index = 0; index < inputs.Count; index++)
            {
                if (building.GetStock(inputs[index].Item) < inputs[index].Quantity)
                {
                    item = inputs[index].Item;
                    return true;
                }
            }

            item = default;
            return false;
        }

        /// <summary>
        /// The first output of the recipe, in recipe (ordinal item) order, that doesn't fit a new cycle in the building's own store
        /// (<see cref="FitsInStore"/>, <c>04</c> section 6). The currency always fits.
        /// </summary>
        internal static bool TryFindFullOutput(CityState city, GameContent content, Building building, RecipeDefinition recipe, out ItemId item)
        {
            IReadOnlyList<ItemQuantity> outputs = recipe.Outputs;
            for (int index = 0; index < outputs.Count; index++)
            {
                ItemId output = outputs[index].Item;
                if (!FitsInStore(city, content, building, outputs[index]))
                {
                    item = output;
                    return true;
                }
            }

            item = default;
            return false;
        }

        /// <summary>
        /// Whether one output of a cycle fits the building's own store (<c>04</c> section 6): the currency always does (it goes into the purse);
        /// a good does when <c>stock + reserved + quantity ≤ effective limit</c>, where reserved is the building's own running cycle's output (0
        /// for an idle building, which is the one that starts).
        /// </summary>
        internal static bool FitsInStore(CityState city, GameContent content, Building building, ItemQuantity output)
        {
            if (output.Item == content.Currency)
            {
                return true;
            }

            // Content rule 9 makes a producer's store hold every good of its recipe; a store without the good has no room for it.
            return TryGetEffectiveLimit(city, content, building, output.Item, out long limit)
                && checked(building.GetStock(output.Item) + Reserved(content, building, output.Item) + output.Quantity) <= limit;
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

        /// <summary>The definition of a research node the city has completed; throws when the content doesn't know it, since then the city doesn't belong to this content.</summary>
        private static ResearchDefinition ResearchOf(GameContent content, ResearchId id) =>
            content.TryGetResearch(id, out ResearchDefinition? research)
                ? research
                : throw new InvalidOperationException(FormattableString.Invariant($"The city has completed research {id}, which the content doesn't know."));
    }
}
