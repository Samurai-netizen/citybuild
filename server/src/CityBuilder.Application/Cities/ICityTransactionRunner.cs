namespace CityBuilder.Application.Cities;

/// <summary>
/// The one way a use case changes a city (<c>core-instructions/06-data-model.md</c>, write transaction pattern): it runs the work inside a
/// transaction that holds the row lock of the acting player's own city, so two requests on one city run one after the other, and a request
/// never locks another player's city. Scoped, one per request; a request runs it once.
/// </summary>
public interface ICityTransactionRunner
{
    /// <summary>
    /// Begins the transaction, locks and loads the player's city, runs <paramref name="work"/> on it, and commits when the work returns. If
    /// anything throws, nothing is committed: the transaction rolls back and the exception propagates.
    /// </summary>
    /// <typeparam name="TResult">What the work returns.</typeparam>
    /// <param name="playerId">The acting player, from the authenticated request; the transaction locks only this player's city.</param>
    /// <param name="work">The use case's work on the locked city; it stores the city with <see cref="CityTransaction.SaveAsync"/>, at
    /// most once.</param>
    /// <param name="cancellationToken">Cancels the transaction; nothing is committed then.</param>
    /// <returns>What the work returned, once the transaction is committed.</returns>
    /// <exception cref="Persistence.ConcurrencyConflictException">The lock wasn't granted within the lock timeout, or the city changed
    /// without it; nothing was committed and the request is safe to retry.</exception>
    /// <exception cref="InvalidOperationException">This request already ran a city transaction, or the player has no city.</exception>
    Task<TResult> RunAsync<TResult>(
        Guid playerId, Func<CityTransaction, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken);
}
