using CityBuilder.Application.Metrics;
using CityBuilder.Application.Persistence;

namespace CityBuilder.Application.Cities;

/// <summary>
/// <see cref="ICityTransactionRunner"/> over the ports: <see cref="IUnitOfWork.BeginTransactionAsync"/> gives the transaction and
/// <see cref="ICityRepository.LoadForUpdateAsync"/> the row lock (<c>SELECT … FOR UPDATE</c>). It is the only Application type that uses
/// <see cref="ICityRepository"/> (an architecture test checks it), so no use case loads or writes a city without the lock. Scoped, one per
/// request, and it runs once: a request never locks a second city (<c>core-instructions/06-data-model.md</c>, lock ordering). The player
/// events the work recorded and didn't write with the city's save are written after the work and before the commit (CR-0009).
/// </summary>
/// <param name="cities">The stored cities.</param>
/// <param name="unitOfWork">The request's unit of work: the transaction and the writes.</param>
/// <param name="playerEvents">The request's player events (CR-0009).</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class CityTransactionRunner(ICityRepository cities, IUnitOfWork unitOfWork, IPlayerEventRepository playerEvents)
    : ICityTransactionRunner
{
    /// <summary>Locks and loads the acting player's city.</summary>
    private readonly ICityRepository _cities = cities ?? throw new ArgumentNullException(nameof(cities));
    /// <summary>Begins and commits the transaction, and writes the saved city.</summary>
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    /// <summary>Stages the player events the work records.</summary>
    private readonly IPlayerEventRepository _playerEvents = playerEvents ?? throw new ArgumentNullException(nameof(playerEvents));
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
        var cityTransaction = new CityTransaction(city, _cities, _unitOfWork, _playerEvents);
        try
        {
            TResult result = await work(cityTransaction, cancellationToken);
            // The events of a work that didn't save the city (a refusal at an unchanged instant), or recorded after its save.
            await cityTransaction.FlushEventsAsync(cancellationToken);
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
