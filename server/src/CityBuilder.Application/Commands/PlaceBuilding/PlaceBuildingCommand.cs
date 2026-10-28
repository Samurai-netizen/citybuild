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
    /// Refuses what no city and no content could make valid, so it never locks or settles the city (M0-30; owner directive OD-13): a type id
    /// that can't be a content id (<see cref="ContentIds"/>: blank, too long, other characters) and a negative coordinate (the grid starts at
    /// 0). An id of the right form is a question for the content (<c>UNKNOWN_BUILDING_TYPE</c>), and a coordinate past the grid's width or
    /// height, which the content sets, is the rule's <c>OUT_OF_BOUNDS</c>.
    /// </summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape()
    {
        if (!ContentIds.IsValid(BuildingTypeId))
        {
            return "buildingTypeId must be a content id: " + ContentIds.FormatDescription + ".";
        }

        return X >= 0 && Y >= 0 ? null : "x and y must not be negative: the grid starts at 0.";
    }

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) =>
        CityRules.PlaceBuilding(city, content, new BuildingTypeId(BuildingTypeId), X, Y);
}
