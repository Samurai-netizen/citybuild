using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands;

/// <summary>
/// A player's command on their own city (<c>core-instructions/05-api-contract.md</c>, <c>POST /api/v1/commands/*</c>): its data and its
/// mutation, nothing else. Only <see cref="CommandPipeline"/> runs a command (<c>03</c>, request lifecycle): it checks the shape, locks the
/// city, checks the receipt, settles, calls <see cref="Apply"/>, and stores the city, the ledger and the receipt. A command never sees the
/// transaction or a repository, so it can't write, skip the receipt or lock another city. Implement it as an immutable record whose
/// properties are the command's data: they are its canonical payload (<see cref="CommandPayload"/>), except the members declared here, other
/// than <see cref="RequestId"/>.
/// </summary>
public interface ICityCommand
{
    /// <summary>The client-generated id of the command, reused on every retry (<c>05</c>, idempotency rules); not <see cref="Guid.Empty"/>.</summary>
    Guid RequestId { get; }

    /// <summary>Which command it is, as stored in <c>command_receipts.command_type</c>, for example <c>place-building</c>; fixed per type.</summary>
    string CommandType { get; }

    /// <summary>The ledger reason of what the mutation pays (<c>COST_*</c>), or <see langword="null"/> when it never pays.</summary>
    LedgerReason? CostReason { get; }

    /// <summary>The ledger reason of what the mutation gives back (<c>REFUND_*</c>), or <see langword="null"/> when it never refunds.</summary>
    LedgerReason? RefundReason { get; }

    /// <summary>
    /// Checks the command's shape before anything touches the database: values the request can't have whatever the city's state (a blank id,
    /// an overlong text). The business rules are <see cref="Apply"/>'s.
    /// </summary>
    /// <returns><see langword="null"/> when the shape is fine, or what is wrong, for the <c>INVALID_ARGUMENT</c> detail.</returns>
    string? ValidateShape();

    /// <summary>
    /// Applies the command's mutation to the settled city through the engine's rules (<c>CityRules</c>, <c>04</c> section 8). A rejection
    /// changes nothing; the pipeline then commits only the settlement.
    /// </summary>
    /// <param name="city">The city's state, settled to now.</param>
    /// <param name="content">The game content.</param>
    /// <returns>The engine's result: applied with what it paid and gave back, or rejected with an error code.</returns>
    MutationResult Apply(CityState city, GameContent content);
}
