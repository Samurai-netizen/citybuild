namespace CityBuilder.Application.Players;

/// <summary>
/// The player the request acts for (<c>core-instructions/07-security-model.md</c>, trust boundaries): taken only from the verified access
/// token, never from the request body. The Api resolves it per request; use cases receive its <see cref="PlayerId"/>.
/// </summary>
public interface ICurrentPlayer
{
    /// <summary>The acting player's id, from the access token.</summary>
    /// <exception cref="InvalidOperationException">The request carries no valid access token: the endpoint doesn't require one (a bug).</exception>
    Guid PlayerId { get; }
}
