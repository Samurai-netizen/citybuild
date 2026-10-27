using System.Security.Claims;
using CityBuilder.Api.Auth;
using Microsoft.AspNetCore.Http;

namespace CityBuilder.IntegrationTests.Auth;

/// <summary>
/// <see cref="HttpCurrentPlayer"/> takes the player only from an authenticated user's <c>sub</c> claim, and fails loudly when an endpoint
/// that doesn't require a token asks for the player.
/// </summary>
public sealed class HttpCurrentPlayerTests
{
    /// <summary>The player of the tests.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>An authenticated user with a player id claim gives that player.</summary>
    [Fact]
    public void PlayerId_AuthenticatedUserWithPlayerClaim_IsThatPlayer()
    {
        HttpCurrentPlayer current = For(new ClaimsIdentity([new Claim(AccessTokens.PlayerIdClaim, _playerId.ToString("D"))], "Bearer"));

        Assert.Equal(_playerId, current.PlayerId);
    }

    /// <summary>
    /// Without an authenticated user (no token checked), with no claim, or with a claim that isn't a player id, asking for the player
    /// throws instead of guessing.
    /// </summary>
    /// <param name="authenticationType">The identity's authentication type; <see langword="null"/> is an anonymous user.</param>
    /// <param name="claim">The <c>sub</c> claim's value, or <see langword="null"/> for none.</param>
    [Theory]
    [InlineData(null, "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b")]
    [InlineData("Bearer", null)]
    [InlineData("Bearer", "dev1")]
    [InlineData("Bearer", "00000000-0000-0000-0000-000000000000")]
    public void PlayerId_NoVerifiedPlayerClaim_Throws(string? authenticationType, string? claim)
    {
        Claim[] claims = claim is null ? [] : [new Claim(AccessTokens.PlayerIdClaim, claim)];
        HttpCurrentPlayer current = For(new ClaimsIdentity(claims, authenticationType));

        Assert.Throws<InvalidOperationException>(() => current.PlayerId);
    }

    /// <summary>A current player over a request whose user has the identity.</summary>
    /// <param name="identity">The user's identity.</param>
    /// <returns>The current player.</returns>
    private static HttpCurrentPlayer For(ClaimsIdentity identity) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } });
}
