using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.Infrastructure.Persistence;

/// <summary>Registers the EF Core persistence on PostgreSQL in the Api's composition root (core-instructions/03-architecture.md).</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CityBuilderDbContext"/> (scoped, one per request) on the Npgsql provider. The connection string is read
    /// when the first context is created, so the Api can take it from its validated options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Gives the connection string from the app's services.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCityBuilderPersistence(
        this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        return services.AddDbContext<CityBuilderDbContext>(
            (provider, options) => options.UseNpgsql(connectionString(provider)));
    }
}
