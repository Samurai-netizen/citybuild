using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;

namespace CityBuilder.Application.Cities;

/// <summary>
/// The port to the stored cities (<c>core-instructions/03-architecture.md</c>, <c>06-data-model.md</c>). <see cref="Save"/> only stages
/// the writes; <see cref="Persistence.IUnitOfWork.SaveChangesAsync"/> writes them. Only <see cref="CityTransactionRunner"/> uses it, inside the
/// transaction that holds the city's row lock; use cases go through <see cref="ICityTransactionRunner"/>.
/// </summary>
public interface ICityRepository
{
    /// <summary>
    /// Locks the player's city row for the rest of the transaction (<c>SELECT … FOR UPDATE</c>) and loads the city to change it. Every
    /// player has a city: the first login creates it.
    /// </summary>
    /// <param name="playerId">The acting player; a request never locks another player's city.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The player's city.</returns>
    /// <exception cref="InvalidOperationException">The player has no city, no transaction is running, or this request has already loaded a
    /// city.</exception>
    /// <exception cref="Persistence.ConcurrencyConflictException">Another transaction held the lock for longer than the lock timeout.</exception>
    Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages the city as it is now, with the ledger entries and the receipt of what changed it, for the next
    /// <see cref="Persistence.IUnitOfWork.SaveChangesAsync"/>. A city this repository didn't load is new and is inserted. The stored version
    /// becomes <see cref="CityAggregate.StateVersion"/> + 1, so save an aggregate once after it was loaded or created; load the city again
    /// for a further change.
    /// </summary>
    /// <param name="city">The city, loaded by <see cref="LoadForUpdateAsync"/> or new.</param>
    /// <param name="ledger">The ledger entries of the city's changes since it was loaded, in order; empty when there are none.</param>
    /// <param name="receipt">The receipt of the successful command that changed the city, or <see langword="null"/> (a settle, a
    /// rejected command).</param>
    void Save(CityAggregate city, IReadOnlyList<LedgerEntry> ledger, CommandReceipt? receipt);
}
