using CityBuilder.Domain.Commands;

namespace CityBuilder.Application.Commands;

/// <summary>
/// The port to the stored command receipts (<c>core-instructions/06-data-model.md</c>, <c>command_receipts</c>): it only reads them; a receipt
/// is written with the city it describes (<see cref="Cities.CityTransaction.SaveAsync"/>). Used by <see cref="CommandPipeline"/>: inside the
/// city's transaction after the row lock, and after a lost receipt race, outside one.
/// </summary>
public interface ICommandReceiptRepository
{
    /// <summary>
    /// Finds the receipt of a player's command. The key includes the player, so another player's request id never finds it
    /// (<c>05</c>, idempotency rules).
    /// </summary>
    /// <param name="playerId">The acting player.</param>
    /// <param name="requestId">The command's request id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The receipt, or <see langword="null"/> when this player has none with the id.</returns>
    Task<CommandReceipt?> FindAsync(Guid playerId, Guid requestId, CancellationToken cancellationToken);
}
