using CityBuilder.Application.Metrics;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Domain.Metrics;

namespace CityBuilder.Application.Cities;

/// <summary>
/// The acting player's city inside its transaction (<see cref="ICityTransactionRunner"/>): the city, loaded under its row lock, the one
/// save of it, and the player events the work records (CR-0009), written in the same transaction. Valid only while the work of
/// <see cref="ICityTransactionRunner.RunAsync"/> runs.
/// </summary>
public sealed class CityTransaction
{
    /// <summary>Stages the city's rows.</summary>
    private readonly ICityRepository _cities;
    /// <summary>Writes the staged rows inside the transaction.</summary>
    private readonly IUnitOfWork _unitOfWork;
    /// <summary>Stages the player events.</summary>
    private readonly IPlayerEventRepository _playerEvents;
    /// <summary>The events recorded since the last write, in order.</summary>
    private readonly List<PlayerEvent> _pendingEvents = [];
    /// <summary>Whether the work has returned or thrown; the city can't be saved, nor an event recorded, after that.</summary>
    private bool _closed;

    /// <summary>Wraps the city the runner has just locked and loaded.</summary>
    /// <param name="city">The locked city.</param>
    /// <param name="cities">The repository that loaded it.</param>
    /// <param name="unitOfWork">The unit of work of the transaction.</param>
    /// <param name="playerEvents">The player events of the transaction.</param>
    internal CityTransaction(CityAggregate city, ICityRepository cities, IUnitOfWork unitOfWork, IPlayerEventRepository playerEvents)
    {
        City = city;
        _cities = cities;
        _unitOfWork = unitOfWork;
        _playerEvents = playerEvents;
    }

    /// <summary>The acting player's city, loaded under its row lock; the work changes it in place.</summary>
    public CityAggregate City { get; }

    /// <summary>Whether <see cref="SaveAsync"/> has run.</summary>
    public bool IsSaved { get; private set; }

    /// <summary>
    /// Stores the city as it is now, with the ledger entries and the receipt of what changed it, and the events recorded so far: the rows are
    /// written inside the transaction (other requests see them once the runner commits) with <c>state_version</c> one above the loaded one.
    /// Once per transaction.
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
        StagePendingEvents();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Records a player event of the city's player (CR-0009): it is written with the city's <see cref="SaveAsync"/>, or, when the work
    /// records it after the save or doesn't save the city, by the runner before the commit. So it exists exactly when the transaction
    /// commits, and a work that throws stores none. Game code never reads it back (<c>02</c> rule 23).
    /// </summary>
    /// <param name="playerEvent">The event; its player is the city's.</param>
    /// <exception cref="ArgumentNullException"><paramref name="playerEvent"/> is null.</exception>
    /// <exception cref="ArgumentException">The event belongs to another player.</exception>
    /// <exception cref="InvalidOperationException">The work has ended.</exception>
    public void Record(PlayerEvent playerEvent)
    {
        ArgumentNullException.ThrowIfNull(playerEvent);
        if (_closed)
        {
            throw new InvalidOperationException("A city transaction records events while its work runs: the transaction has ended.");
        }

        if (playerEvent.PlayerId != City.PlayerId)
        {
            throw new ArgumentException("A city transaction records events of its own city's player only.", nameof(playerEvent));
        }

        _pendingEvents.Add(playerEvent);
    }

    /// <summary>
    /// Writes the events recorded since the last write, if there are any: the runner calls it after the work returned and before the commit
    /// (a work that doesn't save the city, or records after the save). Without events it writes nothing. The city itself isn't saved, so
    /// its rows and <c>state_version</c> stay as they are.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    internal async Task FlushEventsAsync(CancellationToken cancellationToken)
    {
        if (_pendingEvents.Count == 0)
        {
            return;
        }

        StagePendingEvents();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Ends the transaction's work: <see cref="SaveAsync"/> and <see cref="Record"/> throw from now on.</summary>
    internal void Close() => _closed = true;

    /// <summary>Hands the recorded events, if any, to the repository for the next write, in order, and forgets them.</summary>
    private void StagePendingEvents()
    {
        if (_pendingEvents.Count == 0)
        {
            return;
        }

        _playerEvents.Add([.. _pendingEvents]);
        _pendingEvents.Clear();
    }
}
