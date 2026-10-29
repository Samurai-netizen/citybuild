namespace CityBuilder.Api.Http;

/// <summary>Which limit an endpoint counts against (<see cref="RateLimitOptions"/>).</summary>
public enum RateLimitClass
{
    /// <summary>The command endpoints: per player, the burst and the sustained limit.</summary>
    Commands,

    /// <summary><c>GET /city</c>: per player.</summary>
    City,

    /// <summary>The dev login: per remote address.</summary>
    DevLogin,

    /// <summary><c>GET /content</c>: per player, a budget of its own.</summary>
    Content,
}

/// <summary>
/// The endpoint metadata that puts an endpoint in a <see cref="RateLimitClass"/>. The limiter reads it from the matched endpoint, so a
/// route group marks all its endpoints at once, and a test can check that no endpoint was left out.
/// </summary>
/// <param name="Class">The limit the endpoint counts against.</param>
public sealed record RateLimitMetadata(RateLimitClass Class);
