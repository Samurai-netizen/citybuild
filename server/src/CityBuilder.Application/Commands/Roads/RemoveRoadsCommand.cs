using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.Roads;

/// <summary>
/// Takes the road off every tile of a list in the acting player's city (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/remove-roads</c>; the rule is <see cref="RoadRules.RemoveRoads"/>, <c>04</c> sections 8 and 17): all or nothing,
/// at once. Nothing is paid and nothing refunded (CR-0008 D6), so it writes no ledger rows. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="Tiles">The tiles, in the player's order; 1 to 64, each once.</param>
public sealed record RemoveRoadsCommand(Guid RequestId, IReadOnlyList<CommandTile?> Tiles) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "remove-roads";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => null;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>Refuses a list no city could take (<see cref="RoadTileList.ValidateShape"/>, OD-13) before the city is locked.</summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape() => RoadTileList.ValidateShape(Tiles);

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => RoadRules.RemoveRoads(city, content, RoadTileList.ToEngine(Tiles));
}
