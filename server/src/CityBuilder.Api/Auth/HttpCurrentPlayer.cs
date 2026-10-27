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
            System.Security.Claims.ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
            string? claim = user?.Identity?.IsAuthenticated == true ? user.FindFirst(AccessTokens.PlayerIdClaim)?.Value : null;
            if (!Guid.TryParseExact(claim, "D", out Guid playerId) || playerId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "The request has no verified access token with a player id: the endpoint must require authorization.");
            }

            return playerId;
        }
    }
}
