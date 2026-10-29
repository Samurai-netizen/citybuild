using CityBuilder.Application.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Errors;

/// <summary>
/// A test-only path, <c>/test/throw?kind=…</c>, that throws what the query names: <c>internal</c> (an <see cref="InvalidOperationException"/>
/// with a message that must never reach a client outside Development), <c>conflict</c> (<see cref="ConcurrencyConflictException"/>),
/// <c>bad400</c> and <c>bad413</c> (<see cref="BadHttpRequestException"/> with that status, as the framework throws it). Unlike the other
/// test endpoints it is added <b>behind</b> the server's own pipeline, where an unmatched path ends, so the exception handler and the status
/// code pages of the real server are in front of it.
/// </summary>
internal sealed class ThrowingEndpoint : IStartupFilter
{
    /// <summary>The message of the exception <c>kind=internal</c> throws, which the tests look for in the answers.</summary>
    public const string SecretMessage = "secret detail: connection to db.internal:5432 failed";

    /// <summary>Puts the path behind the server's pipeline.</summary>
    /// <param name="next">The server's pipeline.</param>
    /// <returns>The pipeline with the path last.</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use((context, forward) =>
        {
            if (!context.Request.Path.StartsWithSegments("/test/throw", StringComparison.Ordinal))
            {
                return forward(context);
            }

            throw context.Request.Query["kind"].ToString() switch
            {
                "internal" => new InvalidOperationException(SecretMessage),
                "conflict" => new ConcurrencyConflictException(),
                "bad400" => new BadHttpRequestException("Failed to read the request body.", StatusCodes.Status400BadRequest),
                "bad413" => new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge),
                var other => new ArgumentOutOfRangeException(nameof(context), other, "Unknown kind."),
            };
        });
    };

    /// <summary>A copy of the server with this path added.</summary>
    /// <param name="factory">The server.</param>
    /// <returns>The copy; the caller disposes it.</returns>
    public static WebApplicationFactory<Program> AddTo(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(host => host.ConfigureTestServices(services => services.AddTransient<IStartupFilter, ThrowingEndpoint>()));
}
