using System.Text;
using CityBuilder.Application.Commands;
using CityBuilder.Simulation.Rules;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CityBuilder.Api.Endpoints;

/// <summary>
/// Turns what <see cref="CommandPipeline"/> did with a command into the HTTP answer of every command endpoint (<c>05</c>): a success or a
/// replay is 200 with the snapshot text the pipeline returned (on a replay the stored one, byte for byte); anything else is RFC 9457 Problem
/// Details with the code's status and the extensions <c>code</c> and <c>requestId</c> (the framework adds <c>traceId</c>). The engine's
/// codes are all mapped here, so the next commands only add their endpoint. The error catalogue moves this mapping to one place for every
/// endpoint (M0-34).
/// </summary>
public static class CommandResponses
{
    /// <summary>The answer of a request id that was used before with another payload.</summary>
    public static readonly CommandError IdempotencyConflict = new(
        "IDEMPOTENCY_CONFLICT", StatusCodes.Status409Conflict, "Request id already used",
        "This requestId was used before with another command or payload; a new command needs a new requestId.");

    /// <summary>The answer of a command whose shape is wrong; the detail is the command's own.</summary>
    /// <param name="detail">What is wrong.</param>
    /// <returns>The error.</returns>
    public static CommandError InvalidArgument(string detail) =>
        new("INVALID_ARGUMENT", StatusCodes.Status400BadRequest, "Invalid argument", detail);

    /// <summary>The HTTP answer of a command's result.</summary>
    /// <param name="result">What the pipeline did.</param>
    /// <param name="requestId">The command's request id, echoed in an error unless it is all zeros.</param>
    /// <returns>200 with the snapshot, or the Problem Details of the error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An outcome this mapping doesn't know.</exception>
    public static Results<ContentHttpResult, ProblemHttpResult> ToHttpResult(CommandResult result, Guid requestId)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Outcome switch
        {
            // The snapshot is already JSON text: written as it is, so a replay is the stored response byte for byte (decision 041).
            CommandOutcome.Succeeded or CommandOutcome.Replayed => TypedResults.Text(result.ResponseJson, "application/json", Encoding.UTF8),
            CommandOutcome.Rejected => Problem(Of(result.ErrorCode!.Value), requestId),
            CommandOutcome.InvalidArgument => Problem(InvalidArgument(result.Detail!), requestId),
            CommandOutcome.IdempotencyConflict => Problem(IdempotencyConflict, requestId),
            _ => throw new InvalidOperationException($"No HTTP answer for the command outcome {result.Outcome}."),
        };
    }

    /// <summary>The Problem Details answer of an error.</summary>
    /// <param name="error">The error.</param>
    /// <param name="requestId">The command's request id; left out when it is all zeros (no request id was sent).</param>
    /// <returns>The answer.</returns>
    public static ProblemHttpResult Problem(CommandError error, Guid requestId)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (requestId != Guid.Empty)
        {
            extensions["requestId"] = requestId;
        }

        return TypedResults.Problem(
            detail: error.Detail, statusCode: error.Status, title: error.Title, type: error.Type, extensions: extensions);
    }

    /// <summary>The API error of an engine rejection: its code and status in <c>05</c>.</summary>
    /// <param name="code">Why the engine refused the command.</param>
    /// <returns>The error.</returns>
    /// <exception cref="InvalidOperationException">A code this mapping doesn't know (an engine change forgot the contract).</exception>
    public static CommandError Of(MutationErrorCode code) => code switch
    {
        MutationErrorCode.InvalidArgument => InvalidArgument("The building takes no workers."),
        MutationErrorCode.UnknownBuildingType => new(
            "UNKNOWN_BUILDING_TYPE", StatusCodes.Status422UnprocessableEntity, "Unknown building type", "The building type is not in the content."),
        MutationErrorCode.BuildingLocked => new(
            "BUILDING_LOCKED", StatusCodes.Status422UnprocessableEntity, "Building locked",
            "The building type belongs to a later era, or the research that unlocks it is not completed."),
        MutationErrorCode.OutOfBounds => new(
            "OUT_OF_BOUNDS", StatusCodes.Status422UnprocessableEntity, "Out of bounds", "The tile is outside the grid."),
        MutationErrorCode.TileOccupied => new(
            "TILE_OCCUPIED", StatusCodes.Status409Conflict, "Tile occupied", "A building already stands on the tile."),
        MutationErrorCode.InsufficientResource => new(
            "INSUFFICIENT_RESOURCE", StatusCodes.Status422UnprocessableEntity, "Not enough resources", "The city can't pay the cost."),
        MutationErrorCode.InsufficientWorkers => new(
            "INSUFFICIENT_WORKERS", StatusCodes.Status422UnprocessableEntity, "Not enough workers",
            "The city has too few free workers for the requested count."),
        MutationErrorCode.WorkerLimit => new(
            "WORKER_LIMIT", StatusCodes.Status422UnprocessableEntity, "Worker limit",
            "The requested count is negative or above the building's max workers."),
        MutationErrorCode.NoFreeBuilder => new(
            "NO_FREE_BUILDER", StatusCodes.Status409Conflict, "No free builder", "Every builder slot is busy."),
        MutationErrorCode.BuildingNotFound => new(
            "BUILDING_NOT_FOUND", StatusCodes.Status404NotFound, "Building not found", "Your city has no building with this id."),
        MutationErrorCode.BuildingBusy => new(
            "BUILDING_BUSY", StatusCodes.Status409Conflict, "Building busy", "The building is being built or upgraded."),
        MutationErrorCode.MaxLevel => new(
            "MAX_LEVEL", StatusCodes.Status422UnprocessableEntity, "Max level", "The building is already at its type's max level."),
        MutationErrorCode.UnknownResearch => new(
            "UNKNOWN_RESEARCH", StatusCodes.Status422UnprocessableEntity, "Unknown research", "The research is not in the content."),
        MutationErrorCode.ResearchLocked => new(
            "RESEARCH_LOCKED", StatusCodes.Status422UnprocessableEntity, "Research locked",
            "A prerequisite is not completed, or the research belongs to another era."),
        MutationErrorCode.ResearchInProgress => new(
            "RESEARCH_IN_PROGRESS", StatusCodes.Status409Conflict, "Research in progress", "The research slot is busy."),
        MutationErrorCode.ResearchCompleted => new(
            "RESEARCH_COMPLETED", StatusCodes.Status409Conflict, "Research completed", "The research is already completed."),
        _ => throw new InvalidOperationException($"No API error for the engine code {code}."),
    };
}
