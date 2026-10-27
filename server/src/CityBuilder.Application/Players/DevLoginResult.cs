namespace CityBuilder.Application.Players;

/// <summary>What <see cref="DevLogin.ExecuteAsync"/> returns.</summary>
/// <param name="PlayerId">The logged-in player.</param>
/// <param name="IsNewPlayer">Whether this login created the player and their starter city.</param>
public sealed record DevLoginResult(Guid PlayerId, bool IsNewPlayer);
