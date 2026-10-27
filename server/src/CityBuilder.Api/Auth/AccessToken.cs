namespace CityBuilder.Api.Auth;

/// <summary>An access token <see cref="AccessTokens.Issue"/> issued.</summary>
/// <param name="Token">The signed JWT, sent back as <c>Authorization: Bearer &lt;token&gt;</c>.</param>
/// <param name="ExpiresAt">When the token stops being valid (UTC, a whole second).</param>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
