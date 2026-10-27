using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace CityBuilder.IntegrationTests.Postgres;

/// <summary>
/// The in-memory server on one test's PostgreSQL database: the server's own registrations (repositories, unit of work, content), its
/// connection string pointed at the database, no migration at startup (the database is a migrated copy, or a test turns it on with
/// <c>WithWebHostBuilder</c>), and <see cref="Clock"/> as the server clock.
/// </summary>
/// <param name="database">The test's database.</param>
public sealed class PostgresApiFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    /// <summary>The instant the server clock starts at.</summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The server clock; a test advances it explicitly.</summary>
    public FakeTimeProvider Clock { get; } = new(Start);

    /// <summary>Points the server at the database, turns the startup migration off and replaces the clock.</summary>
    /// <param name="builder">The host builder of the server under test.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CityBuilder", database.ConnectionString);
        builder.UseSetting("Database:ApplyMigrationsAtStartup", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
