#nullable enable
using System;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// The player's mutations of a city (<c>core-instructions/04-simulation-model.md</c>, section 8). Each runs at the city's clock, after the
    /// caller has settled the city to now; the caller then runs a zero-length advance (M0-09), so the returned state is canonical. Each checks in
    /// the order of section 8 and returns the first failure; a rejected mutation changes nothing.
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
        /// Ok, or the first failed check: <see cref="MutationErrorCode.UnknownBuildingType"/>, <see cref="MutationErrorCode.BuildingLocked"/>,
        /// <see cref="MutationErrorCode.OutOfBounds"/>, <see cref="MutationErrorCode.TileOccupied"/>, <see cref="MutationErrorCode.NoFreeBuilder"/>,
        /// <see cref="MutationErrorCode.InsufficientResource"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static MutationResult PlaceBuilding(CityState city, GameContent content, BuildingTypeId typeId, int x, int y)
        {
            RequireArguments(city, content);
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
            return MutationResult.Ok;
        }

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

        private static bool HasFreeBuilder(CityState city, GameContent content) => CityQueries.BuildersBusy(city) < content.Rules.BuilderSlots;

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
