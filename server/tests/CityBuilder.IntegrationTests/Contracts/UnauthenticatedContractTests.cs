using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using CityBuilder.IntegrationTests.Auth;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// What a game endpoint answers a request without a working access token (<c>core-instructions/05-api-contract.md</c>, errors): 401 with a
/// Problem Details body whose <c>code</c> is <c>AUTHENTICATION_REQUIRED</c>, read as raw JSON. The token checks themselves are
/// <see cref="AuthenticationTests"/>'; these prove the answer a client gets from the real endpoints. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server.</param>
public sealed class UnauthenticatedContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The game endpoints that read an access token.</summary>
    public static TheoryData<string> Paths => new() { "/api/v1/content", "/api/v1/city" };

    /// <summary>The player the hand-built tokens are for.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>A request without a token gets the 401 Problem Details answer, with the exact members of the contract.</summary>
    /// <param name="path">The endpoint.</param>
    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Get_WithoutToken_ReturnsAuthenticationRequiredProblem(string path)
    {
        ApiResponse response = await ApiClient.GetAsync(factory, path, authorization: null);

        AssertAuthenticationRequired(response);
    }

    /// <summary>Every way a token can fail gets the same answer, so it never says why a token was refused.</summary>
    /// <param name="path">The endpoint.</param>
    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Security_EveryRefusal_GetsTheSameAnswer(string path)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AuthenticationHeaderValue?[] refusals =
        [
            null,
            new("Bearer", "not-a-token"),
            new("Basic", "ZGV2MTpwYXNzd29yZA=="),
            new("Bearer", TestTokens.Build(_playerId, now.AddHours(-2), TestTokens.SigningKey)),
            new("Bearer", TestTokens.Build(_playerId, now, TestTokens.OtherSigningKey)),
            new("Bearer", TestTokens.Build(_playerId, now, signingKey: null)),
            new("Bearer", TestTokens.Build(_playerId, now, TestTokens.SigningKey, issuer: "someone-else")),
        ];

        var bodies = new HashSet<string>();
        foreach (AuthenticationHeaderValue? authorization in refusals)
        {
            ApiResponse response = await ApiClient.GetAsync(factory, path, authorization);

            AssertAuthenticationRequired(response);
            // The trace id is the one member that differs between requests.
            JsonNode body = response.Json();
            body.AsObject().Remove("traceId");
            bodies.Add(body.ToJsonString());
            if (authorization?.Parameter is { } token)
            {
                Assert.DoesNotContain(token, response.Body, StringComparison.Ordinal);
            }
        }

        Assert.Single(bodies);
    }

    /// <summary>A valid token is not refused: the content endpoint answers 200 (the city endpoint needs a city, which the snapshot tests give it).</summary>
    [Fact]
    public async Task Get_WithValidToken_IsNotRefused()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/api/v1/content", _playerId);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    /// <summary>Asserts the 401 answer of the contract: status, problem media type, <c>WWW-Authenticate</c> and the members of the body.</summary>
    /// <param name="response">The response.</param>
    private static void AssertAuthenticationRequired(ApiResponse response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.StartsWith("application/problem+json", response.ContentType, StringComparison.Ordinal);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).ToString());

        JsonNode body = response.Json();
        JsonShape.AssertObject(body, "type:string title:string status:int detail:string code:string traceId:string", "$");
        Assert.Equal("AUTHENTICATION_REQUIRED", (string)body["code"]!);
        Assert.Equal(401, (int)body["status"]!);
    }
}
