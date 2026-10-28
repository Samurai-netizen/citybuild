using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace CityBuilder.Api.Http;

/// <summary>
/// Keeps huge bodies out (core-instructions/07-security-model.md, huge or malformed bodies): no request body larger than
/// <see cref="RequestLimitOptions.MaxBodyBytes"/> is read. Two parts: Kestrel's own limit, which stops reading any body at the limit, also
/// one without a <c>Content-Length</c>; and a check of the <c>Content-Length</c> before anything else runs, which answers
/// <c>INVALID_ARGUMENT</c> (400, the catalogue's code for a body too large, <c>05</c>) as Problem Details without reading the body,
/// checking the token or opening a transaction. A body Kestrel stops mid-way gets the framework's own answer until the error catalogue
/// maps it (M0-34).
/// </summary>
public static class RequestBodyLimit
{
    /// <summary>The stable error code of the answer (<c>05</c>).</summary>
    public const string Code = "INVALID_ARGUMENT";

    /// <summary>
    /// Registers <see cref="RequestLimitOptions"/> (validated on start: <see cref="RequestLimitOptions.MaxBodyBytes"/> in range) and sets
    /// Kestrel's body limit to it, in place of Kestrel's default of about 30 MB.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration; the section <see cref="RequestLimitOptions.SectionName"/> is bound.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRequestBodyLimit(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RequestLimitOptions>()
            .Bind(configuration.GetSection(RequestLimitOptions.SectionName))
            .Validate(options => options.MaxBodyBytes is >= RequestLimitOptions.MinMaxBodyBytes and <= RequestLimitOptions.MaxMaxBodyBytes,
                $"RequestLimits:MaxBodyBytes must be between {RequestLimitOptions.MinMaxBodyBytes} and {RequestLimitOptions.MaxMaxBodyBytes}.")
            .ValidateOnStart();
        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<RequestLimitOptions>>((kestrel, limits) => kestrel.Limits.MaxRequestBodySize = limits.Value.MaxBodyBytes);
        return services;
    }

    /// <summary>
    /// Adds the <c>Content-Length</c> check as the first step of the request pipeline: a request that announces a body larger than the
    /// limit gets the 400 answer and goes no further.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseRequestBodyLimit(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        int maxBodyBytes = app.ApplicationServices.GetRequiredService<IOptions<RequestLimitOptions>>().Value.MaxBodyBytes;
        return app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > maxBodyBytes)
            {
                await Results.Problem(
                        type: "urn:citybuilder:error:invalid-argument",
                        title: "Invalid argument",
                        statusCode: StatusCodes.Status400BadRequest,
                        detail: $"The body must not be larger than {maxBodyBytes} bytes.",
                        extensions: new Dictionary<string, object?> { ["code"] = Code })
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });
    }
}
