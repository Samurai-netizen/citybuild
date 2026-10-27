namespace CityBuilder.Api.Contracts;

/// <summary>The answer of <c>POST /api/v1/auth/dev-login</c> (core-instructions/05-api-contract.md).</summary>
/// <param name="AccessToken">The access token, JSON <c>accessToken</c>; send it as <c>Authorization: Bearer &lt;accessToken&gt;</c>.</param>
/// <param name="ExpiresAtUtc">When the token stops being valid, JSON <c>expiresAtUtc</c>; log in again after that.</param>
public sealed record DevLoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
