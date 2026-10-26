using CityBuilder.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CityBuilder.IntegrationTests;

/// <summary>
/// The in-memory server for the tests that need no database (no Docker): a placeholder connection string that names no reachable
/// server, so the required setting is there and the server starts. A test that needs another setting derives its own with
/// <c>WithWebHostBuilder</c>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Sets the placeholder connection string.</summary>
    /// <param name="builder">The host builder of the server under test.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CityBuilder", PersistenceTestContext.UnusedConnectionString);
    }
}
