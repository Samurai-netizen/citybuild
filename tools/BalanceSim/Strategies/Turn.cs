using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>What a strategy sees and uses on one decision step while the player is checked in.</summary>
/// <param name="City">The city, settled to now.</param>
/// <param name="Content">The game content.</param>
/// <param name="Chain">The content's coin chain and special building types.</param>
/// <param name="Actions">The player's actions.</param>
/// <param name="IsSessionStart">Whether this is the first step of a check-in.</param>
public sealed record Turn(CityState City, GameContent Content, ContentChain Chain, CityActions Actions, bool IsSessionStart)
{
    /// <summary>A building's type definition (the city and the content belong together, so it always exists).</summary>
    /// <param name="building">The building.</param>
    /// <returns>Its definition.</returns>
    public BuildingDefinition DefinitionOf(Building building) =>
        Content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition)
            ? definition
            : throw new InvalidOperationException($"The content has no building type {building.TypeId}.");

    /// <summary>The coins in a cost (its currency part).</summary>
    /// <param name="cost">The cost.</param>
    /// <returns>The coins.</returns>
    public long CoinsOf(IReadOnlyList<ItemQuantity> cost) => cost.Where(part => part.Item == Content.Currency).Sum(part => part.Quantity);

    /// <summary>The research nodes the city may start now (its era, not done, not running, prerequisites done), cheapest first.</summary>
    /// <returns>The nodes: by coins, then by goods in the cost, then by id (ordinal).</returns>
    public IEnumerable<ResearchDefinition> AvailableResearch() => Content.Research
        .Where(node => node.Era == City.Era && !City.HasCompletedResearch(node.Id) && City.ActiveResearch?.Id != node.Id
            && node.Prerequisites.All(City.HasCompletedResearch))
        .OrderBy(CoinsOfResearch)
        .ThenBy(node => node.Cost.Where(part => part.Item != Content.Currency).Sum(part => part.Quantity))
        .ThenBy(node => node.Id.Value, StringComparer.Ordinal);

    /// <summary>Whether every research node of the city's era is done.</summary>
    /// <returns>True when nothing is left to research.</returns>
    public bool AllResearchDone() => Content.Research.Where(node => node.Era == City.Era).All(node => City.HasCompletedResearch(node.Id));

    /// <summary>Whether a builder slot is free.</summary>
    /// <returns>True when one more construction or upgrade may start.</returns>
    public bool HasFreeBuilder() => CityQueries.BuildersBusy(City) < Content.Rules.BuilderSlots;

    /// <summary>The coins of a research node's cost.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The coins.</returns>
    public long CoinsOfResearch(ResearchDefinition node) => CoinsOf(node.Cost);
}
