using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests;

/// <summary>The server clock is the only clock that drives progress (core-instructions/02-principles.md, rule 3).</summary>
public sealed class ServerClockTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void GetRequiredService_TimeProvider_IsTheSystemClock()
    {
        TimeProvider clock = factory.Services.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, clock);
    }
}
