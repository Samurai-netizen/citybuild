using System.Text;
using CityBuilder.Api.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CityBuilder.IntegrationTests.Auth;

/// <summary>
/// The tests' token signing key, and tokens the server didn't issue, built by hand to attack its checks (a wrong key, no signature, another
/// issuer). Valid tokens come from the server's own <see cref="AccessTokens"/>.
/// </summary>
internal static class TestTokens
{
    /// <summary>The signing key every test server gets (<c>Auth:SigningKey</c>); long enough, and never used outside tests.</summary>
    public const string SigningKey = "integration-tests-only-signing-key-0123456789";

    /// <summary>A key the test servers never get: a token signed with it has a wrong signature.</summary>
    public const string OtherSigningKey = "another-key-the-server-never-saw-0123456789ab";

    /// <summary>
    /// Builds a token for the player like the server's, valid from <paramref name="issuedAt"/> for an hour, with the parts a test changes.
    /// </summary>
    /// <param name="playerId">The player in the <c>sub</c> claim.</param>
    /// <param name="issuedAt">Its <c>iat</c> and <c>nbf</c>; <c>exp</c> is an hour later.</param>
    /// <param name="signingKey">The key it is signed with (HMAC-SHA256), or <see langword="null"/> for an unsigned token (<c>alg: none</c>).</param>
    /// <param name="issuer">Its <c>iss</c> claim.</param>
    /// <returns>The token.</returns>
    public static string Build(Guid playerId, DateTimeOffset issuedAt, string? signingKey, string issuer = AccessTokens.Issuer) =>
        Build(new Dictionary<string, object> { [AccessTokens.PlayerIdClaim] = playerId.ToString("D") }, issuedAt, signingKey, issuer);

    /// <summary>Builds a token with exactly the claims a test names, for a token signed with the right key that names no player.</summary>
    /// <param name="claims">Its claims besides the issuer, the audience and the times.</param>
    /// <param name="issuedAt">Its <c>iat</c> and <c>nbf</c>; <c>exp</c> is an hour later.</param>
    /// <param name="signingKey">The key it is signed with (HMAC-SHA256), or <see langword="null"/> for an unsigned token (<c>alg: none</c>).</param>
    /// <param name="issuer">Its <c>iss</c> claim.</param>
    /// <returns>The token.</returns>
    public static string Build(Dictionary<string, object> claims, DateTimeOffset issuedAt, string? signingKey, string issuer = AccessTokens.Issuer)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = AccessTokens.Audience,
            Claims = claims,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = issuedAt.AddHours(1).UtcDateTime,
            SigningCredentials = signingKey is null
                ? null
                : new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }
}
