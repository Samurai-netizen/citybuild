using System.Text;
using CityBuilder.Api.Errors;
using CityBuilder.Application.Commands;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CityBuilder.Api.Endpoints;

/// <summary>
/// Turns what <see cref="CommandPipeline"/> did with a command into the HTTP answer of every command endpoint (<c>05</c>): a success or a
/// replay is 200 with the snapshot text the pipeline returned (on a replay the stored one, byte for byte); anything else is RFC 9457 Problem
/// Details (<see cref="ApiProblem"/>) with the code's status and the extensions <c>code</c> and <c>requestId</c>. The codes are the
/// <see cref="ErrorCatalogue"/>'s, so the next commands only add their endpoint.
/// </summary>
public static class CommandResponses
{
    /// <summary>The HTTP answer of a command's result.</summary>
    /// <param name="result">What the pipeline did.</param>
    /// <param name="requestId">The command's request id, echoed in an error unless it is all zeros.</param>
    /// <returns>200 with the snapshot, or the Problem Details of the error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An outcome this mapping doesn't know.</exception>
    public static Results<ContentHttpResult, ProblemHttpResult> ToHttpResult(CommandResult result, Guid requestId)
    {
        ArgumentNullException.ThrowIfNull(result);

        // The snapshot is already JSON text: written as it is, so a replay is the stored response byte for byte (decision 041).
        return ErrorOf(result) is { } error
            ? ApiProblem.Of(error, requestId)
            : TypedResults.Text(result.ResponseJson, "application/json", Encoding.UTF8);
    }

    /// <summary>The catalogue's error of a command that didn't succeed, for its answer and its log line; none for a success or a replay.</summary>
    /// <param name="result">What the pipeline did.</param>
    /// <returns>The error, or <see langword="null"/> when the answer is the snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An outcome this mapping doesn't know.</exception>
    public static ApiError? ErrorOf(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Outcome switch
        {
            CommandOutcome.Succeeded or CommandOutcome.Replayed => null,
            CommandOutcome.Rejected => ErrorCatalogue.Of(result.ErrorCode!.Value),
            CommandOutcome.InvalidArgument => ErrorCatalogue.InvalidArgument.WithDetail(result.Detail!),
            CommandOutcome.IdempotencyConflict => ErrorCatalogue.IdempotencyConflict,
            _ => throw new InvalidOperationException($"No HTTP answer for the command outcome {result.Outcome}."),
        };
    }
}
