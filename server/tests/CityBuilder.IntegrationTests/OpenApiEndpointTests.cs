using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace CityBuilder.IntegrationTests;

/// <summary>The OpenAPI document is served in Development only (core-instructions/05-api-contract.md).</summary>
/// <param name="factory">The in-memory server; each test runs a copy of it in one environment.</param>
public sealed class OpenApiEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>Where the OpenAPI document is served.</summary>
    private static readonly Uri _openApiDocument = new("/openapi/v1.json", UriKind.Relative);

    /// <summary>In Development the server serves the OpenAPI document.</summary>
    [Fact]
    public async Task GetOpenApiDocument_InDevelopment_ReturnsOpenApiJson()
    {
        await using WebApplicationFactory<Program> development =
            factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development));
        using HttpClient client = development.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(_openApiDocument, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openapi\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    /// <summary>In Production the server doesn't serve the OpenAPI document: the request gets 404.</summary>
    [Fact]
    public async Task Security_OpenApiDocument_InProduction_IsNotServed()
    {
        await using WebApplicationFactory<Program> production =
            factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));
        using HttpClient client = production.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(_openApiDocument, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
