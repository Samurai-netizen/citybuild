using CityBuilder.Domain.Players;

namespace CityBuilder.Application.Players;

/// <summary>
/// The port to the stored players (<c>core-instructions/06-data-model.md</c>, <c>players</c>). The dev login (M0-25) finds a player by
/// name or adds a new one; <see cref="Persistence.IUnitOfWork.SaveChangesAsync"/> writes the addition.
/// </summary>
public interface IPlayerRepository
{
    /// <summary>Finds the player with the dev login name.</summary>
    /// <param name="devName">The name, compared exactly.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The player, or <see langword="null"/> when no player has that name.</returns>
    Task<Player?> FindByDevNameAsync(string devName, CancellationToken cancellationToken);

    /// <summary>Stages a new player for the next <see cref="Persistence.IUnitOfWork.SaveChangesAsync"/>.</summary>
    /// <param name="player">The new player; its id and name must not be stored yet.</param>
    void Add(Player player);
}
