using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// ResearchRush: every research node as soon as possible. Research: always keeps the slot busy, cheapest available node first. Builds: the
/// basic city, then (a station upgrade when the routes are full, and) the chain grows only with the coins above the next research's cost, and nothing is upgraded until every node is done;
/// after that it plays like <see cref="BalancedStrategy"/>.
/// </summary>
public sealed class ResearchRushStrategy : Strategy
{
    /// <summary>What the strategy does once every node is done.</summary>
    private readonly BalancedStrategy _afterResearch = new();

    /// <inheritdoc />
    public override StrategyKind Kind => StrategyKind.ResearchRush;

    /// <inheritdoc />
    protected override ResearchDefinition? ChooseResearch(Turn turn) => turn.AvailableResearch().FirstOrDefault();

    /// <inheritdoc />
    protected override bool Build(Turn turn)
    {
        if (turn.AllResearchDone())
        {
            return _afterResearch.BuildBalanced(turn);
        }

        if (TryBasic(turn, wantScience: true, out bool waiting))
        {
            return true;
        }

        if (waiting)
        {
            return false;
        }

        long reserve = CheapestResearchCoins(turn) + Construction.BasicReserve(turn);
        return TryStationUpgrade(turn, reserve)
            || (Construction.FreeTile(turn) is not null && Construction.NextExpansion(turn) is { } next && Construction.TryPlace(turn, next, reserve));
    }
}
