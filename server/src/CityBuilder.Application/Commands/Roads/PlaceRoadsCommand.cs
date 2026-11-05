using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.Roads;

/// <summary>
/// Builds a level-1 road on every tile of a list in the acting player's city (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/place-roads</c>; the rule is <see cref="RoadRules.PlaceRoads"/>, <c>04</c> sections 8 and 17): all or nothing,
/// at once, without a builder, for level 1's coins per tile (<c>COST_ROAD</c>). The data is the tiles only: what lies there and the cost come
/// from the city and the content. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="Tiles">The tiles, in the player's order; 1 to 64, each once.</param>
public sealed record PlaceRoadsCommand(Guid RequestId, IReadOnlyList<CommandTile?> Tiles) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "place-roads";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => LedgerReason.CostRoad;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>Refuses a list no city could take (<see cref="RoadTileList.ValidateShape"/>, OD-13) before the city is locked.</summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape() => RoadTileList.ValidateShape(Tiles);

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => RoadRules.PlaceRoads(city, content, RoadTileList.ToEngine(Tiles));
}
