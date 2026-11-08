using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.ReenergizeCouriers;

/// <summary>
/// Re-energizes every courier of the acting player's city (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/reenergize-couriers</c>; the rule is <see cref="CourierRules.ReenergizeCouriers"/>, <c>04</c> sections 2, 8 and
/// 19): each courier's stamina is its station level's again, and a resting one stands at its station and leaves in the closing advance. It
/// is intent only, with no data beyond its request id: the stamina comes from the content. It always succeeds, pays and refunds nothing and
/// writes no ledger row. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
public sealed record ReenergizeCouriersCommand(Guid RequestId) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "reenergize-couriers";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => null;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>The command has no data, so every shape is fine.</summary>
    /// <returns><see langword="null"/>.</returns>
    public string? ValidateShape() => null;

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => CourierRules.ReenergizeCouriers(city, content);
}
