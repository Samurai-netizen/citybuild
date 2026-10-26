using CityBuilder.Application.Cities;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Players;
using CityBuilder.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.Infrastructure.Persistence;

/// <summary>Registers the EF Core persistence on PostgreSQL in the Api's composition root (core-instructions/03-architecture.md).</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CityBuilderDbContext"/> (scoped, one per request) on the Npgsql provider, and the ports over it, scoped as well
    /// so the repositories and the unit of work of a request share its context: <see cref="ICityRepository"/>, <see cref="IPlayerRepository"/>
    /// and <see cref="IUnitOfWork"/>. The repositories need a <see cref="TimeProvider"/>, which the Api registers. The connection string is
    /// read when the first context is created, so the Api can take it from its validated options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Gives the connection string from the app's services.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCityBuilderPersistence(
        this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddDbContext<CityBuilderDbContext>((provider, options) => options.UseNpgsql(connectionString(provider)));
        services.AddScoped<ICityRepository, CityRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
