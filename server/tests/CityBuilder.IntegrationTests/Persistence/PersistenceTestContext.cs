using CityBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>Builds a <see cref="CityBuilderDbContext"/> on the Npgsql provider for tests that read the model and never touch a database.</summary>
internal static class PersistenceTestContext
{
    /// <summary>A connection string that names no reachable server; a test that tried to connect would fail at once.</summary>
    public const string UnusedConnectionString = "Host=unused.invalid;Database=unused;Username=unused";

    /// <summary>A new context with the provider the server uses, so the model has PostgreSQL's types.</summary>
    /// <returns>The context; the caller disposes it.</returns>
    public static CityBuilderDbContext Create() =>
        new(new DbContextOptionsBuilder<CityBuilderDbContext>().UseNpgsql(UnusedConnectionString).Options);
}
