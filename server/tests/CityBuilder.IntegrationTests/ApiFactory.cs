using CityBuilder.IntegrationTests.Auth;
using CityBuilder.IntegrationTests.Persistence;
using CityBuilder.IntegrationTests.RateLimits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CityBuilder.IntegrationTests;

/// <summary>
/// The in-memory server for the tests that need no database (no Docker): a placeholder connection string that names no reachable
/// server, so the required setting is there and the server starts, the tests' token signing key (<see cref="TestTokens.SigningKey"/>),
/// no migration at startup (Development turns it on), and every rate limit at its highest value. A test that needs another setting derives
/// its own with <c>WithWebHostBuilder</c>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Whether the server's rate limits are switched off (set to their highest value), as for every suite that isn't about the limits
    /// (<see cref="TestRateLimits"/>). <see cref="DefaultRateLimitsApiFactory"/> turns it off.
    /// </summary>
    protected virtual bool RelaxRateLimits => true;

    /// <summary>Sets the placeholder connection string and the signing key, turns the startup migration off, and relaxes the rate limits.</summary>
    /// <param name="builder">The host builder of the server under test.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CityBuilder", PersistenceTestContext.UnusedConnectionString);
        builder.UseSetting("Database:ApplyMigrationsAtStartup", "false");
        builder.UseSetting("Auth:SigningKey", TestTokens.SigningKey);
        if (RelaxRateLimits)
        {
            TestRateLimits.Relax(builder);
        }
    }
}
