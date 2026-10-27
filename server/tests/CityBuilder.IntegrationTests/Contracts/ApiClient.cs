using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using CityBuilder.Api.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// What a test saw of a response: its status, its content type, its headers and its body as the raw text, so a contract test reads the JSON
/// the client would read, not an object a serializer built from it.
/// </summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="ContentType">The <c>Content-Type</c> header, or <see langword="null"/> when there is none.</param>
/// <param name="Headers">The response headers.</param>
/// <param name="Body">The body text; empty when there is none.</param>
internal sealed record ApiResponse(HttpStatusCode Status, string? ContentType, HttpResponseHeaders Headers, string Body)
{
    /// <summary>The body parsed as JSON, for a test that walks it.</summary>
    /// <returns>The root of the document.</returns>
    public JsonNode Json() => JsonNode.Parse(Body) ?? throw new InvalidOperationException("The body is JSON null.");
}

/// <summary>Calls the server under test over HTTP like the client does: <c>Authorization: Bearer</c> with a token the server issued.</summary>
internal static class ApiClient
{
    /// <summary>Sends a GET request.</summary>
    /// <param name="server">The server under test.</param>
    /// <param name="path">The path and query, for example <c>/api/v1/content</c>.</param>
    /// <param name="playerId">The player to send a token for (the server's own <see cref="AccessTokens"/> issues it), or <see langword="null"/> for no token.</param>
    /// <returns>The response as the test saw it.</returns>
    public static async Task<ApiResponse> GetAsync(WebApplicationFactory<Program> server, string path, Guid? playerId)
    {
        using HttpClient client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (playerId is { } player)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Services.GetRequiredService<AccessTokens>().Issue(player).Token);
        }

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        return new ApiResponse(
            response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            response.Headers,
            await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
