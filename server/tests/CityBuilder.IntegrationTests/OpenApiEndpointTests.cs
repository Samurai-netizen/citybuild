using System.Net;
using System.Text.Json;
using CityBuilder.Api.OpenApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// The document marks the operations that need the access token with the bearer scheme (<c>BearerSecurity</c>, M0-61): every game
    /// endpoint, and not the dev login, which takes requests without a token.
    /// </summary>
    /// <param name="path">An operation's path.</param>
    /// <param name="method">Its method, lower case as in the document.</param>
    /// <param name="needsToken">Whether it needs the token.</param>
    [Theory]
    [InlineData("/api/v1/city", "get", true)]
    [InlineData("/api/v1/content", "get", true)]
    [InlineData("/api/v1/commands/place-building", "post", true)]
    [InlineData("/api/v1/auth/dev-login", "post", false)]
    public async Task GetOpenApiDocument_Operations_CarryTheBearerRequirementWhenTheyNeedAToken(string path, string method, bool needsToken)
    {
        await using WebApplicationFactory<Program> development =
            factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development));
        using HttpClient client = development.CreateClient();

        using JsonDocument document = JsonDocument.Parse(
            await client.GetStringAsync(_openApiDocument, TestContext.Current.CancellationToken));

        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        bool hasBearer = operation.TryGetProperty("security", out JsonElement security)
            && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _));
        Assert.Equal(needsToken, hasBearer);
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").ValueKind);
    }

    /// <summary>
    /// A server never takes the build-time document's placeholder settings (<see cref="BuildTimeDocument"/>): only the package's tool process
    /// counts as that run, and a server's signing key is its own.
    /// </summary>
    [Fact]
    public void Security_BuildTimeDocumentSettings_AreNotUsedByAServer()
    {
        using WebApplicationFactory<Program> production =
            factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));
        IConfiguration configuration = production.Services.GetRequiredService<IConfiguration>();

        Assert.False(BuildTimeDocument.IsRunning);
        Assert.NotEqual(BuildTimeDocument.Settings["Auth:SigningKey"], configuration["Auth:SigningKey"]);
        Assert.NotEqual(BuildTimeDocument.Settings["ConnectionStrings:CityBuilder"], configuration["ConnectionStrings:CityBuilder"]);
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
