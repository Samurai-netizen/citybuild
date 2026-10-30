using CityBuilder.Application.Players;

namespace CityBuilder.Api.Auth;

/// <summary>
/// <see cref="ICurrentPlayer"/> of an HTTP request: the player id in the <see cref="AccessTokens.PlayerIdClaim"/> claim of the request's
/// verified access token (the JWT bearer handler checked it before the endpoint ran). Never anything from the body. Scoped.
/// </summary>
/// <param name="httpContextAccessor">Gives the current request.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class HttpCurrentPlayer(IHttpContextAccessor httpContextAccessor) : ICurrentPlayer
{
    /// <summary>Gives the current request and its authenticated user.</summary>
    private readonly IHttpContextAccessor _httpContextAccessor =
        httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    /// <inheritdoc />
    public Guid PlayerId
    {
        get
        {
            return Find(_httpContextAccessor.HttpContext?.User)
                ?? throw new InvalidOperationException(
                    "The request has no verified access token with a player id: the endpoint must require authorization.");
        }
    }

    /// <summary>
    /// The player of a request's user: the <see cref="AccessTokens.PlayerIdClaim"/> claim of a verified token as a GUID other than all zeros.
    /// For the log lines of code that runs whether or not a request has a token (the rate limiter, the exception handler).
    /// </summary>
    /// <param name="user">The request's user, or <see langword="null"/>.</param>
    /// <returns>The player id, or <see langword="null"/> when the request has no verified token with one.</returns>
    public static Guid? Find(System.Security.Claims.ClaimsPrincipal? user)
    {
        string? claim = user?.Identity?.IsAuthenticated == true ? user.FindFirst(AccessTokens.PlayerIdClaim)?.Value : null;
        return Guid.TryParseExact(claim, "D", out Guid playerId) && playerId != Guid.Empty ? playerId : null;
    }
}
