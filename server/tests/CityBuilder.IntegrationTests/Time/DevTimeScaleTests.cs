using System.Net;
using CityBuilder.Api.Time;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CityBuilder.IntegrationTests.Time;

/// <summary>
/// <c>Simulation:DevTimeScale</c> (core-instructions/04-simulation-model.md section 1; <c>07</c>, dev time scale left on): the server clock
/// is a <see cref="ScaledTimeProvider"/> only in Development with a scale above 1, the start stops for a scale out of range or other than 1
/// elsewhere, and <c>GET /content</c> reports the active scale as <c>rules.timeScale</c>, 1 outside Development. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server with the test settings (Development); each test derives one with a changed setting.</param>
public sealed class DevTimeScaleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>A player id for the tokens; the content endpoint reads no city.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");

    /// <summary>A scale other than 1 outside Development stops the start: no production server can run a fast clock.</summary>
    /// <param name="environment">An environment other than Development.</param>
    /// <param name="scale">The setting's value.</param>
    [Theory]
    [InlineData("Production", "60")]
    [InlineData("Production", "2")]
    [InlineData("Staging", "60")]
    public void Security_DevTimeScaleOutsideDevelopment_StopsTheStart(string environment, string scale)
    {
        using WebApplicationFactory<Program> broken = factory.WithWebHostBuilder(host => host
            .UseEnvironment(environment)
            .UseSetting("Simulation:DevTimeScale", scale));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures,
            failure => failure.StartsWith("Simulation:DevTimeScale may be other than 1 only in Development", StringComparison.Ordinal));
    }

    /// <summary>A scale below 1 or above the highest stops the start, in Development too.</summary>
    /// <param name="scale">The setting's value.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("-60")]
    [InlineData("1001")]
    public void Start_DevTimeScaleOutOfRange_FailsValidation(string scale)
    {
        using WebApplicationFactory<Program> broken = factory.WithWebHostBuilder(host => host.UseSetting("Simulation:DevTimeScale", scale));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures,
            failure => failure.StartsWith("Simulation:DevTimeScale must be between 1 and 1000", StringComparison.Ordinal));
    }

    /// <summary>Outside Development the clock is the system clock and <c>rules.timeScale</c> is 1, with the setting left out or set to 1.</summary>
    /// <param name="scale">The setting's value; <see langword="null"/> leaves it out.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Security_Production_ClockIsRealAndTimeScaleIsOne(string? scale)
    {
        using WebApplicationFactory<Program> production = factory.WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Production");
            if (scale is not null)
            {
                host.UseSetting("Simulation:DevTimeScale", scale);
            }
        });

        ApiResponse response = await ApiClient.GetAsync(production, "/api/v1/content", _playerId);

        Assert.Same(TimeProvider.System, production.Services.GetRequiredService<TimeProvider>());
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(1, (int)response.Json()["rules"]!["timeScale"]!);
    }

    /// <summary>In Development a scale above 1 makes the server clock a scaled one, and <c>GET /content</c> reports that scale.</summary>
    /// <param name="scale">The setting's value.</param>
    [Theory]
    [InlineData(60)]
    [InlineData(1000)]
    public async Task Get_DevelopmentWithAScale_ClockIsScaledAndTimeScaleIsIt(int scale)
    {
        using WebApplicationFactory<Program> fast =
            factory.WithWebHostBuilder(host => host.UseSetting("Simulation:DevTimeScale", scale.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        ApiResponse response = await ApiClient.GetAsync(fast, "/api/v1/content", _playerId);

        ScaledTimeProvider clock = Assert.IsType<ScaledTimeProvider>(fast.Services.GetRequiredService<TimeProvider>());
        Assert.Equal(scale, clock.Scale);
        Assert.Equal(scale, (int)response.Json()["rules"]!["timeScale"]!);
    }

    /// <summary>
    /// A fast clock is logged at startup as a warning with its scale and the server time in ISO 8601 UTC (event id 7000); real time logs
    /// nothing about the clock.
    /// </summary>
    [Fact]
    public void Start_DevelopmentWithAScale_LogsTheFastClock()
    {
        var fastLogs = new CapturingLogger();
        var realLogs = new CapturingLogger();
        using WebApplicationFactory<Program> fast = fastLogs.AddTo(factory.WithWebHostBuilder(host => host.UseSetting("Simulation:DevTimeScale", "60")));
        using WebApplicationFactory<Program> real = realLogs.AddTo(factory);

        _ = fast.Services;
        _ = real.Services;

        CapturingLogger.Entry warning = Assert.Single(fastLogs.Events(7000));
        Assert.Equal((LogLevel.Warning, (object?)60), (warning.Level, warning.Values["TimeScale"]));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", (string)warning.Values["ServerTimeUtc"]!);
        Assert.Empty(realLogs.Events(7000));
    }

    /// <summary>The time scale of any clock but a scaled one is 1.</summary>
    [Fact]
    public void TimeScaleOf_ClocksThatAreNotScaled_IsOne()
    {
        Assert.Equal(1, ServerClock.TimeScaleOf(TimeProvider.System));
        Assert.Equal(1, ServerClock.TimeScaleOf(new Microsoft.Extensions.Time.Testing.FakeTimeProvider()));
        Assert.Equal(60, ServerClock.TimeScaleOf(new ScaledTimeProvider(TimeProvider.System, 60)));
    }
}
