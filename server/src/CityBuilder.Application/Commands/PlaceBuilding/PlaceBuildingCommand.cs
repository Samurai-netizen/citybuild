using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.PlaceBuilding;

/// <summary>
/// Places a building of a type on a tile of the acting player's city (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/place-building</c>; the rule is <see cref="CityRules.PlaceBuilding"/>, <c>04</c> section 8). It pays the type's
/// cost (<c>COST_PLACE</c>) and never refunds. The data is what the player chose and nothing else: the cost, the build time, the level and the
/// building's id come from the content and the city. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="BuildingTypeId">The type to place, a building id of the content.</param>
/// <param name="X">The tile's column.</param>
/// <param name="Y">The tile's row.</param>
public sealed record PlaceBuildingCommand(Guid RequestId, string BuildingTypeId, int X, int Y) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "place-building";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => LedgerReason.CostPlace;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>
    /// Refuses a missing or blank type id; any other text is a question for the content (<c>UNKNOWN_BUILDING_TYPE</c>), and coordinates
    /// outside the grid are the rule's <c>OUT_OF_BOUNDS</c>.
    /// </summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape() => string.IsNullOrWhiteSpace(BuildingTypeId) ? "buildingTypeId must not be blank." : null;

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) =>
        CityRules.PlaceBuilding(city, content, new BuildingTypeId(BuildingTypeId), X, Y);
}
