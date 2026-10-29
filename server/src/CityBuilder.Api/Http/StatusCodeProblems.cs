using CityBuilder.Api.Errors;
using Microsoft.Extensions.Options;

namespace CityBuilder.Api.Http;

/// <summary>
/// The answer to a request the framework ended with a status and no body (core-instructions/05-api-contract.md, errors): a path no endpoint
/// has (404 <c>NOT_FOUND</c>), a method an endpoint doesn't take (405 <c>METHOD_NOT_ALLOWED</c>), a body that isn't JSON (415
/// <c>UNSUPPORTED_MEDIA_TYPE</c>), each the catalogue's Problem Details. One status is rewritten: a body Kestrel stops at
/// <see cref="RequestLimitOptions.MaxBodyBytes"/> (a chunked one, with no <c>Content-Length</c> to check first) ends the request with 413
/// inside the binding, where no exception reaches the handler; the contract says a body that is too large is <c>INVALID_ARGUMENT</c> (400), as
/// the <c>Content-Length</c> check answers it, so this answers the same. Any other empty error status keeps the framework's Problem Details
/// without a <c>code</c>; the pipeline makes none today.
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
            ApiError? error = http.Response.StatusCode switch
            {
                StatusCodes.Status404NotFound => ErrorCatalogue.NotFound,
                StatusCodes.Status405MethodNotAllowed => ErrorCatalogue.MethodNotAllowed,
                StatusCodes.Status415UnsupportedMediaType => ErrorCatalogue.UnsupportedMediaType,
                StatusCodes.Status413PayloadTooLarge =>
                    ErrorCatalogue.InvalidArgument.WithDetail($"The body must not be larger than {maxBodyBytes} bytes."),
                _ => null,
            };
            if (error is not null)
            {
                await ApiProblem.Of(error).ExecuteAsync(http);
                return;
            }

            await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
                new ProblemDetailsContext { HttpContext = http, ProblemDetails = { Status = http.Response.StatusCode } });
        });
    }
}
