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
    /// so the repositories and the unit of work of a request share its context: <see cref="ICityRepository"/>,
    /// <see cref="INewCityRepository"/> (both <see cref="CityRepository"/>), <see cref="IPlayerRepository"/> and <see cref="IUnitOfWork"/>;
    /// and <see cref="ICityTransactionRunner"/>, scoped, the one entry point of every change of a stored city. The repositories need a <see cref="TimeProvider"/>, which the Api registers. The settings are read when they are first needed, so the Api
    /// can build them from its validated options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">Gives the persistence settings from the app's services.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCityBuilderPersistence(
        this IServiceCollection services, Func<IServiceProvider, PersistenceSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        services.AddSingleton<PersistenceSettings>(settings);
        services.AddDbContext<CityBuilderDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<PersistenceSettings>().ConnectionString));
        services.AddScoped<ICityRepository, CityRepository>();
        services.AddScoped<INewCityRepository, CityRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICityTransactionRunner, CityTransactionRunner>();
        return services;
    }
}
