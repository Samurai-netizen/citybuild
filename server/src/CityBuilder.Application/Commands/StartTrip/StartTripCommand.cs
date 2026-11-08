using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.StartTrip;

/// <summary>
/// Sends a free worker with goods from one building's store to another building of the acting player's city
/// (<c>core-instructions/05-api-contract.md</c>, <c>POST /api/v1/commands/start-trip</c>; the rule is <see cref="TripRules.StartTrip"/>,
/// <c>04</c> sections 8 and 18). Goods only move, so it pays and refunds nothing and writes no ledger row (<c>06</c>). The data is what the
/// player chose: the walk, the load and the trip's id come from the city and the content. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="SourceId">The building whose store the goods come from.</param>
/// <param name="TargetId">The building they go to.</param>
/// <param name="ItemId">The good, an item id of the content.</param>
/// <param name="Amount">How much of the source's stock, in the API's form (<c>05</c> → Amounts): a preset id, <c>1%</c>…<c>100%</c>, or a number.</param>
public sealed record StartTripCommand(Guid RequestId, int SourceId, int TargetId, string ItemId, string Amount) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "start-trip";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => null;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>
    /// Refuses what no city and no content could make valid, before the city is locked (OD-13): building ids below 1 or equal (a trip goes
    /// somewhere else), an item id that can't be a content id, and an amount that is none of the three forms or a percentage outside 1–100
    /// (<see cref="Simulation.State.Amount.TryParse"/>). A known form is a question for the city and the content: an unknown preset is the
    /// rule's <c>INVALID_ARGUMENT</c>, a locked mode its <c>AMOUNT_MODE_LOCKED</c>.
    /// </summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape()
    {
        if (SourceId < 1 || TargetId < 1)
        {
            return "sourceId and targetId must be at least 1.";
        }

        if (SourceId == TargetId)
        {
            return "sourceId and targetId must be different buildings.";
        }

        if (!ContentIds.IsValid(ItemId))
        {
            return "itemId must be a content id: " + ContentIds.FormatDescription + ".";
        }

        return Simulation.State.Amount.TryParse(Amount, out _)
            ? null
            : "amount must be a preset id, a percentage from \"1%\" to \"100%\", or a number of 1 to 12 digits without a leading zero.";
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The amount has no valid form: the pipeline checks the shape first, so this is a programmer error.</exception>
    public MutationResult Apply(CityState city, GameContent content) =>
        TripRules.StartTrip(
            city,
            content,
            SourceId,
            TargetId,
            new ItemId(ItemId),
            Simulation.State.Amount.TryParse(Amount, out Amount? amount)
                ? amount
                : throw new InvalidOperationException("The amount's shape was not checked before the command was applied."));
}
