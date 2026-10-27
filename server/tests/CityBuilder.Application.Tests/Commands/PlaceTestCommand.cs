using CityBuilder.Application.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// A test command that places a building through the engine's rule (<c>CityRules.PlaceBuilding</c>), as M0-29's command will: it pays
/// <c>COST_PLACE</c> and never refunds. Its shape check refuses a blank type id.
/// </summary>
/// <param name="RequestId">The command's request id.</param>
/// <param name="BuildingTypeId">The type to place.</param>
/// <param name="X">The tile's column.</param>
/// <param name="Y">The tile's row.</param>
internal sealed record PlaceTestCommand(Guid RequestId, string BuildingTypeId, int X, int Y) : ICityCommand
{
    /// <summary>The command type the pipeline stores in the receipt and hashes.</summary>
    public const string Type = "place-test";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => LedgerReason.CostPlace;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <inheritdoc />
    public string? ValidateShape() => string.IsNullOrWhiteSpace(BuildingTypeId) ? "buildingTypeId must not be blank." : null;

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) =>
        CityRules.PlaceBuilding(city, content, new BuildingTypeId(BuildingTypeId), X, Y);
}
