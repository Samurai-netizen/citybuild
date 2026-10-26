#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// The player's mutations of a city (<c>core-instructions/04-simulation-model.md</c>, section 8). Each runs at the city's clock, after the
    /// caller has settled the city to now; the caller then runs a zero-length advance (<see cref="Settlement.Simulator.Advance"/> with 0), so
    /// the returned state is canonical. Each checks in the order of section 8 and returns the first failure; a rejected mutation changes nothing.
    /// An applied one names the cost it paid and the inputs it refunded (<see cref="MutationResult.Spent"/>, <see cref="MutationResult.Refunded"/>).
    /// </summary>
    public static class CityRules
    {
        /// <summary>
        /// Places a building: pays the type's cost and starts construction on the tile, from the clock for the type's build seconds. The
        /// building gets the next id, level 1 and no workers. Placing never needs workers.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="typeId">The building type.</param>
        /// <param name="x">The tile column.</param>
        /// <param name="y">The tile row.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.UnknownBuildingType"/>, <see cref="MutationErrorCode.BuildingLocked"/>
        /// (a later era than the city's, or not unlocked yet), <see cref="MutationErrorCode.OutOfBounds"/>, <see cref="MutationErrorCode.TileOccupied"/>, <see cref="MutationErrorCode.NoFreeBuilder"/>,
        /// <see cref="MutationErrorCode.InsufficientResource"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static MutationResult PlaceBuilding(CityState city, GameContent content, BuildingTypeId typeId, int x, int y)
        {
            RequireArguments(city, content);
            // The checks run in the order of 04 section 8, and the first failure is the answer: the order decides which error a request with several
            // problems gets, and a rejection changes nothing.
            if (!content.TryGetBuilding(typeId, out BuildingDefinition? definition))
            {
                return MutationResult.Rejected(MutationErrorCode.UnknownBuildingType);
            }

            if (!CityQueries.IsUnlocked(city, content, typeId))
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingLocked);
            }

            if (x < 0 || y < 0 || x >= content.Rules.GridWidth || y >= content.Rules.GridHeight)
            {
                return MutationResult.Rejected(MutationErrorCode.OutOfBounds);
            }

            if (IsTileTaken(city, x, y))
            {
                return MutationResult.Rejected(MutationErrorCode.TileOccupied);
            }

            if (!HasFreeBuilder(city, content))
            {
                return MutationResult.Rejected(MutationErrorCode.NoFreeBuilder);
            }

            if (!city.HasItems(definition.Cost))
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientResource);
            }

            city.RemoveItems(definition.Cost);
            city.AddBuilding(typeId, x, y, checked(city.Clock + definition.BuildSeconds));
            return MutationResult.Applied(definition.Cost);
        }

        /// <summary>
        /// Starts upgrading a building to the next level. The cost is checked before anything else changes; then a running cycle (frozen or not)
        /// ends without output: its inputs come back, even above a storage cap, and its outputs are no longer reserved. Then the cost is paid
        /// and the upgrade runs from the clock for the next level's upgrade seconds. The workers stay assigned and do nothing meanwhile.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="buildingId">The building's id in the city.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.BuildingNotFound"/>, <see cref="MutationErrorCode.BuildingBusy"/>
        /// (constructing or upgrading), <see cref="MutationErrorCode.MaxLevel"/>, <see cref="MutationErrorCode.NoFreeBuilder"/>,
        /// <see cref="MutationErrorCode.InsufficientResource"/> (with the stock as it is, before any refund).
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static MutationResult UpgradeBuilding(CityState city, GameContent content, int buildingId)
        {
            RequireArguments(city, content);
            if (!city.TryGetBuilding(buildingId, out Building? building))
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingNotFound);
            }

            if (building.Phase != BuildingPhase.Idle && building.Phase != BuildingPhase.Producing)
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingBusy);
            }

            BuildingDefinition definition = CityQueries.DefinitionOf(content, building.TypeId);
            if (building.Level >= definition.MaxLevel)
            {
                return MutationResult.Rejected(MutationErrorCode.MaxLevel);
            }

            if (!HasFreeBuilder(city, content))
            {
                return MutationResult.Rejected(MutationErrorCode.NoFreeBuilder);
            }

            BuildingLevelDefinition nextLevel = definition.GetLevel(building.Level + 1);
            if (!city.HasItems(nextLevel.UpgradeCost))
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientResource);
            }

            IReadOnlyList<ItemQuantity>? refunded = null;
            if (building.Phase == BuildingPhase.Producing)
            {
                // Leaving the Producing phase releases the reservation, which is derived from the phase and never stored.
                refunded = CityQueries.RecipeOf(content, building.TypeId, building.Level).Inputs;
                city.AddItems(refunded);
            }

            city.RemoveItems(nextLevel.UpgradeCost);
            building.StartUpgrade(city.Clock, checked(city.Clock + nextLevel.UpgradeSeconds));
            return MutationResult.Applied(nextLevel.UpgradeCost, refunded);
        }

        /// <summary>
        /// Sets the number of workers on a producer (an absolute count; 0 pauses it). On a running cycle the progress so far is kept: the labor
        /// done with the old count is taken off, a new labor segment starts at the clock, and the cycle ends after
        /// <c>CeilDiv(laborRemaining, workers)</c> seconds, or is frozen (no end) with 0 workers (<c>04</c> section 3). On an idle or upgrading
        /// building only the count changes.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="buildingId">The building's id in the city.</param>
        /// <param name="workers">The new worker count.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.BuildingNotFound"/>, <see cref="MutationErrorCode.InvalidArgument"/> (the
        /// building has no recipe: a house), <see cref="MutationErrorCode.BuildingBusy"/> (constructing), <see cref="MutationErrorCode.WorkerLimit"/>
        /// (negative or above the level's max), <see cref="MutationErrorCode.InsufficientWorkers"/> (the increase is more than the free workers).
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static MutationResult AssignWorkers(CityState city, GameContent content, int buildingId, int workers)
        {
            RequireArguments(city, content);
            if (!city.TryGetBuilding(buildingId, out Building? building))
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingNotFound);
            }

            BuildingLevelDefinition level = CityQueries.DefinitionOf(content, building.TypeId).GetLevel(building.Level);
            if (level.Recipe is null)
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            if (building.Phase == BuildingPhase.Constructing)
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingBusy);
            }

            if (workers < 0 || workers > level.MaxWorkers)
            {
                return MutationResult.Rejected(MutationErrorCode.WorkerLimit);
            }

            // Fewer workers than now always fit: the increase is then negative.
            if ((long)workers - building.AssignedWorkers > CityQueries.FreeWorkers(city, content))
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientWorkers);
            }

            building.AssignWorkers(workers, city.Clock);
            return MutationResult.Ok;
        }

        /// <summary>
        /// Starts a research node: pays its full cost up front and fills the research slot from the clock for the node's duration. Research needs
        /// no workers; its effects apply when it completes (<c>04</c> section 5).
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="researchId">The research node.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.UnknownResearch"/>, <see cref="MutationErrorCode.ResearchCompleted"/>,
        /// <see cref="MutationErrorCode.ResearchInProgress"/>, <see cref="MutationErrorCode.ResearchLocked"/> (a prerequisite is missing, or the
        /// node is not of the city's era), <see cref="MutationErrorCode.InsufficientResource"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static MutationResult StartResearch(CityState city, GameContent content, ResearchId researchId)
        {
            RequireArguments(city, content);
            if (!content.TryGetResearch(researchId, out ResearchDefinition? node))
            {
                return MutationResult.Rejected(MutationErrorCode.UnknownResearch);
            }

            if (city.HasCompletedResearch(researchId))
            {
                return MutationResult.Rejected(MutationErrorCode.ResearchCompleted);
            }

            // The M0 engine holds one running research; content rule 1 keeps the content at exactly one research slot.
            if (city.ActiveResearch != null)
            {
                return MutationResult.Rejected(MutationErrorCode.ResearchInProgress);
            }

            if (node.Era != city.Era || !HasCompletedAll(city, node))
            {
                return MutationResult.Rejected(MutationErrorCode.ResearchLocked);
            }

            if (!city.HasItems(node.Cost))
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientResource);
            }

            city.RemoveItems(node.Cost);
            city.StartResearch(researchId, checked(city.Clock + node.DurationSeconds));
            return MutationResult.Applied(node.Cost);
        }

        /// <summary>Throws for a null city or content: a programmer error, unlike the business rules, which return a rejection.</summary>
        private static void RequireArguments(CityState city, GameContent content)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }
        }

        /// <summary>Whether any building, in any phase, stands on the tile; one building per tile (<c>04</c> section 8, placement).</summary>
        private static bool IsTileTaken(CityState city, int x, int y)
        {
            foreach (Building building in city.Buildings)
            {
                if (building.X == x && building.Y == y)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a builder slot is free for one more construction or upgrade (<see cref="CityQueries.BuildersBusy"/> below the content's slots).</summary>
        private static bool HasFreeBuilder(CityState city, GameContent content) => CityQueries.BuildersBusy(city) < content.Rules.BuilderSlots;

        /// <summary>Whether the city has completed every prerequisite of a research node.</summary>
        private static bool HasCompletedAll(CityState city, ResearchDefinition node)
        {
            foreach (ResearchId prerequisite in node.Prerequisites)
            {
                if (!city.HasCompletedResearch(prerequisite))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
