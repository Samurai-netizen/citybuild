using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// Balanced: research and growth together. Research: every node, cheapest first. Builds: the basic city, then new chain buildings and upgrades
/// in turn (after a placement it upgrades if it can); while the research slot is free it keeps back the coins of the cheapest node.
/// </summary>
public sealed class BalancedStrategy : Strategy
{
    /// <summary>Whether the last thing this strategy ordered was a placement (then an upgrade comes next, if one can).</summary>
    private bool _lastWasPlacement;

    /// <inheritdoc />
    public override StrategyKind Kind => StrategyKind.Balanced;

    /// <summary>The build rule, also used by <see cref="ResearchRushStrategy"/> once every node is done.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>True when something was ordered.</returns>
    public bool BuildBalanced(Turn turn)
    {
        if (TryBasic(turn, wantScience: !turn.AllResearchDone(), out bool waiting))
        {
            _lastWasPlacement = true;
            return true;
        }

        if (waiting)
        {
            return false;
        }

        long reserve = (turn.City.ActiveResearch is null ? CheapestResearchCoins(turn) : 0) + Construction.BasicReserve(turn);
        if (TryStationUpgrade(turn, reserve))
        {
            _lastWasPlacement = false;
            return true;
        }

        Building? upgrade = Construction.NextUpgrade(turn);
        bool fits = Construction.FreeTile(turn) is not null;
        if ((_lastWasPlacement || !fits) && upgrade is not null && Construction.TryUpgrade(turn, upgrade, reserve))
        {
            _lastWasPlacement = false;
            return true;
        }

        if (fits && Construction.NextExpansion(turn) is { } next && Construction.TryPlace(turn, next, reserve))
        {
            _lastWasPlacement = true;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override ResearchDefinition? ChooseResearch(Turn turn) => turn.AvailableResearch().FirstOrDefault();

    /// <inheritdoc />
    protected override bool Build(Turn turn) => BuildBalanced(turn);
}
