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
        /// <summary>
        /// <c>INVALID_ARGUMENT</c>: workers assigned to a building that takes none (a house, a warehouse, the science building); a trip whose
        /// source is its target, that carries the currency, or whose amount names a preset the content doesn't have.
        /// </summary>
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

        /// <summary><c>ROAD_NOT_FOUND</c>: a tile of a road command has no road to upgrade or remove (CR-0008).</summary>
        RoadNotFound,

        /// <summary><c>BUILDING_LIMIT</c>: the city already has the most buildings of the type its <c>maxPerCity</c> allows (CR-0008: the science building).</summary>
        BuildingLimit,

        /// <summary><c>NO_SCIENCE_BUILDING</c>: research needs a completed science building, and the city has none (CR-0008 D11).</summary>
        NoScienceBuilding,

        /// <summary><c>UNKNOWN_ITEM</c>: the item of a trip or a route is not in the content (CR-0008).</summary>
        UnknownItem,

        /// <summary><c>AMOUNT_MODE_LOCKED</c>: a percentage or an exact amount before the research that unlocks its mode (<c>04</c> section 19).</summary>
        AmountModeLocked,

        /// <summary><c>ITEM_NOT_ACCEPTED</c>: the trip's target neither needs the item for a project nor has it in its store (<c>04</c> section 8).</summary>
        ItemNotAccepted,

        /// <summary><c>NOTHING_TO_CARRY</c>: the trip's amount gives nothing, because the source's store has none of the item (<c>04</c> section 18).</summary>
        NothingToCarry,

        /// <summary>
        /// <c>INVALID_ROUTE</c>: a route's slot above the station level's couriers, one stop or more than the content's most, a stop with more
        /// than the most actions, or two consecutive stops (the last and the first included) at one building (<c>04</c> section 8).
        /// </summary>
        InvalidRoute,

        /// <summary>
        /// <c>NO_FREE_COURIER</c>: the city has a built courier station, but every courier has a route, so none can take a delivery
        /// (<c>04</c> section 18, CR-0008 D14).
        /// </summary>
        NoFreeCourier,

        /// <summary><c>DELIVERY_QUEUE_FULL</c>: the delivery's lines don't fit in the chosen courier's queue (<c>maxDeliveryLines</c>, CR-0008 D17).</summary>
        DeliveryQueueFull,

        /// <summary><c>ROUTES_LOCKED</c>: a route before the city has built the content's route building (the Scriptorium, CR-0008 D15).</summary>
        RoutesLocked,

        /// <summary><c>COURIER_BUSY</c>: a route for a courier that still has delivery lines; a courier has one or the other (CR-0008 D19).</summary>
        CourierBusy,

        /// <summary><c>DELIVERY_NOT_FOUND</c>: the delivery line to cancel is not one of that courier's lines (<c>04</c> section 8).</summary>
        DeliveryNotFound,
    }
}
