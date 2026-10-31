using System.Net;

namespace CityBuilder.IntegrationTests;

/// <summary>The liveness endpoint (core-instructions/05-api-contract.md); runs in memory, no Docker.</summary>
/// <param name="factory">The in-memory server.</param>
public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary><c>/health</c> is a GET in the contract, and HEAD for a load balancer; any other method is 405, as on every other path.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="status">The expected status.</param>
    [Theory]
    [InlineData("GET", HttpStatusCode.OK)]
    [InlineData("HEAD", HttpStatusCode.OK)]
    [InlineData("POST", HttpStatusCode.MethodNotAllowed)]
    [InlineData("DELETE", HttpStatusCode.MethodNotAllowed)]
    public async Task Health_Methods_AreGetAndHeadOnly(string method, HttpStatusCode status)
    {
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/health");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
    }
}
