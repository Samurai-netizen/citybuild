namespace CityBuilder.Api.Http;

/// <summary>
/// One limit: at most <see cref="Limit"/> requests of a player (or address) in a fixed window of <see cref="WindowSeconds"/> seconds. The
/// request after that is refused with <c>RATE_LIMITED</c> until the window is over (core-instructions/05-api-contract.md, rate limits).
/// </summary>
/// <param name="limit">The requests allowed per window.</param>
/// <param name="windowSeconds">The window's length in seconds.</param>
public sealed class RateRule(int limit, int windowSeconds)
{
    /// <summary>The most requests one player (or address) may make in one window.</summary>
    public int Limit { get; set; } = limit;

    /// <summary>The window's length in seconds.</summary>
    public int WindowSeconds { get; set; } = windowSeconds;
}

/// <summary>
/// The limits on what a player may do, the configuration section <c>RateLimits</c> (core-instructions/05-api-contract.md, rate limits;
/// <c>07</c>, spam). Per player: the commands (a burst limit per second and a sustained limit per minute, both must hold), <c>GET /city</c> and <c>GET /content</c>;
/// per address: the dev login. Validated on start: a limit or window out of range stops it. The defaults are the contract's numbers.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>The configuration section's name.</summary>
    public const string SectionName = "RateLimits";

    /// <summary>The highest allowed <see cref="RateRule.Limit"/>: far above any real client; the tests' servers use it to switch a limit off.</summary>
    public const int MaxLimit = 1_000_000;

    /// <summary>The longest allowed <see cref="RateRule.WindowSeconds"/>: an hour.</summary>
    public const int MaxWindowSeconds = 3600;

    /// <summary><c>RateLimits:Commands</c>: every command endpoint, counted together per player.</summary>
    public CommandRateLimits Commands { get; set; } = new();

    /// <summary><c>RateLimits:City</c>: <c>GET /city</c> per player. Default 30 per minute.</summary>
    public RateRule City { get; set; } = new(30, 60);

    /// <summary><c>RateLimits:Content</c>: <c>GET /content</c> per player, a budget apart from the city's. Default 30 per minute.</summary>
    public RateRule Content { get; set; } = new(30, 60);

    /// <summary><c>RateLimits:DevLogin</c>: the dev login per remote address. Default 10 per minute.</summary>
    public RateRule DevLogin { get; set; } = new(10, 60);
}

/// <summary>The two limits of the command endpoints; a command must pass both.</summary>
public sealed class CommandRateLimits
{
    /// <summary><c>RateLimits:Commands:Burst</c>: a short window that allows a quick run of taps. Default 10 per second.</summary>
    public RateRule Burst { get; set; } = new(10, 1);

    /// <summary><c>RateLimits:Commands:Sustained</c>: a long window that stops a script. Default 120 per minute.</summary>
    public RateRule Sustained { get; set; } = new(120, 60);
}
