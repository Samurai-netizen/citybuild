using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// GreedyCoins: the most coins soonest. Research: only nodes that help the coin chain (<see cref="ContentChain.HelpsCoins"/>), cheapest first.
/// Builds: the basic city, then a station upgrade when the routes are full, then the chain grows (<see cref="Construction.NextExpansion"/>) with every coin; upgrades only when no new
/// building fits on the grid. It never saves coins for research.
/// </summary>
public sealed class GreedyCoinsStrategy : Strategy
{
    /// <inheritdoc />
    public override StrategyKind Kind => StrategyKind.GreedyCoins;

    /// <inheritdoc />
    protected override ResearchDefinition? ChooseResearch(Turn turn) =>
        turn.AvailableResearch().FirstOrDefault(turn.Chain.HelpsCoins);

    /// <inheritdoc />
    protected override bool Build(Turn turn)
    {
        bool wantScience = turn.Content.Research.Any(node => node.Era == turn.City.Era && turn.Chain.HelpsCoins(node)
            && !turn.City.HasCompletedResearch(node.Id));
        if (TryBasic(turn, wantScience, out bool waiting))
        {
            return true;
        }

        if (waiting)
        {
            return false;
        }

        if (TryStationUpgrade(turn, Construction.BasicReserve(turn)))
        {
            return true;
        }

        if (Construction.FreeTile(turn) is not null)
        {
            return Construction.NextExpansion(turn) is { } next && Construction.TryPlace(turn, next, Construction.BasicReserve(turn));
        }

        return Construction.NextUpgrade(turn) is Building upgrade && Construction.TryUpgrade(turn, upgrade, Construction.BasicReserve(turn));
    }
}
