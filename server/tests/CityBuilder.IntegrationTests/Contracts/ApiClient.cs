using System.Net;
using System.Net.Http.Headers;
using System.Text;
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
    public static Task<ApiResponse> GetAsync(WebApplicationFactory<Program> server, string path, Guid? playerId) =>
        GetAsync(
            server,
            path,
            playerId is { } player
                ? new AuthenticationHeaderValue("Bearer", server.Services.GetRequiredService<AccessTokens>().Issue(player).Token)
                : null);

    /// <summary>Sends a GET request with the given <c>Authorization</c> header, for the tests that attack the token check.</summary>
    /// <param name="server">The server under test.</param>
    /// <param name="path">The path and query.</param>
    /// <param name="authorization">The header, or <see langword="null"/> for none.</param>
    /// <returns>The response as the test saw it.</returns>
    public static async Task<ApiResponse> GetAsync(WebApplicationFactory<Program> server, string path, AuthenticationHeaderValue? authorization)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = authorization;
        return await SendAsync(server, request);
    }

    /// <summary>
    /// Sends a POST request with a JSON body written by the test as text, so a test can send exactly the bytes a client (or an attacker)
    /// would: unknown properties, wrong types, broken JSON.
    /// </summary>
    /// <param name="server">The server under test.</param>
    /// <param name="path">The path, for example <c>/api/v1/commands/place-building</c>.</param>
    /// <param name="playerId">The player to send a token for, or <see langword="null"/> for no token.</param>
    /// <param name="body">The body text, sent as <c>application/json</c>; <see langword="null"/> sends no body.</param>
    /// <returns>The response as the test saw it.</returns>
    public static Task<ApiResponse> PostAsync(WebApplicationFactory<Program> server, string path, Guid? playerId, string? body) =>
        PostAsync(server, path, playerId, body is null ? null : new StringContent(body, Encoding.UTF8, "application/json"), headers: null);

    /// <summary>
    /// Sends a POST request with any content and extra headers, for the tests that attack what surrounds the body: another content type, a
    /// header that claims a player.
    /// </summary>
    /// <param name="server">The server under test.</param>
    /// <param name="path">The path and query.</param>
    /// <param name="playerId">The player to send a token for, or <see langword="null"/> for no token.</param>
    /// <param name="content">The body; <see langword="null"/> sends none. The request disposes it.</param>
    /// <param name="headers">Extra request headers, or <see langword="null"/> for none.</param>
    /// <returns>The response as the test saw it.</returns>
    public static async Task<ApiResponse> PostAsync(
        WebApplicationFactory<Program> server, string path, Guid? playerId, HttpContent? content, IReadOnlyDictionary<string, string>? headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (playerId is { } player)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Services.GetRequiredService<AccessTokens>().Issue(player).Token);
        }

        foreach ((string name, string value) in headers ?? new Dictionary<string, string>())
        {
            request.Headers.Add(name, value);
        }

        request.Content = content;
        return await SendAsync(server, request);
    }

    /// <summary>Sends a request and reads the whole response.</summary>
    /// <param name="server">The server under test.</param>
    /// <param name="request">The request.</param>
    /// <returns>The response as the test saw it.</returns>
    private static async Task<ApiResponse> SendAsync(WebApplicationFactory<Program> server, HttpRequestMessage request)
    {
        using HttpClient client = server.CreateClient();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        return new ApiResponse(
            response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            response.Headers,
            await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
