using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace CityBuilder.IntegrationTests.Auth;

/// <summary>
/// Where <c>POST /api/v1/auth/dev-login</c> exists, and the bodies it refuses before it touches the database
/// (core-instructions/05-api-contract.md, 07-security-model.md). In memory, no Docker: the database is never reached. What a login stores is
/// in <see cref="DevLoginDatabaseTests"/>.
/// </summary>
/// <param name="factory">The in-memory server (Development, dev login on); a test derives a copy with another environment or setting.</param>
public sealed class DevLoginEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The dev login's path.</summary>
    private static readonly Uri _devLogin = new("/api/v1/auth/dev-login", UriKind.Relative);

    /// <summary>In Production the dev login isn't mapped: the path answers 404, as for any unknown path.</summary>
    [Fact]
    public async Task Security_DevLoginInProduction_IsNotMapped()
    {
        await using WebApplicationFactory<Program> production =
            factory.WithWebHostBuilder(host => host.UseEnvironment(Environments.Production));

        using HttpResponseMessage response = await PostAsync(production, JsonContent.Create(new { displayName = "dev1" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>In Development with <c>Auth:DevLoginEnabled</c> off, the dev login isn't mapped either.</summary>
    [Fact]
    public async Task PostDevLogin_DevLoginDisabled_Returns404()
    {
        await using WebApplicationFactory<Program> disabled =
            factory.WithWebHostBuilder(host => host.UseSetting("Auth:DevLoginEnabled", "false"));

        using HttpResponseMessage response = await PostAsync(disabled, JsonContent.Create(new { displayName = "dev1" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A display name that isn't 1 to 32 ASCII letters, digits, <c>_</c>, <c>-</c> or <c>.</c>, or none at all, is refused with 400
    /// <c>INVALID_ARGUMENT</c> before anything is stored.
    /// </summary>
    /// <param name="body">The request body as JSON.</param>
    [Theory]
    [InlineData("""{ "displayName": "" }""")]
    [InlineData("""{ "displayName": "   " }""")]
    [InlineData("""{ "displayName": "dev one" }""")]
    [InlineData("""{ "displayName": "dév" }""")]
    [InlineData("""{ "displayName": "<script>" }""")]
    [InlineData("""{ "displayName": "abcdefghijabcdefghijabcdefghijabc" }""")]
    [InlineData("""{ "displayName": null }""")]
    [InlineData("""{ }""")]
    [InlineData("""null""")]
    public async Task PostDevLogin_DisplayNameNotAllowed_Returns400InvalidArgument(string body)
    {
        using HttpResponseMessage response = await PostAsync(factory, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("INVALID_ARGUMENT", problem.RootElement.GetProperty("code").GetString());
    }

    /// <summary>A body that isn't JSON is refused with 400 before anything is stored.</summary>
    [Fact]
    public async Task PostDevLogin_MalformedJson_Returns400()
    {
        using HttpResponseMessage response =
            await PostAsync(factory, new StringContent("""{ "displayName": """, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Posts the body to the dev login.</summary>
    /// <param name="server">The server.</param>
    /// <param name="body">The request body.</param>
    /// <returns>The response; the caller disposes it.</returns>
    private static async Task<HttpResponseMessage> PostAsync(WebApplicationFactory<Program> server, HttpContent body)
    {
        using HttpClient client = server.CreateClient();
        using (body)
        {
            return await client.PostAsync(_devLogin, body, TestContext.Current.CancellationToken);
        }
    }
}
