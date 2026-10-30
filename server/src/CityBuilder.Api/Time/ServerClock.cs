using Microsoft.Extensions.Options;

namespace CityBuilder.Api.Time;

/// <summary>
/// The server clock in the composition root (core-instructions/02-principles.md, rule 3; <c>04</c> section 1): the one
/// <see cref="TimeProvider"/> every use case, repository and token check reads. It is the system clock, except on a Development server with
/// <c>Simulation:DevTimeScale</c> above 1, where it is a <see cref="ScaledTimeProvider"/>. A scale other than 1 anywhere else stops the start
/// (core-instructions/07-security-model.md, dev time scale left on).
/// </summary>
public static class ServerClock
{
    /// <summary>
    /// Registers <see cref="SimulationOptions"/> (validated on start: the scale in range, and 1 outside Development) and the server's
    /// <see cref="TimeProvider"/>, built from them when first resolved.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration; the section <see cref="SimulationOptions.SectionName"/> is bound.</param>
    /// <param name="environment">The host environment; a scale other than 1 is allowed only in Development.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddServerClock(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<SimulationOptions>()
            .Bind(configuration.GetSection(SimulationOptions.SectionName))
            .Validate(options => options.DevTimeScale is >= SimulationOptions.MinDevTimeScale and <= SimulationOptions.MaxDevTimeScale,
                $"Simulation:DevTimeScale must be between {SimulationOptions.MinDevTimeScale} and {SimulationOptions.MaxDevTimeScale}.")
            .Validate(options => options.DevTimeScale == 1 || environment.IsDevelopment(),
                "Simulation:DevTimeScale may be other than 1 only in Development: a fast clock would hand every player hours of production " +
                "for minutes (core-instructions/07-security-model.md).")
            .ValidateOnStart();

        // The environment check repeats the validation's rule here, so no setting can make a clock fast outside Development.
        services.AddSingleton<TimeProvider>(provider =>
        {
            int scale = provider.GetRequiredService<IOptions<SimulationOptions>>().Value.DevTimeScale;
            return scale > 1 && environment.IsDevelopment() ? new ScaledTimeProvider(TimeProvider.System, scale) : TimeProvider.System;
        });
        return services;
    }

    /// <summary>
    /// The time scale of a server clock: server seconds per real second, which <c>GET /content</c> reports as <c>rules.timeScale</c> so the
    /// client's display clock runs as fast (<c>05</c>). 1 for any clock but a <see cref="ScaledTimeProvider"/>.
    /// </summary>
    /// <param name="clock">The server's clock.</param>
    /// <returns>The scale; 1 for real time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
    public static int TimeScaleOf(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return clock is ScaledTimeProvider scaled ? scaled.Scale : 1;
    }
}
