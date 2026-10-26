using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CityBuilder.IntegrationTests;

/// <summary>The liveness endpoint (core-instructions/05-api-contract.md); runs in memory, no Docker.</summary>
/// <param name="factory">The in-memory server.</param>
public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>A running server answers <c>/health</c> with 200 and only the status word <c>Healthy</c>.</summary>
    [Fact]
    public async Task GetHealth_AppRunning_Returns200WithStatusOnly()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Only the status word: no check details, versions or exception text leak to anonymous callers (07).
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
