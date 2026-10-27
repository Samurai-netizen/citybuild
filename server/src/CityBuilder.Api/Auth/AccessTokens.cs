using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CityBuilder.Api.Auth;

/// <summary>
/// Issues the access tokens and gives the rules that check them (core-instructions/07-security-model.md): a JWT signed with HMAC-SHA256 and
/// <see cref="AuthOptions.SigningKey"/>, for this server only (<see cref="Issuer"/>, <see cref="Audience"/>), with the player's id as its
/// only claim of its own (<see cref="PlayerIdClaim"/>) and a lifetime of <see cref="AuthOptions.TokenLifetimeMinutes"/>. Times come from the
/// server clock, both when a token is issued and when it is checked, so tests move them with a fake clock. Singleton.
/// </summary>
/// <param name="options">The validated authentication settings.</param>
/// <param name="timeProvider">The server clock.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class AccessTokens(IOptions<AuthOptions> options, TimeProvider timeProvider)
{
    /// <summary>The <c>iss</c> claim of every token: this server issued it.</summary>
    public const string Issuer = "citybuilder-api";

    /// <summary>The <c>aud</c> claim of every token: it is meant for this server's API.</summary>
    public const string Audience = "citybuilder-api";

    /// <summary>The claim that holds the player's id: <c>sub</c>, the token's subject, as a GUID in the <c>D</c> format.</summary>
    public const string PlayerIdClaim = JwtRegisteredClaimNames.Sub;

    /// <summary>The only signing algorithm issued and accepted: an unsigned token or one with another algorithm is refused.</summary>
    private const string _algorithm = SecurityAlgorithms.HmacSha256;

    /// <summary>The settings; read when a token is issued or the rules are built, after the start validated them.</summary>
    private readonly IOptions<AuthOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
    /// <summary>The server clock: when a token is issued and whether it is still valid.</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    /// <summary>Writes the tokens.</summary>
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    /// <summary>Issues an access token for the player, valid from now (the whole second) for the configured lifetime.</summary>
    /// <param name="playerId">The player the token lets the bearer act for.</param>
    /// <returns>The signed token and when it expires.</returns>
    public AccessToken Issue(Guid playerId)
    {
        // JWT times are whole seconds, so the token's times and the returned expiry are the same instant.
        DateTimeOffset issuedAt = DateTimeOffset.FromUnixTimeSeconds(_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        DateTimeOffset expiresAt = issuedAt.AddMinutes(_options.Value.TokenLifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = new Dictionary<string, object> { [PlayerIdClaim] = playerId.ToString("D") },
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(), _algorithm),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    /// <summary>
    /// The rules a token must pass: signed with <see cref="AuthOptions.SigningKey"/> using HMAC-SHA256, this server's issuer and audience,
    /// and the server clock between its <c>nbf</c> (inclusive) and <c>exp</c> (exclusive), with no clock skew.
    /// </summary>
    /// <returns>The validation parameters for the JWT bearer handler.</returns>
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = SigningKey(),
        ValidAlgorithms = [_algorithm],
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = IsWithinLifetime,
    };

    /// <summary>The signing key from the settings.</summary>
    /// <returns>The symmetric key.</returns>
    private SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(_options.Value.SigningKey));

    /// <summary>
    /// Checks a token's lifetime against the server clock (principle 3); the handler's own check would read the machine's clock. The
    /// parameters are the handler's delegate's.
    /// </summary>
    /// <param name="notBefore">The token's <c>nbf</c>, in UTC, if it has one.</param>
    /// <param name="expires">The token's <c>exp</c>, in UTC; a token without one is refused.</param>
    /// <param name="token">The token; not used.</param>
    /// <param name="parameters">The validation parameters; not used.</param>
    /// <returns><see langword="true"/> when the server clock is at or after <paramref name="notBefore"/> and before
    /// <paramref name="expires"/>.</returns>
    private bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters parameters)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return expires is { } end && now < new DateTimeOffset(end, TimeSpan.Zero)
            && (notBefore is not { } start || now >= new DateTimeOffset(start, TimeSpan.Zero));
    }
}
