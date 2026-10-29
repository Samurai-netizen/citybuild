using CityBuilder.Api.Errors;
using Microsoft.Extensions.Options;

namespace CityBuilder.Api.Http;

/// <summary>
/// The answer to a request the framework ended with a status and no body (core-instructions/05-api-contract.md, errors): a path no endpoint
/// has (404), a method an endpoint doesn't take (405), a body that isn't JSON (415). They get a Problem Details body with <c>type</c>,
/// <c>title</c>, <c>status</c> and <c>traceId</c> and no <c>code</c>, because the catalogue has none for them. One status is rewritten: a body
/// Kestrel stops at <see cref="RequestLimitOptions.MaxBodyBytes"/> (a chunked one, with no <c>Content-Length</c> to check first) ends the
/// request with 413 inside the binding, where no exception reaches the handler; the contract says a body that is too large is
/// <c>INVALID_ARGUMENT</c> (400), as the <c>Content-Length</c> check answers it, so this answers the same.
/// </summary>
public static class StatusCodeProblems
{
    /// <summary>Adds the status code pages with the answers above, behind the exception handler and in front of the body limit.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseStatusCodeProblems(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        int maxBodyBytes = app.ApplicationServices.GetRequiredService<IOptions<RequestLimitOptions>>().Value.MaxBodyBytes;
        return app.UseStatusCodePages(async context =>
        {
            HttpContext http = context.HttpContext;
            if (http.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                await ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail($"The body must not be larger than {maxBodyBytes} bytes.")).ExecuteAsync(http);
                return;
            }

            await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
                new ProblemDetailsContext { HttpContext = http, ProblemDetails = { Status = http.Response.StatusCode } });
        });
    }
}
