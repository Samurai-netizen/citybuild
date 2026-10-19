namespace CityBuilder.Application.Cities;

/// <summary>
/// The port to the stored cities (<c>core-instructions/03-architecture.md</c>). A skeleton for the settle use case: M0-20 adds the ledger
/// and the command receipt to <see cref="SaveAsync"/>, and M0-22 the row lock and the transaction around both calls.
/// </summary>
public interface ICityRepository
{
    /// <summary>Loads the player's city to change it. Every player has a city: the first login creates it.</summary>
    /// <param name="playerId">The acting player; a use case never loads another player's city.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The player's city.</returns>
    Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken);

    /// <summary>Stores the city as it is now.</summary>
    /// <param name="city">A city loaded by <see cref="LoadForUpdateAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the city is stored.</returns>
    Task SaveAsync(CityAggregate city, CancellationToken cancellationToken);
}
