namespace CityBuilder.Application.Persistence;

/// <summary>
/// A database transaction begun by <see cref="IUnitOfWork.BeginTransactionAsync"/>: every write of the request between the begin and
/// <see cref="CommitAsync"/> is committed together, and disposing it without a commit rolls all of them back and leaves none of them staged
/// for a later <see cref="IUnitOfWork.SaveChangesAsync"/> (<c>core-instructions/06-data-model.md</c>, write transaction pattern). Only
/// <see cref="Cities.CityTransactionRunner"/> begins one.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>Commits the transaction: the writes become visible to other requests, and the city's row lock is released.</summary>
    /// <param name="cancellationToken">Cancels the commit; the transaction then rolls back when it is disposed.</param>
    /// <returns>A task that completes when the transaction is committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken);
}
