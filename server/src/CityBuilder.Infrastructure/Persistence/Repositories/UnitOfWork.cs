using System.Globalization;
using CityBuilder.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CityBuilder.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="IUnitOfWork"/> over <see cref="CityBuilderDbContext"/>: one <c>SaveChanges</c> writes what the repositories of the request
/// staged in the shared, scoped context, inside the request's transaction when one was begun. A stale <c>state_version</c> (EF's
/// concurrency check) and a lock timeout become <see cref="ConcurrencyConflictException"/>.
/// </summary>
/// <param name="db">The request's context, the same one the repositories use.</param>
/// <param name="settings">The persistence settings; the lock timeout of a transaction comes from them.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class UnitOfWork(CityBuilderDbContext db, PersistenceSettings settings) : IUnitOfWork
{
    /// <summary>The request's context.</summary>
    private readonly CityBuilderDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    /// <summary>The persistence settings.</summary>
    private readonly PersistenceSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // UPDATE cities … WHERE state_version = <loaded> matched no row: something wrote the city without its lock (06).
            throw new ConcurrencyConflictException(
                "The city's stored state_version is not the one it was loaded with: it was written without its row lock.", exception);
        }
        catch (DbUpdateException exception) when (LockTimeouts.IsLockTimeout(exception))
        {
            throw new ConcurrencyConflictException("A write waited for a lock longer than the lock timeout.", exception);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The transaction runs at PostgreSQL's default isolation, read committed: once the city's row lock is granted, each statement sees what
    /// the transaction before it committed. <c>lock_timeout</c> is set for this transaction only (<c>set_config(…, true)</c>, as
    /// <c>SET LOCAL</c>).
    /// </remarks>
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("The request already has a transaction; a request runs one.");
        }

        IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            string lockTimeout = _settings.LockTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms";
            await _db.Database.ExecuteSqlAsync($"SELECT set_config('lock_timeout', {lockTimeout}, true)", cancellationToken);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }

        return new Transaction(transaction);
    }

    /// <summary>The request's EF Core transaction as the Application's port: disposing it uncommitted rolls it back.</summary>
    /// <param name="transaction">The transaction EF Core began.</param>
    private sealed class Transaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        /// <inheritdoc />
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
