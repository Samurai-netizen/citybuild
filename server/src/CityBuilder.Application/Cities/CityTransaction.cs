using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;

namespace CityBuilder.Application.Cities;

/// <summary>
/// The acting player's city inside its transaction (<see cref="ICityTransactionRunner"/>): the city, loaded under its row lock, and the one
/// save of it. Valid only while the work of <see cref="ICityTransactionRunner.RunAsync"/> runs.
/// </summary>
public sealed class CityTransaction
{
    /// <summary>Stages the city's rows.</summary>
    private readonly ICityRepository _cities;
    /// <summary>Writes the staged rows inside the transaction.</summary>
    private readonly IUnitOfWork _unitOfWork;
    /// <summary>Whether the work has returned or thrown; the city can't be saved after that.</summary>
    private bool _closed;

    /// <summary>Wraps the city the runner has just locked and loaded.</summary>
    /// <param name="city">The locked city.</param>
    /// <param name="cities">The repository that loaded it.</param>
    /// <param name="unitOfWork">The unit of work of the transaction.</param>
    internal CityTransaction(CityAggregate city, ICityRepository cities, IUnitOfWork unitOfWork)
    {
        City = city;
        _cities = cities;
        _unitOfWork = unitOfWork;
    }

    /// <summary>The acting player's city, loaded under its row lock; the work changes it in place.</summary>
    public CityAggregate City { get; }

    /// <summary>Whether <see cref="SaveAsync"/> has run.</summary>
    public bool IsSaved { get; private set; }

    /// <summary>
    /// Stores the city as it is now, with the ledger entries and the receipt of what changed it: the rows are written inside the transaction
    /// (other requests see them once the runner commits) with <c>state_version</c> one above the loaded one. Once per transaction.
    /// </summary>
    /// <param name="ledger">The ledger entries of the city's changes since it was loaded, in order; empty when there are none.</param>
    /// <param name="receipt">The receipt of the successful command that changed the city, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    /// <exception cref="InvalidOperationException">The city was saved already, or the work has ended.</exception>
    /// <exception cref="ConcurrencyConflictException">The stored <c>state_version</c> isn't the loaded one: something wrote the city
    /// without its lock.</exception>
    public async Task SaveAsync(IReadOnlyList<LedgerEntry> ledger, CommandReceipt? receipt, CancellationToken cancellationToken)
    {
        if (_closed || IsSaved)
        {
            throw new InvalidOperationException(
                "A city transaction saves its city once, while its work runs: the next version needs a new request.");
        }

        IsSaved = true;
        _cities.Save(City, ledger, receipt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Ends the transaction's work: <see cref="SaveAsync"/> throws from now on.</summary>
    internal void Close() => _closed = true;
}
