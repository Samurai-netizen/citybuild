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
    /// An applied one names the coins it paid and the inputs it refunded (<see cref="MutationResult.Spent"/>, <see cref="MutationResult.Refunded"/>).
    /// Only coins are paid at once; the goods of a cost become the project's needs, delivered by walkers (<c>04</c> section 16, CR-0008 D1).
    /// </summary>
    public static class CityRules
    {
        /// <summary>
        /// Places a building: pays the coins of the type's cost and starts construction on the tile: from the clock for the type's build
        /// seconds when the cost has no goods, otherwise as a construction site that waits for them (<c>04</c> section 16). The building gets
        /// the next id, level 1, no workers and an empty store. Placing never needs workers.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="typeId">The building type.</param>
        /// <param name="x">The tile column.</param>
        /// <param name="y">The tile row.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.UnknownBuildingType"/>, <see cref="MutationErrorCode.BuildingLocked"/>
        /// (a later era than the city's, or not unlocked yet), <see cref="MutationErrorCode.OutOfBounds"/>, <see cref="MutationErrorCode.TileOccupied"/>
        /// (a building or a road), <see cref="MutationErrorCode.BuildingLimit"/> (the type's <c>maxPerCity</c>),
        /// <see cref="MutationErrorCode.NoFreeBuilder"/>, <see cref="MutationErrorCode.InsufficientResource"/> (the cost's coins).
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

            if (definition.MaxPerCity.HasValue && CityQueries.CountOfType(city, typeId) >= definition.MaxPerCity.Value)
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingLimit);
            }

            if (!HasFreeBuilder(city, content))
            {
                return MutationResult.Rejected(MutationErrorCode.NoFreeBuilder);
            }

            long coins = Costs.CoinsOf(content, definition.Cost);
            if (city.Purse < coins)
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientResource);
            }

            city.PayFromPurse(coins);
            IReadOnlyList<ItemQuantity> goods = Costs.GoodsOf(content, definition.Cost);
            if (goods.Count == 0)
            {
                city.AddBuilding(typeId, x, y, checked(city.Clock + definition.BuildSeconds));
            }
            else
            {
                city.AddConstructionSite(typeId, x, y, goods);
            }

            return MutationResult.Applied(Costs.AsSpent(content, coins));
        }

        /// <summary>
        /// Orders the upgrade of a building to the next level: pays the coins of the next level's upgrade cost and takes a builder slot. Without
        /// goods in the cost the upgrade starts at once: a running cycle (frozen or not) ends without output, its inputs come back into the
        /// building's store, even above its limit, and the upgrade runs from the clock for the next level's upgrade seconds. With goods the
        /// building keeps working with an upgrade waiting for them, which starts the same way when the last one arrives (CR-0008 D12). The
        /// workers stay assigned and do nothing while it upgrades.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="buildingId">The building's id in the city.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.BuildingNotFound"/>, <see cref="MutationErrorCode.BuildingBusy"/>
        /// (constructing, upgrading, or an upgrade already waiting for goods), <see cref="MutationErrorCode.MaxLevel"/>,
        /// <see cref="MutationErrorCode.NoFreeBuilder"/>, <see cref="MutationErrorCode.InsufficientResource"/> (the upgrade cost's coins).
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static MutationResult UpgradeBuilding(CityState city, GameContent content, int buildingId)
        {
            RequireArguments(city, content);
            if (!city.TryGetBuilding(buildingId, out Building? building))
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingNotFound);
            }

            if ((building.Phase != BuildingPhase.Idle && building.Phase != BuildingPhase.Producing) || building.HasUpgradeWaiting)
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
            long coins = Costs.CoinsOf(content, nextLevel.UpgradeCost);
            if (city.Purse < coins)
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientResource);
            }

            city.PayFromPurse(coins);
            IReadOnlyList<ItemQuantity> goods = Costs.GoodsOf(content, nextLevel.UpgradeCost);
            IReadOnlyList<ItemQuantity>? refunded = null;
            if (goods.Count == 0)
            {
                refunded = ProjectRules.StartUpgrade(city, content, building, city.Clock);
            }
            else
            {
                building.WaitForUpgrade(goods);
            }

            return MutationResult.Applied(Costs.AsSpent(content, coins), refunded);
        }

        /// <summary>
        /// Sets the number of workers on a producer or a courier station (an absolute count; 0 pauses it). On a running cycle the progress so far is kept: the labor
        /// done with the old count is taken off, a new labor segment starts at the clock, and the cycle ends after
        /// <c>CeilDiv(laborRemaining, workers)</c> seconds, or is frozen (no end) with 0 workers (<c>04</c> section 3). On an idle or upgrading
        /// building only the count changes. On a courier station each new worker is a courier at the station and each removed one the highest
        /// slot's courier, whose load goes back (<c>04</c> section 19, <see cref="CourierRules"/>).
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="buildingId">The building's id in the city.</param>
        /// <param name="workers">The new worker count.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.BuildingNotFound"/>, <see cref="MutationErrorCode.InvalidArgument"/> (neither
        /// a producer nor a courier station: a house, a warehouse, the science building), <see cref="MutationErrorCode.BuildingBusy"/> (constructing), <see cref="MutationErrorCode.WorkerLimit"/>
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

            BuildingDefinition definition = CityQueries.DefinitionOf(content, building.TypeId);
            if (definition.Kind != BuildingKind.Producer && definition.Kind != BuildingKind.CourierStation)
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            BuildingLevelDefinition level = definition.GetLevel(building.Level);

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

            int before = building.AssignedWorkers;
            building.AssignWorkers(workers, city.Clock);
            if (definition.Kind == BuildingKind.CourierStation)
            {
                CourierRules.StaffStation(city, level, building, before);
            }

            return MutationResult.Ok;
        }

        /// <summary>
        /// Starts a research node at the city's science building: pays the coins of its cost and fills the research slot. Without goods in the
        /// cost its timer runs from the clock for the node's duration; otherwise it waits until its goods are delivered to the science building,
        /// and the timer runs from then (<c>04</c> sections 5 and 16). Research needs no workers; its effects apply when it completes.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="researchId">The research node.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.UnknownResearch"/>, <see cref="MutationErrorCode.ResearchCompleted"/>,
        /// <see cref="MutationErrorCode.ResearchInProgress"/>, <see cref="MutationErrorCode.ResearchLocked"/> (a prerequisite is missing, or the
        /// node is not of the city's era), <see cref="MutationErrorCode.NoScienceBuilding"/> (no completed science building),
        /// <see cref="MutationErrorCode.InsufficientResource"/> (the cost's coins).
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

            if (CityQueries.ScienceBuilding(city, content) is null)
            {
                return MutationResult.Rejected(MutationErrorCode.NoScienceBuilding);
            }

            long coins = Costs.CoinsOf(content, node.Cost);
            if (city.Purse < coins)
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientResource);
            }

            city.PayFromPurse(coins);
            IReadOnlyList<ItemQuantity> goods = Costs.GoodsOf(content, node.Cost);
            if (goods.Count == 0)
            {
                city.StartResearch(researchId, checked(city.Clock + node.DurationSeconds));
            }
            else
            {
                city.StartResearchWaiting(researchId, goods);
            }

            return MutationResult.Applied(Costs.AsSpent(content, coins));
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

        /// <summary>
        /// Whether a building (in any phase) or a road lies on the tile: one building per tile, and never on a road (<c>04</c> section 8,
        /// placement; CR-0008).
        /// </summary>
        private static bool IsTileTaken(CityState city, int x, int y) => CityQueries.TileAt(city, x, y) != TileContent.Ground;

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
