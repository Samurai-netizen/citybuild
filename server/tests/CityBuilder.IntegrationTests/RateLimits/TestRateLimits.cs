using Microsoft.AspNetCore.Hosting;

namespace CityBuilder.IntegrationTests.RateLimits;

/// <summary>
/// The rate limits of the test servers. A test server is driven far faster than a player, and most suites aren't about the limits (the
/// replay storms send dozens of commands in a second), so the shared factories switch every limit to its highest value; the tests of the
/// limits themselves derive a server with the numbers they need, or use <see cref="DefaultRateLimitsApiFactory"/> for the real defaults.
/// </summary>
internal static class TestRateLimits
{
    /// <summary>The configuration key of every limit's <c>Limit</c>.</summary>
    public static readonly string[] LimitKeys =
    [
        "RateLimits:Commands:Burst:Limit", "RateLimits:Commands:Sustained:Limit", "RateLimits:City:Limit", "RateLimits:Content:Limit", "RateLimits:DevLogin:Limit",
    ];

    /// <summary>Sets every limit to the highest the options allow.</summary>
    /// <param name="host">The host builder of the server under test.</param>
    public static void Relax(IWebHostBuilder host)
    {
        foreach (string key in LimitKeys)
        {
            host.UseSetting(key, "1000000");
        }
    }
}
