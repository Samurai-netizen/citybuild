using Microsoft.AspNetCore.Http.HttpResults;

namespace CityBuilder.Api.Errors;

/// <summary>
/// The one place that writes an error answer: RFC 9457 Problem Details with the members of <c>core-instructions/05-api-contract.md</c>
/// (<c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>, the extension <c>code</c>, the extension <c>requestId</c> when the request had
/// one, and <c>traceId</c>, which the framework adds). Endpoints return it; middleware (the token check, the body limit, the rate limiter,
/// the exception handler) executes it against the request.
/// </summary>
public static class ApiProblem
{
    /// <summary>The Problem Details answer of an error.</summary>
    /// <param name="error">The error, from <see cref="ErrorCatalogue"/>.</param>
    /// <param name="requestId">The command's request id; left out when it is all zeros (the request had none).</param>
    /// <returns>The answer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public static ProblemHttpResult Of(ApiError error, Guid requestId = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (requestId != Guid.Empty)
        {
            extensions["requestId"] = requestId;
        }

        return TypedResults.Problem(
            detail: error.Detail, statusCode: error.Status, title: error.Title, type: error.Type, extensions: extensions);
    }
}
