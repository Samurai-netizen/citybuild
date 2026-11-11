using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.CancelDelivery;

/// <summary>
/// Cancels one delivery line of a courier in the acting player's city, or all of its lines and its route
/// (<c>core-instructions/05-api-contract.md</c>, <c>POST /api/v1/commands/cancel-delivery</c>; the rule is <see cref="CourierRules.CancelDelivery"/>,
/// <c>04</c> sections 8 and 18, CR-0008 D18). A courier whose running work goes gives its load back to where it came from and starts again
/// from its station. Goods only move, so it pays and refunds nothing and writes no ledger row. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="StationId">The courier station's building id.</param>
/// <param name="Slot">The courier's slot at the station.</param>
/// <param name="LineId">The line to cancel, or <see langword="null"/> for all of the courier's lines and its route.</param>
public sealed record CancelDeliveryCommand(Guid RequestId, int StationId, int Slot, int? LineId) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "cancel-delivery";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => null;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>Refuses what no city could make valid, before the city is locked (OD-13): a station id, a slot or a line id below 1.</summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape()
    {
        if (StationId < 1 || Slot < 1)
        {
            return "stationId and slot must be at least 1.";
        }

        return LineId is < 1 ? "lineId must be at least 1, or null for every line." : null;
    }

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => CourierRules.CancelDelivery(city, content, StationId, Slot, LineId);
}
