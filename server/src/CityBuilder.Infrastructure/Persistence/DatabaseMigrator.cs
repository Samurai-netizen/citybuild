using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.Infrastructure.Persistence;

/// <summary>
/// Applies the committed migrations to the database. The Api calls it at startup in Development only (docs/how-to/add-migration.md);
/// elsewhere migrations are applied on purpose, never as a side effect of starting the server.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>Applies every pending migration in a scope of its own and says which ones it applied.</summary>
    /// <param name="services">The app's services, with <see cref="CityBuilderDbContext"/> registered.</param>
    /// <param name="cancellationToken">Stops the migration when the app shuts down.</param>
    /// <returns>The ids of the applied migrations in the order they ran; empty when the database was up to date.</returns>
    public static async Task<IReadOnlyList<string>> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CityBuilderDbContext context = scope.ServiceProvider.GetRequiredService<CityBuilderDbContext>();
        List<string> pending = [.. await context.Database.GetPendingMigrationsAsync(cancellationToken)];
        await context.Database.MigrateAsync(cancellationToken);
        return pending;
    }
}
