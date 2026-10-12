using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace CityBuilder.IntegrationTests;

/// <summary>The OpenAPI document is served in Development only (core-instructions/05-api-contract.md).</summary>
public sealed class OpenApiEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Uri _openApiDocument = new("/openapi/v1.json", UriKind.Relative);

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
