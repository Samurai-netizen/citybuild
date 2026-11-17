using CityBuilder.Api.Metrics;
using CityBuilder.Application.Metrics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CityBuilder.IntegrationTests.Metrics;

/// <summary>
/// <c>Metrics:SessionGapSeconds</c> (CR-0009): the server's <see cref="SessionPolicy"/> is the gap (default 1 800 s) times the clock's
/// scale, and a gap out of 60 … 86 400 stops the start. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server with the test settings (Development); each test derives one with a changed setting.</param>
public sealed class MetricsOptionsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>With the setting left out the gap is 30 minutes.</summary>
    [Fact]
    public void SessionPolicy_Default_IsThirtyMinutes()
    {
        Assert.Equal(1800, factory.Services.GetRequiredService<SessionPolicy>().GapSeconds);
    }

    /// <summary>The gap is real time: on a Development clock 60 times as fast it is 60 times as many server seconds.</summary>
    [Fact]
    public void SessionPolicy_FastClock_IsTheGapTimesTheScale()
    {
        using WebApplicationFactory<Program> fast = factory.WithWebHostBuilder(host => host
            .UseSetting("Simulation:DevTimeScale", "60")
            .UseSetting("Metrics:SessionGapSeconds", "600"));

        Assert.Equal(36_000, fast.Services.GetRequiredService<SessionPolicy>().GapSeconds);
    }

    /// <summary>
    /// A gap below a minute or above a day stops the start: the start validator the host runs (<see cref="IStartupValidator"/>) fails on
    /// it. Checked on the registration itself, not by starting a host: a host whose start fails can lose the validation's exception to a
    /// disposed service provider (seen once in a full run).
    /// </summary>
    /// <param name="gap">The setting's value.</param>
    [Theory]
    [InlineData("59")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("86401")]
    public void Start_SessionGapOutOfRange_FailsValidation(string gap)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Metrics:SessionGapSeconds"] = gap })
            .Build();
        using ServiceProvider services = new ServiceCollection().AddPlayerMetrics(configuration).BuildServiceProvider();

        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(exception.Failures,
            failure => failure.StartsWith("Metrics:SessionGapSeconds must be between 60 and 86400", StringComparison.Ordinal));
    }

    /// <summary>The bounds themselves are allowed.</summary>
    /// <param name="gap">The setting's value.</param>
    /// <param name="expected">The policy's gap in server seconds (the clock is real time).</param>
    [Theory]
    [InlineData("60", 60)]
    [InlineData("86400", 86_400)]
    public void Start_SessionGapAtABound_IsTheGap(string gap, long expected)
    {
        using WebApplicationFactory<Program> bounded = factory.WithWebHostBuilder(host => host.UseSetting("Metrics:SessionGapSeconds", gap));

        Assert.Equal(expected, bounded.Services.GetRequiredService<SessionPolicy>().GapSeconds);
    }
}
