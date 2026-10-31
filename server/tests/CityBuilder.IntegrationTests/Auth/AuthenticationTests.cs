using System.Net;
using System.Net.Http.Headers;
using CityBuilder.Api.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace CityBuilder.IntegrationTests.Auth;

/// <summary>
/// The access token checks over HTTP (core-instructions/07-security-model.md, trust boundaries): an endpoint that requires a token answers
/// only a request with a valid one, and the acting player comes from that token. In memory, no Docker; the test-only
/// <see cref="CurrentPlayerEndpoint"/> stands in for the game endpoints, and the server clock is a fake one.
/// </summary>
/// <param name="factory">The in-memory server; each test runs a copy with the test endpoint and its own clock.</param>
public sealed class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The instant the fake server clock starts at.</summary>
    private static readonly DateTimeOffset _start = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The player the tokens are for.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>The server clock of the test's server copy.</summary>
    private readonly FakeTimeProvider _clock = new(_start);

    /// <summary>A token the server issued resolves to its player: the endpoint answers with the token's player id.</summary>
    [Fact]
    public async Task GetCurrentPlayer_TokenTheServerIssued_ReturnsItsPlayer()
    {
        await using WebApplicationFactory<Program> server = Server();
        string token = server.Services.GetRequiredService<AccessTokens>().Issue(_playerId).Token;

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, "Bearer", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_playerId.ToString("D"), await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>A token is still valid in the last second of its lifetime (12 hours by default).</summary>
    [Fact]
    public async Task GetCurrentPlayer_LastSecondOfLifetime_ReturnsItsPlayer()
    {
        await using WebApplicationFactory<Program> server = Server();
        AccessToken token = server.Services.GetRequiredService<AccessTokens>().Issue(_playerId);
        _clock.Advance(TimeSpan.FromHours(12) - TimeSpan.FromSeconds(1));

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, "Bearer", token.Token);

        Assert.Equal(_start.AddHours(12), token.ExpiresAt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A request without a token is refused with 401.</summary>
    [Fact]
    public async Task Security_RequestWithoutToken_IsRefused()
    {
        await using WebApplicationFactory<Program> server = Server();

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, scheme: null, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A malformed token, or a token under another scheme, is refused with 401.</summary>
    /// <param name="scheme">The scheme of the <c>Authorization</c> header.</param>
    /// <param name="token">The header's parameter.</param>
    [Theory]
    [InlineData("Bearer", "not-a-token")]
    [InlineData("Bearer", "a.b.c")]
    [InlineData("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0")]
    [InlineData("Basic", "ZGV2MTpwYXNzd29yZA==")]
    public async Task Security_MalformedToken_IsRefused(string scheme, string token)
    {
        await using WebApplicationFactory<Program> server = Server();

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, scheme, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A token is refused from the second it expires on, by the server clock: an old token can't be replayed.</summary>
    [Fact]
    public async Task Security_ExpiredToken_IsRefused()
    {
        await using WebApplicationFactory<Program> server = Server();
        string token = server.Services.GetRequiredService<AccessTokens>().Issue(_playerId).Token;
        _clock.Advance(TimeSpan.FromHours(12));

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, "Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A token signed with a key other than the server's is refused: nobody without the key can act for a player.</summary>
    [Fact]
    public async Task Security_TokenWithWrongSignature_IsRefused()
    {
        await using WebApplicationFactory<Program> server = Server();
        string token = TestTokens.Build(_playerId, _start, TestTokens.OtherSigningKey);

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, "Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An unsigned token (<c>alg: none</c>) is refused, even with the right claims.</summary>
    [Fact]
    public async Task Security_UnsignedToken_IsRefused()
    {
        await using WebApplicationFactory<Program> server = Server();
        string token = TestTokens.Build(_playerId, _start, signingKey: null);

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, "Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A token from another issuer is refused, even signed with the server's key.</summary>
    [Fact]
    public async Task Security_TokenOfAnotherIssuer_IsRefused()
    {
        await using WebApplicationFactory<Program> server = Server();
        string token = TestTokens.Build(_playerId, _start, TestTokens.SigningKey, issuer: "someone-else");

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, "Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A token signed with the server's key whose <c>sub</c> is no player id (the server never issues one, but a key can outlive a format) is
    /// refused at the token check like every other bad token, not a 500 at the first read of the player.
    /// </summary>
    /// <param name="subject">The <c>sub</c> claim, or <see langword="null"/> for none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-player-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Security_SignedTokenWithoutAPlayerId_IsRefused(string? subject)
    {
        await using WebApplicationFactory<Program> server = Server();
        Dictionary<string, object> claims = subject is null ? [] : new() { [AccessTokens.PlayerIdClaim] = subject };
        string token = TestTokens.Build(claims, _start, TestTokens.SigningKey);

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, "Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The hand-built token of the attacks is otherwise right: with the server's key and issuer it is accepted.</summary>
    [Fact]
    public async Task GetCurrentPlayer_HandBuiltTokenWithServerKey_ReturnsItsPlayer()
    {
        await using WebApplicationFactory<Program> server = Server();
        string token = TestTokens.Build(_playerId, _start, TestTokens.SigningKey);

        using HttpResponseMessage response = await GetCurrentPlayerAsync(server, "Bearer", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A copy of the server with the test endpoint and <see cref="_clock"/> as its clock.</summary>
    /// <returns>The copy; the test disposes it.</returns>
    private WebApplicationFactory<Program> Server() =>
        CurrentPlayerEndpoint.AddTo(factory, host => host.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(_clock);
        }));

    /// <summary>Calls the test endpoint with an <c>Authorization</c> header, or without one.</summary>
    /// <param name="server">The server copy.</param>
    /// <param name="scheme">The header's scheme, or <see langword="null"/> for no header.</param>
    /// <param name="token">The header's parameter.</param>
    /// <returns>The response; the caller disposes it.</returns>
    private static async Task<HttpResponseMessage> GetCurrentPlayerAsync(WebApplicationFactory<Program> server, string? scheme, string? token)
    {
        using HttpClient client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, CurrentPlayerEndpoint.Path);
        if (scheme is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
