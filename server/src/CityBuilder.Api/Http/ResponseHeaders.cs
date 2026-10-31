using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace CityBuilder.Api.Http;

/// <summary>
/// What every answer says about itself (core-instructions/07-security-model.md, information leakage): <c>Cache-Control: no-store</c>, because a
/// snapshot or a token is for one player and a shared cache must not keep it; <c>X-Content-Type-Options: nosniff</c>, so the JSON is never read as
/// anything else; and no <c>Server</c> header, because nothing needs to know the server's software.
/// </summary>
public static class ResponseHeaders
{
    /// <summary>Turns off Kestrel's <c>Server</c> header.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddResponseHeaders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<KestrelServerOptions>().Configure(kestrel => kestrel.AddServerHeader = false);
        return services;
    }

    /// <summary>
    /// Adds the headers to every answer. Run first in the pipeline, as the headers are set when the answer starts: the exception handler clears
    /// what was set before it, and sets a cache header of its own that this one replaces.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static IApplicationBuilder UseResponseHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use((context, next) =>
        {
            context.Response.OnStarting(
                static state =>
                {
                    IHeaderDictionary headers = ((HttpResponse)state).Headers;
                    headers.CacheControl = "no-store";
                    headers.XContentTypeOptions = "nosniff";
                    return Task.CompletedTask;
                },
                context.Response);
            return next(context);
        });
    }
}
