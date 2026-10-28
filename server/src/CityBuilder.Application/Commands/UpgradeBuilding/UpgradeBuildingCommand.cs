using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.UpgradeBuilding;

/// <summary>
/// Starts upgrading a building of the acting player's city to its next level (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/upgrade-building</c>; the rule is <see cref="CityRules.UpgradeBuilding"/>, <c>04</c> section 8). It pays the next
/// level's upgrade cost (<c>COST_UPGRADE</c>) and gives a running cycle's inputs back (<c>REFUND_UPGRADE</c>). The data is only the building's
/// id: the level, the cost and the upgrade time come from the content and the city, and the id is looked up in the acting player's city
/// only. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="BuildingId">The building to upgrade, its id in the city.</param>
public sealed record UpgradeBuildingCommand(Guid RequestId, int BuildingId) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "upgrade-building";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => LedgerReason.CostUpgrade;

    /// <inheritdoc />
    public LedgerReason? RefundReason => LedgerReason.RefundUpgrade;

    /// <summary>
    /// Refuses an id below 1, which no city has (ids count from 1 in each city), so such a request never locks or settles the city (owner
    /// directive OD-13). An id of 1 or more is looked up in the acting player's city only: one it doesn't have (one only another city has)
    /// is the rule's <c>BUILDING_NOT_FOUND</c>.
    /// </summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape() => BuildingId >= 1 ? null : "buildingId must be a building id: an integer of at least 1.";

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => CityRules.UpgradeBuilding(city, content, BuildingId);
}
