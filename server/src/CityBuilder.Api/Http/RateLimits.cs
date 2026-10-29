using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using CityBuilder.Api.Auth;
using CityBuilder.Api.Errors;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CityBuilder.Api.Http;

/// <summary>
/// The rate limits (core-instructions/05-api-contract.md, rate limits; <c>07</c>, spam): the framework's limiter, in memory, one process.
/// Each request is counted by the limits of its endpoint's <see cref="RateLimitClass"/> (<see cref="RateLimitMetadata"/>): the commands and
/// <c>GET /city</c> per player (the token's <c>sub</c>), the dev login per remote address, so one player's spam never uses up another's
/// budget. A request over a limit is refused with <c>RATE_LIMITED</c> (429) and <c>Retry-After</c> before the body is read or a city is
/// locked. The limiter runs after the token check, so a request without a working token is 401 and counts against nothing.
/// </summary>
public static class RateLimits
{
    /// <summary>
    /// Registers <see cref="RateLimitOptions"/> (validated on start) and the limiter: a chain of fixed-window limiters, one per limit, of which
    /// each applies to the endpoints of its class only (the other ones pass), and the answer of a refusal.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration; the section <see cref="RateLimitOptions.SectionName"/> is bound.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        OptionsBuilder<RateLimitOptions> options = services.AddOptions<RateLimitOptions>().Bind(configuration.GetSection(RateLimitOptions.SectionName));
        foreach ((string name, Func<RateLimitOptions, RateRule> rule) in _rules)
        {
            options.Validate(
                all => IsValid(rule(all)),
                $"{RateLimitOptions.SectionName}:{name} must have a Limit between 1 and {RateLimitOptions.MaxLimit} and WindowSeconds between 1 and {RateLimitOptions.MaxWindowSeconds}.");
        }

        options.ValidateOnStart();
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitOptions>>((limiter, limits) =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.GlobalLimiter = Chain(limits.Value);
            limiter.OnRejected = WriteRejectedAsync;
        });
        return services;
    }

    /// <summary>Adds the limiter to the request pipeline; after authentication and authorization, which it needs for the player.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseRateLimits(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Read here, as for the body limit, so that a setting out of range stops the start the same way every time.
        _ = app.ApplicationServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        return app.UseRateLimiter();
    }

    /// <summary>Counts the endpoints of the builder (a route or a group) against a limit class.</summary>
    /// <typeparam name="TBuilder">The builder's type.</typeparam>
    /// <param name="builder">The route, or the route group whose routes all count.</param>
    /// <param name="limitClass">The limit.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder RequireRateLimit<TBuilder>(this TBuilder builder, RateLimitClass limitClass)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new RateLimitMetadata(limitClass));

    /// <summary>The limits by their configuration name, to validate and to name in the failure message.</summary>
    private static readonly (string Name, Func<RateLimitOptions, RateRule> Rule)[] _rules =
    [
        ("Commands:Burst", all => all.Commands.Burst),
        ("Commands:Sustained", all => all.Commands.Sustained),
        ("City", all => all.City),
        ("DevLogin", all => all.DevLogin),
    ];

    /// <summary>Whether a limit and its window are in range.</summary>
    /// <param name="rule">The limit.</param>
    /// <returns><see langword="true"/> when both are in range.</returns>
    private static bool IsValid(RateRule rule) =>
        rule.Limit is >= 1 and <= RateLimitOptions.MaxLimit && rule.WindowSeconds is >= 1 and <= RateLimitOptions.MaxWindowSeconds;

    /// <summary>The four limiters chained: a request must pass every limiter that applies to it, and the others let it through.</summary>
    /// <param name="limits">The configured limits.</param>
    /// <returns>The limiter to set as the global one.</returns>
    private static PartitionedRateLimiter<HttpContext> Chain(RateLimitOptions limits) =>
        PartitionedRateLimiter.CreateChained(
            LimiterFor(RateLimitClass.Commands, limits.Commands.Burst, PlayerKey),
            LimiterFor(RateLimitClass.Commands, limits.Commands.Sustained, PlayerKey),
            LimiterFor(RateLimitClass.City, limits.City, PlayerKey),
            LimiterFor(RateLimitClass.DevLogin, limits.DevLogin, AddressKey));

    /// <summary>A limiter that counts the requests of one class, each partition (player or address) in its own fixed window.</summary>
    /// <param name="limitClass">The class it applies to.</param>
    /// <param name="rule">The limit.</param>
    /// <param name="partitionKey">Who a request counts for.</param>
    /// <returns>The limiter; a request of another class gets no limit from it.</returns>
    private static PartitionedRateLimiter<HttpContext> LimiterFor(RateLimitClass limitClass, RateRule rule, Func<HttpContext, string> partitionKey) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.GetEndpoint()?.Metadata.GetMetadata<RateLimitMetadata>() is { } metadata && metadata.Class == limitClass
                ? RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rule.Limit,
                        Window = TimeSpan.FromSeconds(rule.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    })
                : RateLimitPartition.GetNoLimiter(string.Empty));

    /// <summary>The partition of a request that has a verified token: the player in its <c>sub</c> claim; without one, the remote address.</summary>
    /// <param name="context">The request.</param>
    /// <returns>The key; never throws, because it runs inside the limiter.</returns>
    private static string PlayerKey(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true && context.User.FindFirst(AccessTokens.PlayerIdClaim)?.Value is { Length: > 0 } player
            ? "player:" + player
            : AddressKey(context);

    /// <summary>The partition of a request counted by address: its remote IP address (an IPv4 address mapped into IPv6 counts as IPv4).</summary>
    /// <param name="context">The request.</param>
    /// <returns>The key; <c>unknown</c> when the connection has no address.</returns>
    private static string AddressKey(HttpContext context)
    {
        IPAddress? address = context.Connection.RemoteIpAddress;
        return "address:" + (address is null ? "unknown" : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString());
    }

    /// <summary>
    /// Writes the 429 answer: <c>Retry-After</c> in whole seconds (the window's length, which is when the player is sure to have a new budget;
    /// at least 1) and the Problem Details of <see cref="ErrorCatalogue.RateLimited"/>.
    /// </summary>
    /// <param name="context">The refused request and the lease that refused it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the answer is written.</returns>
    private static ValueTask WriteRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        int retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 1;
        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        return new ValueTask(ApiProblem.Of(ErrorCatalogue.RateLimited).ExecuteAsync(context.HttpContext));
    }
}
