using CityBuilder.Application.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// A test command that upgrades a building through the engine's rule (<c>CityRules.UpgradeBuilding</c>): it pays <c>COST_UPGRADE</c> and
/// gives a running cycle's inputs back as <c>REFUND_UPGRADE</c>.
/// </summary>
/// <param name="RequestId">The command's request id.</param>
/// <param name="BuildingId">The building to upgrade.</param>
internal sealed record UpgradeTestCommand(Guid RequestId, int BuildingId) : ICityCommand
{
    /// <inheritdoc />
    public string CommandType => "upgrade-test";

    /// <inheritdoc />
    public LedgerReason? CostReason => LedgerReason.CostUpgrade;

    /// <inheritdoc />
    public LedgerReason? RefundReason => LedgerReason.RefundUpgrade;

    /// <inheritdoc />
    public string? ValidateShape() => null;

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => CityRules.UpgradeBuilding(city, content, BuildingId);
}
