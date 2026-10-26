using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests;

/// <summary>The server clock is the only clock that drives progress (core-instructions/02-principles.md, rule 3).</summary>
/// <param name="factory">The in-memory server.</param>
public sealed class ServerClockTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The server's <see cref="TimeProvider"/> is the system clock.</summary>
    [Fact]
    public void GetRequiredService_TimeProvider_IsTheSystemClock()
    {
        TimeProvider clock = factory.Services.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, clock);
    }
}
