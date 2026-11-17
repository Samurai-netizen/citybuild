using CityBuilder.Api.Time;
using CityBuilder.Application.Metrics;
using Microsoft.Extensions.Options;

namespace CityBuilder.Api.Metrics;

/// <summary>
/// The player metrics in the composition root (CR-0009): the options and the <see cref="SessionPolicy"/> of the settle and the command
/// pipeline. The events themselves are written by the city's transaction (<c>IPlayerEventRepository</c>, registered with the persistence).
/// </summary>
public static class PlayerMetrics
{
    /// <summary>
    /// Registers <see cref="MetricsOptions"/> (validated on start: the session gap in range) and the <see cref="SessionPolicy"/>: the gap
    /// times the server clock's scale (<see cref="ServerClock.TimeScaleOf"/>).
    /// </summary>
    /// <param name="services">The service collection; <see cref="ServerClock.AddServerClock"/> registers the clock the policy reads.</param>
    /// <param name="configuration">The app's configuration; the section <see cref="MetricsOptions.SectionName"/> is bound.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddPlayerMetrics(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MetricsOptions>()
            .Bind(configuration.GetSection(MetricsOptions.SectionName));

        services.AddSingleton(provider => new SessionPolicy(provider.GetRequiredService<IOptions<MetricsOptions>>().Value.SessionGapSeconds));
        return services;
    }
}
