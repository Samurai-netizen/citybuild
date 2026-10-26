using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CityBuilder.Infrastructure.Persistence;

/// <summary>
/// Builds the context for the dotnet-ef tool (docs/how-to/add-migration.md), so <c>dotnet ef migrations add</c> needs no configuration
/// and never starts the server. Only the tool uses it; the server registers the context with
/// <see cref="PersistenceServiceCollectionExtensions.AddCityBuilderPersistence"/>.
/// </summary>
public sealed class CityBuilderDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CityBuilderDbContext>
{
    /// <summary>
    /// A connection string that names no reachable server. Creating a migration doesn't connect; a command that does (such as
    /// <c>dotnet ef database update</c>) gets the real one with <c>--connection</c> and fails clearly without it.
    /// </summary>
    private const string _placeholderConnectionString = "Host=set-the-connection-with--connection.invalid;Database=citybuilder";

    /// <summary>A context on the Npgsql provider, the one the server uses, so the migration has PostgreSQL's types.</summary>
    /// <param name="args">Arguments the tool passes after <c>--</c>; not used.</param>
    /// <returns>The context.</returns>
    public CityBuilderDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CityBuilderDbContext>().UseNpgsql(_placeholderConnectionString).Options);
}
