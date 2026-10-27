using CityBuilder.Application.Persistence;

namespace CityBuilder.Application.Cities;

/// <summary>
/// <see cref="ICityTransactionRunner"/> over the ports: <see cref="IUnitOfWork.BeginTransactionAsync"/> gives the transaction and
/// <see cref="ICityRepository.LoadForUpdateAsync"/> the row lock (<c>SELECT … FOR UPDATE</c>). It is the only Application type that uses
/// <see cref="ICityRepository"/> (an architecture test checks it), so no use case loads or writes a city without the lock. Scoped, one per
/// request, and it runs once: a request never locks a second city (<c>core-instructions/06-data-model.md</c>, lock ordering).
/// </summary>
/// <param name="cities">The stored cities.</param>
/// <param name="unitOfWork">The request's unit of work: the transaction and the writes.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class CityTransactionRunner(ICityRepository cities, IUnitOfWork unitOfWork) : ICityTransactionRunner
{
    /// <summary>Locks and loads the acting player's city.</summary>
    private readonly ICityRepository _cities = cities ?? throw new ArgumentNullException(nameof(cities));
    /// <summary>Begins and commits the transaction, and writes the saved city.</summary>
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    /// <summary>Whether this runner has run; a request runs one city transaction.</summary>
    private bool _hasRun;

    /// <inheritdoc />
    public async Task<TResult> RunAsync<TResult>(
        Guid playerId, Func<CityTransaction, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (_hasRun)
        {
            // Also a nested run: a request locks one city, its own. Another player's city is reached through a mailbox (06, lock ordering).
            throw new InvalidOperationException(
                "This request has already run a city transaction: a request locks only the acting player's city, once.");
        }

        _hasRun = true;
        await using IUnitOfWorkTransaction transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        CityAggregate city = await _cities.LoadForUpdateAsync(playerId, cancellationToken);
        var cityTransaction = new CityTransaction(city, _cities, _unitOfWork);
        try
        {
            TResult result = await work(cityTransaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            // An exception leaves the commit out: disposing the transaction rolls back every write of the work.
            cityTransaction.Close();
        }
    }
}
