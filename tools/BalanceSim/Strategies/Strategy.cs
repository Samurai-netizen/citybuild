using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>The strategies a run can play (docs/modules/BalanceSim.md → Strategies).</summary>
public enum StrategyKind
{
    /// <summary>Grows the coin chain first; researches only what helps coins (<see cref="GreedyCoinsStrategy"/>).</summary>
    GreedyCoins,

    /// <summary>Keeps the research slot busy, cheapest node first (<see cref="ResearchRushStrategy"/>).</summary>
    ResearchRush,

    /// <summary>Researches every node and alternates new buildings with upgrades (<see cref="BalancedStrategy"/>).</summary>
    Balanced,
}

/// <summary>
/// A simple, documented player (core-instructions/24-economy-design.md → Balance simulator). On every decision step of a check-in, in this
/// order: research (<see cref="ChooseResearch"/>, above the basic reserve of <see cref="Construction.BasicReserve"/>), builds and upgrades (<see cref="Build"/>), worker assignment (<see cref="Workforce"/>,
/// shared) and logistics (<see cref="Logistics"/>, shared). It uses only the engine's rules through <see cref="CityActions"/>.
/// </summary>
public abstract class Strategy
{
    /// <summary>The most builds and upgrades one step may order: more than the builder slots of any content would allow anyway.</summary>
    private const int _maxBuildsPerStep = 16;

    /// <summary>Which strategy this is.</summary>
    public abstract StrategyKind Kind { get; }

    /// <summary>Makes the strategy of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>A new strategy.</returns>
    public static Strategy Create(StrategyKind kind) => kind switch
    {
        StrategyKind.GreedyCoins => new GreedyCoinsStrategy(),
        StrategyKind.ResearchRush => new ResearchRushStrategy(),
        StrategyKind.Balanced => new BalancedStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown strategy."),
    };

    /// <summary>Plays one decision step.</summary>
    /// <param name="turn">The turn.</param>
    public void Act(Turn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        if (turn.City.ActiveResearch is null && Simulation.State.CityQueries.ScienceBuilding(turn.City, turn.Content) is not null
            && ChooseResearch(turn) is { } research
            && turn.City.Purse - (UnlocksMissingChainType(turn, research) ? 0 : Construction.BasicReserve(turn)) >= turn.CoinsOfResearch(research))
        {
            turn.Actions.StartResearch(research.Id);
        }

        int builds = 0;
        while (builds < _maxBuildsPerStep && turn.HasFreeBuilder() && Build(turn))
        {
            builds++;
        }

        Workforce.Assign(turn);
        Logistics.Run(turn);
    }

    /// <summary>
    /// The research node the strategy wants next, or null for none; <see cref="Act"/> starts it when the slot is free and the purse covers
    /// its coins (otherwise the strategy waits for it).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The node, or null.</returns>
    protected abstract ResearchDefinition? ChooseResearch(Turn turn);

    /// <summary>Orders at most one build or upgrade.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>True when something was ordered (the step may order another), false when the strategy is done for this step.</returns>
    protected abstract bool Build(Turn turn);

    /// <summary>Whether a research node unlocks a chain type the city has none of (it may spend the basic reserve: it is what the reserve waits for).</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="research">The node.</param>
    /// <returns>True when it does.</returns>
    private static bool UnlocksMissingChainType(Turn turn, ResearchDefinition research) =>
        research.Effects.OfType<UnlockBuildingEffect>().Any(unlock => turn.Chain.IsChainType(unlock.Building)
            && Simulation.State.CityQueries.CountOfType(turn.City, unlock.Building) == 0);

    /// <summary>The coins of the cheapest research the city may start now, or 0 when there is none.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The coins.</returns>
    protected static long CheapestResearchCoins(Turn turn) =>
        turn.AvailableResearch().Select(turn.CoinsOfResearch).FirstOrDefault();

    /// <summary>Upgrades a courier station when the routes are full (<see cref="Construction.NextStationUpgrade"/>), above a reserve.</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="reserve">Coins the strategy keeps back.</param>
    /// <returns>True when the upgrade was ordered.</returns>
    protected static bool TryStationUpgrade(Turn turn, long reserve) =>
        Construction.NextStationUpgrade(turn) is { } station && Construction.TryUpgrade(turn, station, reserve);

    /// <summary>
    /// Places the next building of the basic city (<see cref="Construction.NextBasic"/>) with no reserve: the basic city is what earns the
    /// coins, so no strategy saves against it.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="wantScience">Whether the strategy wants research.</param>
    /// <param name="waiting">Set when a basic building is still missing but can't be placed yet (no coins): the strategy then builds nothing else.</param>
    /// <returns>True when one was placed.</returns>
    protected static bool TryBasic(Turn turn, bool wantScience, out bool waiting)
    {
        waiting = false;
        if (Construction.NextBasic(turn, wantScience) is not { } basic)
        {
            return false;
        }

        if (Construction.TryPlace(turn, basic, 0))
        {
            return true;
        }

        // A full grid can't take it, so the strategy goes on with upgrades; otherwise it waits for the coins.
        waiting = Construction.FreeTile(turn) is not null;
        return false;
    }
}
