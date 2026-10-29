namespace CityBuilder.IntegrationTests.RateLimits;

/// <summary>
/// The in-memory server (<see cref="ApiFactory"/>) with the rate limits the configuration gives it by default, the contract's numbers,
/// instead of the relaxed ones of the other suites: for the tests that a player's normal pace is never limited.
/// </summary>
public sealed class DefaultRateLimitsApiFactory : ApiFactory
{
    /// <summary>The defaults are the point of this server.</summary>
    protected override bool RelaxRateLimits => false;
}
