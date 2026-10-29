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
        return result.Outcome switch
        {
            // The snapshot is already JSON text: written as it is, so a replay is the stored response byte for byte (decision 041).
            CommandOutcome.Succeeded or CommandOutcome.Replayed => TypedResults.Text(result.ResponseJson, "application/json", Encoding.UTF8),
            CommandOutcome.Rejected => ApiProblem.Of(ErrorCatalogue.Of(result.ErrorCode!.Value), requestId),
            CommandOutcome.InvalidArgument => ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail(result.Detail!), requestId),
            CommandOutcome.IdempotencyConflict => ApiProblem.Of(ErrorCatalogue.IdempotencyConflict, requestId),
            _ => throw new InvalidOperationException($"No HTTP answer for the command outcome {result.Outcome}."),
        };
    }
}
