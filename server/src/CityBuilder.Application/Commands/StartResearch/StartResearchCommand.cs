using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.StartResearch;

/// <summary>
/// Starts a research node in the acting player's city (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/start-research</c>; the rule is <see cref="CityRules.StartResearch"/>, <c>04</c> sections 5 and 8). It pays the
/// node's full cost up front (<c>COST_RESEARCH</c>) and never refunds; the research completes on its own during a later settle, which applies
/// its effects. The data is the node's id and nothing else: the cost, the duration, the prerequisites and the era come from the content and the
/// city. <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="ResearchId">The node to start, a research id of the content.</param>
public sealed record StartResearchCommand(Guid RequestId, string ResearchId) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "start-research";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => LedgerReason.CostResearch;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>
    /// Refuses a research id that can't be a content id (<see cref="ContentIds"/>: blank, too long, other characters), which no content could
    /// make valid, so such a request never locks or settles the city (owner directive OD-13). An id of the right form is a question for the
    /// content and the city: <c>UNKNOWN_RESEARCH</c>, <c>RESEARCH_LOCKED</c> and the rest are the rule's.
    /// </summary>
    /// <returns><see langword="null"/>, or what is wrong.</returns>
    public string? ValidateShape() =>
        ContentIds.IsValid(ResearchId) ? null : "researchId must be a content id: " + ContentIds.FormatDescription + ".";

    /// <inheritdoc />
    public MutationResult Apply(CityState city, GameContent content) =>
        CityRules.StartResearch(city, content, new ResearchId(ResearchId));
}
