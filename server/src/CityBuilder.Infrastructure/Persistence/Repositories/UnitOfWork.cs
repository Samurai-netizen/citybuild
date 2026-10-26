using CityBuilder.Application.Persistence;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="IUnitOfWork"/> over <see cref="CityBuilderDbContext"/>: one <c>SaveChanges</c> writes what the repositories of the request
/// staged in the shared, scoped context. A stale <c>state_version</c> fails it with EF's concurrency exception; M0-22 maps that and wraps the
/// write in the transaction that holds the city's row lock.
/// </summary>
/// <param name="db">The request's context, the same one the repositories use.</param>
/// <exception cref="ArgumentNullException"><paramref name="db"/> is null.</exception>
public sealed class UnitOfWork(CityBuilderDbContext db) : IUnitOfWork
{
    /// <summary>The request's context.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
