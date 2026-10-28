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
    /// Refuses nothing: every integer is an id of the right form, and one that isn't in the city (0, a negative id, another player's) is the
    /// rule's <c>BUILDING_NOT_FOUND</c>.
    /// </summary>
    /// <returns><see langword="null"/>.</returns>
    public string? ValidateShape() => null;

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => CityRules.UpgradeBuilding(city, content, BuildingId);
}
