namespace CityBuilder.Application.Persistence;

/// <summary>
/// The port that writes what the repositories staged (<c>core-instructions/03-architecture.md</c>): one call writes every staged change of
/// the request together, inside the transaction <see cref="BeginTransactionAsync"/> began when a city is being changed
/// (<see cref="Cities.CityTransactionRunner"/>).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Writes every change the repositories staged since the last call.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the changes are written.</returns>
    /// <exception cref="ConcurrencyConflictException">A city's stored <c>state_version</c> isn't the one it was loaded with, or a lock
    /// wasn't granted in time.</exception>
    /// <exception cref="DuplicateKeyException">A unique constraint refused a row: another request committed the same key first.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Begins the request's transaction, with the lock timeout of the configuration: a lock the transaction waits for longer than that fails
    /// with <see cref="ConcurrencyConflictException"/>. One transaction per request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the begin.</param>
    /// <returns>The transaction; the caller commits it, and disposing it without a commit rolls it back.</returns>
    /// <exception cref="InvalidOperationException">The request already has a transaction.</exception>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
