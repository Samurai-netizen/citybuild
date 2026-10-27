using System.Text;

namespace CityBuilder.Api.Auth;

/// <summary>
/// The authentication settings (docs/getting-started.md → Configuration), configuration section <c>Auth</c>: the key that signs and checks
/// the access tokens, how long a token lives, and whether the development login is on (core-instructions/07-security-model.md).
/// </summary>
public sealed class AuthOptions
{
    /// <summary>The configuration section's name.</summary>
    public const string SectionName = "Auth";

    /// <summary>The fewest bytes <see cref="SigningKey"/> may have: 32 bytes are the 256 bits HMAC-SHA256 needs.</summary>
    public const int MinSigningKeyBytes = 32;

    /// <summary>The shortest allowed <see cref="TokenLifetimeMinutes"/>.</summary>
    public const int MinTokenLifetimeMinutes = 1;

    /// <summary>The longest allowed <see cref="TokenLifetimeMinutes"/>: a day. M0 can't revoke a token, so a token must not live long.</summary>
    public const int MaxTokenLifetimeMinutes = 1440;

    /// <summary>
    /// <c>Auth:SigningKey</c>: the secret the access tokens are signed and checked with (HMAC-SHA256), at least
    /// <see cref="MinSigningKeyBytes"/> bytes as UTF-8, required in every environment. Anyone who knows it can sign a token for any player,
    /// so it comes from user-secrets or the environment variable <c>Auth__SigningKey</c>, never from a committed file.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// <c>Auth:TokenLifetimeMinutes</c>: how long an access token is valid after it was issued. Default 720 (12 hours); between
    /// <see cref="MinTokenLifetimeMinutes"/> and <see cref="MaxTokenLifetimeMinutes"/>, or the start stops.
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 720;

    /// <summary>
    /// <c>Auth:DevLoginEnabled</c>: map <c>POST /api/v1/auth/dev-login</c>, which logs anyone in by name alone. Only allowed in Development
    /// (<c>true</c> in <c>appsettings.Development.json</c>, <c>false</c> in <c>appsettings.json</c>); <c>true</c> in any other environment
    /// stops the start.
    /// </summary>
    public bool DevLoginEnabled { get; set; }

    /// <summary>Whether <see cref="SigningKey"/> has at least <see cref="MinSigningKeyBytes"/> bytes as UTF-8.</summary>
    /// <returns><see langword="true"/> when the key is long enough.</returns>
    public bool HasLongEnoughSigningKey() => Encoding.UTF8.GetByteCount(SigningKey ?? string.Empty) >= MinSigningKeyBytes;
}
