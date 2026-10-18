#nullable enable

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// Why the engine rejected a mutation: the business-rule codes of <c>core-instructions/05-api-contract.md</c> that the engine decides. Each
    /// value's doc names its API code; the server's error catalogue maps the value to that code and its HTTP status. Codes the engine never
    /// decides (authentication, idempotency, rate limits, concurrency) are not here.
    /// </summary>
    public enum MutationErrorCode
    {
        /// <summary><c>INVALID_ARGUMENT</c>: workers assigned to a building without a recipe (a house).</summary>
        InvalidArgument,

        /// <summary><c>UNKNOWN_BUILDING_TYPE</c>: the building type is not in the content.</summary>
        UnknownBuildingType,

        /// <summary>
        /// <c>BUILDING_LOCKED</c>: the building type belongs to a later era than the city's, or it is not available at the start and the research
        /// that unlocks it is not completed.
        /// </summary>
        BuildingLocked,

        /// <summary><c>OUT_OF_BOUNDS</c>: the tile is outside the grid.</summary>
        OutOfBounds,

        /// <summary><c>TILE_OCCUPIED</c>: a building already stands on the tile.</summary>
        TileOccupied,

        /// <summary><c>INSUFFICIENT_RESOURCE</c>: the city can't pay the cost.</summary>
        InsufficientResource,

        /// <summary><c>INSUFFICIENT_WORKERS</c>: the city has too few free workers for the requested count.</summary>
        InsufficientWorkers,

        /// <summary><c>WORKER_LIMIT</c>: the requested count is negative or above the max workers of the building's level.</summary>
        WorkerLimit,

        /// <summary><c>NO_FREE_BUILDER</c>: every builder slot is busy with a construction or an upgrade.</summary>
        NoFreeBuilder,

        /// <summary><c>BUILDING_NOT_FOUND</c>: the city has no building with the id.</summary>
        BuildingNotFound,

        /// <summary><c>BUILDING_BUSY</c>: an upgrade of a building that is constructing or upgrading, or workers for one that is constructing.</summary>
        BuildingBusy,

        /// <summary><c>MAX_LEVEL</c>: the building is already at its type's max level.</summary>
        MaxLevel,

        /// <summary><c>UNKNOWN_RESEARCH</c>: the research node is not in the content.</summary>
        UnknownResearch,

        /// <summary><c>RESEARCH_LOCKED</c>: a prerequisite is not completed, or the node belongs to another era than the city's.</summary>
        ResearchLocked,

        /// <summary><c>RESEARCH_IN_PROGRESS</c>: the research slot is busy.</summary>
        ResearchInProgress,

        /// <summary><c>RESEARCH_COMPLETED</c>: the node is already researched.</summary>
        ResearchCompleted,
    }
}
