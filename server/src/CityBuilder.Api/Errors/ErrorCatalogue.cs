using CityBuilder.Simulation.Rules;

namespace CityBuilder.Api.Errors;

/// <summary>
/// The error catalogue: every code of the table in <c>core-instructions/05-api-contract.md</c> with its HTTP status and its texts, in one
/// place. Nothing else in the server writes a code, a status or a title; <see cref="ApiProblem"/> turns an <see cref="ApiError"/> into the
/// answer. A new code is one member here and in <see cref="All"/>, one row in the table test and one row in <c>05</c> (recipe "New error code" in
/// <c>core-instructions/19-change-recipes.md</c>). The engine's own codes are mapped by <see cref="Of"/>.
/// </summary>
public static class ErrorCatalogue
{
    /// <summary>Missing, malformed, expired or wrongly signed token; the answer never says which.</summary>
    public static readonly ApiError AuthenticationRequired = new(
        "AUTHENTICATION_REQUIRED", StatusCodes.Status401Unauthorized, "Authentication required",
        "Send a valid access token as 'Authorization: Bearer <accessToken>'; log in again if it has expired.");

    /// <summary>A request no city could make valid: broken JSON, a field the contract doesn't have, a body too large, a bad value.</summary>
    public static readonly ApiError InvalidArgument = new(
        "INVALID_ARGUMENT", StatusCodes.Status400BadRequest, "Invalid argument",
        "The request is malformed or holds a value that is never valid.");

    /// <summary>A building type id the content doesn't have.</summary>
    public static readonly ApiError UnknownBuildingType = new(
        "UNKNOWN_BUILDING_TYPE", StatusCodes.Status422UnprocessableEntity, "Unknown building type", "The building type is not in the content.");

    /// <summary>A building type of a later era than the city's, or not unlocked by research yet.</summary>
    public static readonly ApiError BuildingLocked = new(
        "BUILDING_LOCKED", StatusCodes.Status422UnprocessableEntity, "Building locked",
        "The building type belongs to a later era, or the research that unlocks it is not completed.");

    /// <summary>Coordinates past the grid's width or height.</summary>
    public static readonly ApiError OutOfBounds = new(
        "OUT_OF_BOUNDS", StatusCodes.Status422UnprocessableEntity, "Out of bounds", "The tile is outside the grid.");

    /// <summary>The tile already has a building.</summary>
    public static readonly ApiError TileOccupied = new(
        "TILE_OCCUPIED", StatusCodes.Status409Conflict, "Tile occupied", "A building already stands on the tile.");

    /// <summary>The cost is not affordable.</summary>
    public static readonly ApiError InsufficientResource = new(
        "INSUFFICIENT_RESOURCE", StatusCodes.Status422UnprocessableEntity, "Not enough resources", "The city can't pay the cost.");

    /// <summary>Not enough free workers for the requested assignment.</summary>
    public static readonly ApiError InsufficientWorkers = new(
        "INSUFFICIENT_WORKERS", StatusCodes.Status422UnprocessableEntity, "Not enough workers",
        "The city has too few free workers for the requested count.");

    /// <summary>The requested workers are above the building's max for its level.</summary>
    public static readonly ApiError WorkerLimit = new(
        "WORKER_LIMIT", StatusCodes.Status422UnprocessableEntity, "Worker limit",
        "The requested count is above the max workers of the building's level.");

    /// <summary>All builder slots are busy.</summary>
    public static readonly ApiError NoFreeBuilder = new(
        "NO_FREE_BUILDER", StatusCodes.Status409Conflict, "No free builder", "Every builder slot is busy.");

    /// <summary>The id is not in the caller's city (also for other players' ids).</summary>
    public static readonly ApiError BuildingNotFound = new(
        "BUILDING_NOT_FOUND", StatusCodes.Status404NotFound, "Building not found", "Your city has no building with this id.");

    /// <summary>The building is being built or upgraded.</summary>
    public static readonly ApiError BuildingBusy = new(
        "BUILDING_BUSY", StatusCodes.Status409Conflict, "Building busy", "The building is being built or upgraded.");

    /// <summary>The building is at its type's max level.</summary>
    public static readonly ApiError MaxLevel = new(
        "MAX_LEVEL", StatusCodes.Status422UnprocessableEntity, "Max level", "The building is already at its type's max level.");

    /// <summary>A research id the content doesn't have.</summary>
    public static readonly ApiError UnknownResearch = new(
        "UNKNOWN_RESEARCH", StatusCodes.Status422UnprocessableEntity, "Unknown research", "The research is not in the content.");

    /// <summary>A prerequisite is not completed, or the node belongs to another era.</summary>
    public static readonly ApiError ResearchLocked = new(
        "RESEARCH_LOCKED", StatusCodes.Status422UnprocessableEntity, "Research locked",
        "A prerequisite is not completed, or the research belongs to another era.");

    /// <summary>The research slot is busy.</summary>
    public static readonly ApiError ResearchInProgress = new(
        "RESEARCH_IN_PROGRESS", StatusCodes.Status409Conflict, "Research in progress", "The research slot is busy.");

    /// <summary>The research is already completed.</summary>
    public static readonly ApiError ResearchCompleted = new(
        "RESEARCH_COMPLETED", StatusCodes.Status409Conflict, "Research completed", "The research is already completed.");

    /// <summary>A request id that was used before with another command or payload.</summary>
    public static readonly ApiError IdempotencyConflict = new(
        "IDEMPOTENCY_CONFLICT", StatusCodes.Status409Conflict, "Request id already used",
        "This requestId was used before with another command or payload; a new command needs a new requestId.");

    /// <summary>Too many requests from this player (or, for the dev login, from this address).</summary>
    public static readonly ApiError RateLimited = new(
        "RATE_LIMITED", StatusCodes.Status429TooManyRequests, "Too many requests",
        "Too many requests; wait for the number of seconds in the Retry-After header and try again.");

    /// <summary>The city's lock wasn't granted in time, or the city changed without it; nothing was committed.</summary>
    public static readonly ApiError ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT", StatusCodes.Status409Conflict, "Request conflict",
        "Another request got in the way of this one and nothing was committed; send it again with the same requestId.");

    /// <summary>Anything unexpected; the answer says nothing about the cause outside Development.</summary>
    public static readonly ApiError InternalError = new(
        "INTERNAL_ERROR", StatusCodes.Status500InternalServerError, "Internal error", "The server could not handle the request.");

    /// <summary>A path no endpoint has (also the dev login outside Development).</summary>
    public static readonly ApiError NotFound = new(
        "NOT_FOUND", StatusCodes.Status404NotFound, "Not found", "The API has no endpoint at this path.");

    /// <summary>A method the endpoint doesn't take.</summary>
    public static readonly ApiError MethodNotAllowed = new(
        "METHOD_NOT_ALLOWED", StatusCodes.Status405MethodNotAllowed, "Method not allowed", "The endpoint doesn't take this HTTP method.");

    /// <summary>A body that isn't <c>application/json</c>.</summary>
    public static readonly ApiError UnsupportedMediaType = new(
        "UNSUPPORTED_MEDIA_TYPE", StatusCodes.Status415UnsupportedMediaType, "Unsupported media type", "Send the body as application/json.");

    /// <summary>
    /// Every code of the catalogue, in the order of <c>05</c>'s table. A test checks that this list holds every member above once and that
    /// each has a row in the contract's table.
    /// </summary>
    public static IReadOnlyList<ApiError> All { get; } =
    [
        AuthenticationRequired, InvalidArgument, UnknownBuildingType, BuildingLocked, OutOfBounds, TileOccupied, InsufficientResource,
        InsufficientWorkers, WorkerLimit, NoFreeBuilder, BuildingNotFound, BuildingBusy, MaxLevel, UnknownResearch, ResearchLocked,
        ResearchInProgress, ResearchCompleted, IdempotencyConflict, RateLimited, ConcurrencyConflict, InternalError, NotFound, MethodNotAllowed,
        UnsupportedMediaType,
    ];

    /// <summary>The API error of an engine rejection: its code and status in <c>05</c>.</summary>
    /// <param name="code">Why the engine refused the command.</param>
    /// <returns>The catalogue's error.</returns>
    /// <exception cref="InvalidOperationException">A code this mapping doesn't know (an engine change forgot the contract).</exception>
    public static ApiError Of(MutationErrorCode code) => code switch
    {
        MutationErrorCode.InvalidArgument => InvalidArgument.WithDetail("The building takes no workers."),
        MutationErrorCode.UnknownBuildingType => UnknownBuildingType,
        MutationErrorCode.BuildingLocked => BuildingLocked,
        MutationErrorCode.OutOfBounds => OutOfBounds,
        MutationErrorCode.TileOccupied => TileOccupied,
        MutationErrorCode.InsufficientResource => InsufficientResource,
        MutationErrorCode.InsufficientWorkers => InsufficientWorkers,
        MutationErrorCode.WorkerLimit => WorkerLimit,
        MutationErrorCode.NoFreeBuilder => NoFreeBuilder,
        MutationErrorCode.BuildingNotFound => BuildingNotFound,
        MutationErrorCode.BuildingBusy => BuildingBusy,
        MutationErrorCode.MaxLevel => MaxLevel,
        MutationErrorCode.UnknownResearch => UnknownResearch,
        MutationErrorCode.ResearchLocked => ResearchLocked,
        MutationErrorCode.ResearchInProgress => ResearchInProgress,
        MutationErrorCode.ResearchCompleted => ResearchCompleted,
        _ => throw new InvalidOperationException($"No API error for the engine code {code}."),
    };
}
