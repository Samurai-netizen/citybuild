using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CityBuilder.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CityBuilder.IntegrationTests.Golden;

/// <summary>
/// A player who plays only over HTTP, as the client will: logged in with <c>POST /api/v1/auth/dev-login</c>, every later request with that
/// token as <c>Authorization: Bearer</c>, every command with a new request id. Each call fails the test unless the server answers 200, and
/// returns the snapshot as JSON, so a test reads what the client would read.
/// </summary>
internal sealed class GoldenHttpPlayer : IDisposable
{
    /// <summary>The client of the server under test, carrying the player's token.</summary>
    private readonly HttpClient _client;

    /// <summary>Wraps a client that already carries the token.</summary>
    /// <param name="client">The client.</param>
    /// <param name="playerId">The player the token is for.</param>
    private GoldenHttpPlayer(HttpClient client, Guid playerId)
    {
        _client = client;
        PlayerId = playerId;
    }

    /// <summary>The player's id: the <c>sub</c> of the token the dev login returned.</summary>
    public Guid PlayerId { get; }

    /// <summary>Logs the name in; the first login of a name creates the player and the starter city at the server's now.</summary>
    /// <param name="server">The server under test.</param>
    /// <param name="displayName">The dev name.</param>
    /// <returns>The logged-in player.</returns>
    public static async Task<GoldenHttpPlayer> LoginAsync(WebApplicationFactory<Program> server, string displayName)
    {
        HttpClient client = server.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/dev-login", UriKind.Relative), new { displayName }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DevLoginResponse login = (await response.Content.ReadFromJsonAsync<DevLoginResponse>(TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return new GoldenHttpPlayer(client, Guid.Parse(new JsonWebToken(login.AccessToken).Subject));
    }

    /// <summary><c>GET /api/v1/city</c>: settles the city to the server's now and returns the snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public async Task<JsonNode> GetCityAsync()
    {
        using HttpResponseMessage response = await _client.GetAsync(new Uri("/api/v1/city", UriKind.Relative), TestContext.Current.CancellationToken);
        return await SnapshotOfAsync(response);
    }

    /// <summary><c>POST /api/v1/commands/start-research</c> with a new request id.</summary>
    /// <param name="researchId">The research node.</param>
    /// <returns>The snapshot.</returns>
    public Task<JsonNode> StartResearchAsync(string researchId) =>
        PostAsync("start-research", new { requestId = Guid.NewGuid(), researchId });

    /// <summary><c>POST /api/v1/commands/place-building</c> with a new request id.</summary>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="x">The tile's column.</param>
    /// <param name="y">The tile's row.</param>
    /// <returns>The snapshot.</returns>
    public Task<JsonNode> PlaceAsync(string buildingTypeId, int x, int y) =>
        PostAsync("place-building", new { requestId = Guid.NewGuid(), buildingTypeId, x, y });

    /// <summary><c>POST /api/v1/commands/assign-workers</c> with a new request id.</summary>
    /// <param name="buildingId">The building, by its id in the player's city.</param>
    /// <param name="workers">The absolute worker count.</param>
    /// <returns>The snapshot.</returns>
    public Task<JsonNode> AssignWorkersAsync(int buildingId, int workers) =>
        PostAsync("assign-workers", new { requestId = Guid.NewGuid(), buildingId, workers });

    /// <summary>Disposes the client.</summary>
    public void Dispose() => _client.Dispose();

    /// <summary>Sends a command body as JSON to <c>/api/v1/commands/</c><paramref name="command"/>.</summary>
    /// <param name="command">The command's path segment.</param>
    /// <param name="body">The body, serialized in camelCase like every request.</param>
    /// <returns>The snapshot.</returns>
    private async Task<JsonNode> PostAsync(string command, object body)
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            new Uri("/api/v1/commands/" + command, UriKind.Relative), body, TestContext.Current.CancellationToken);
        return await SnapshotOfAsync(response);
    }

    /// <summary>The snapshot in a 200 response; fails the test with the body (a Problem Details names its code) for any other status.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The body as JSON.</returns>
    private static async Task<JsonNode> SnapshotOfAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {body}");
        return JsonNode.Parse(body) ?? throw new InvalidOperationException("The body is JSON null.");
    }
}
