namespace CityBuilder.Application.Persistence;

/// <summary>
/// The port that writes what the repositories staged (<c>core-instructions/03-architecture.md</c>): one call writes every staged change of
/// the request together. M0-22 puts it inside the transaction that holds the city's row lock.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Writes every change the repositories staged since the last call.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the changes are written.</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
