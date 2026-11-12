using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.SetPaused;

/// <summary>
/// Pauses or resumes a producer or a courier station of the acting player's city (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/set-paused</c>; the rule is <see cref="CityRules.SetPaused"/>, <c>04</c> sections 4, 6, 8 and 19, CR-0008 D23 and D24).
/// A paused producer finishes its running cycle and starts no new one; a paused station's couriers finish their stop and go home, keeping their
/// routes and lines. Only a flag changes, so it pays and refunds nothing and writes no ledger row. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="BuildingId">The building, its id in the city.</param>
/// <param name="Paused">True to pause, false to resume.</param>
public sealed record SetPausedCommand(Guid RequestId, int BuildingId, bool Paused) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "set-paused";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => null;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>
    /// Refuses an id below 1, which no city has, before the city is locked (owner directive OD-13). Everything else is the rule's: an id the
    /// city doesn't have is <c>BUILDING_NOT_FOUND</c>, a building that does no work <c>INVALID_ARGUMENT</c>, a construction <c>BUILDING_BUSY</c>.
    /// </summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape() => BuildingId < 1 ? "buildingId must be a building id: an integer of at least 1." : null;

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => CityRules.SetPaused(city, content, BuildingId, Paused);
}
