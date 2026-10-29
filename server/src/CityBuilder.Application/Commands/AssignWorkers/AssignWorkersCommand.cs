using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.AssignWorkers;

/// <summary>
/// Sets how many workers a producer of the acting player's city has (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/assign-workers</c>; the rule is <see cref="CityRules.AssignWorkers"/>, <c>04</c> sections 3 and 8). The count is
/// absolute, and 0 pauses the building; a running cycle keeps the labor done so far. Workers are no resource, so it writes no ledger rows. The
/// data is the building's id and the count: the free workers and the level's max come from the city and the content, and the id is looked up
/// in the acting player's city only. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="BuildingId">The building, its id in the city.</param>
/// <param name="Workers">The building's new worker count.</param>
public sealed record AssignWorkersCommand(Guid RequestId, int BuildingId, int Workers) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "assign-workers";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => null;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>
    /// Refuses an id below 1, which no city has (ids count from 1 in each city), and a negative count, which no building can have, so such a
    /// request never locks or settles the city (owner directive OD-13). Everything else is the rule's: an id the city doesn't have is
    /// <c>BUILDING_NOT_FOUND</c>, a count above the level's max <c>WORKER_LIMIT</c>, more than the free workers <c>INSUFFICIENT_WORKERS</c>.
    /// </summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape() => BuildingId < 1 ? "buildingId must be a building id: an integer of at least 1."
        : Workers < 0 ? "workers must be a worker count: an integer of at least 0."
        : null;

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) => CityRules.AssignWorkers(city, content, BuildingId, Workers);
}
