namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The error codes a request can fail with. The first block is the server's catalogue, member for member the table of
    /// <c>05</c> Errors (a repository test holds the two equal); the <c>CLIENT_</c> block names failures that happen before any
    /// server answer exists. Programs compare <see cref="ApiError.Code"/> with these constants.
    /// </summary>
    public static class ApiErrorCodes
    {
        /// <summary>401: a missing, invalid or expired token; log in again.</summary>
        public const string AuthenticationRequired = "AUTHENTICATION_REQUIRED";

        /// <summary>400: a request that can't be read, or a value no city could make valid.</summary>
        public const string InvalidArgument = "INVALID_ARGUMENT";

        /// <summary>422: the building type id is not in the content.</summary>
        public const string UnknownBuildingType = "UNKNOWN_BUILDING_TYPE";

        /// <summary>422: the type is of a later era than the city's or its research is not done.</summary>
        public const string BuildingLocked = "BUILDING_LOCKED";

        /// <summary>422: the coordinates are past the grid's width or height.</summary>
        public const string OutOfBounds = "OUT_OF_BOUNDS";

        /// <summary>409: the tile already has a building.</summary>
        public const string TileOccupied = "TILE_OCCUPIED";

        /// <summary>422: the city can't pay the cost.</summary>
        public const string InsufficientResource = "INSUFFICIENT_RESOURCE";

        /// <summary>422: not enough free workers for the assignment.</summary>
        public const string InsufficientWorkers = "INSUFFICIENT_WORKERS";

        /// <summary>422: more workers than the building's level takes.</summary>
        public const string WorkerLimit = "WORKER_LIMIT";

        /// <summary>409: every builder slot is busy.</summary>
        public const string NoFreeBuilder = "NO_FREE_BUILDER";

        /// <summary>404: no such building id in the player's own city.</summary>
        public const string BuildingNotFound = "BUILDING_NOT_FOUND";

        /// <summary>409: the building is constructing or upgrading.</summary>
        public const string BuildingBusy = "BUILDING_BUSY";

        /// <summary>422: the building is at its highest level.</summary>
        public const string MaxLevel = "MAX_LEVEL";

        /// <summary>422: the research id is not in the content.</summary>
        public const string UnknownResearch = "UNKNOWN_RESEARCH";

        /// <summary>422: a prerequisite is missing or the node belongs to another era.</summary>
        public const string ResearchLocked = "RESEARCH_LOCKED";

        /// <summary>409: the research slot is busy.</summary>
        public const string ResearchInProgress = "RESEARCH_IN_PROGRESS";

        /// <summary>409: the research is already completed.</summary>
        public const string ResearchCompleted = "RESEARCH_COMPLETED";

        /// <summary>409: the same request id was sent with a different command.</summary>
        public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";

        /// <summary>429: too many requests; wait <c>Retry-After</c> seconds.</summary>
        public const string RateLimited = "RATE_LIMITED";

        /// <summary>409: a lock timeout or a version mismatch; safe to retry at once with the same request id.</summary>
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";

        /// <summary>500: anything unexpected on the server.</summary>
        public const string InternalError = "INTERNAL_ERROR";

        /// <summary>404: a path no endpoint has.</summary>
        public const string NotFound = "NOT_FOUND";

        /// <summary>405: a method the endpoint doesn't take.</summary>
        public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";

        /// <summary>415: a body that isn't <c>application/json</c>.</summary>
        public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";

        /// <summary>No HTTP answer arrived: no connection, a refused or reset connection, a DNS failure. <see cref="ApiError.Status"/> is 0. Safe to retry.</summary>
        public const string ClientNetworkError = "CLIENT_NETWORK_ERROR";

        /// <summary>The server did not answer within the client's timeout. <see cref="ApiError.Status"/> is 0. Safe to retry with the same request id: the command may or may not have run.</summary>
        public const string ClientTimeout = "CLIENT_TIMEOUT";

        /// <summary>An answer arrived but is not what <c>05</c> promises: a body that is not JSON (a proxy's 502 page), JSON of the wrong shape, a missing member, or an error answer without a <c>code</c>. <see cref="ApiError.Status"/> is the HTTP status.</summary>
        public const string ClientInvalidResponse = "CLIENT_INVALID_RESPONSE";
    }
}
