using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// Golden scenario #1, the first session of <c>core-instructions/23-mvp-content.md</c>, over <see cref="GoldenContent.V1"/>. A new player's
/// city runs the script: at t=0 StartResearch <c>trade_charter</c> and two Cottages (ids 1 and 2), at t=30 a Woodcutter's Hut (3) and a
/// Sawmill (4), at t=750 a Trading Post (5); nothing else. Each action runs as the server runs a command: the city is settled to the action's
/// time, the mutation must succeed, and a zero-length advance follows. The table doesn't name tiles; the buildings stand in a row from (0, 0).
/// </summary>
internal static class GoldenScenario1
{
    /// <summary>A largest step that is never reached: one advance per gap between actions (and up to the time asked for).</summary>
    public const long OneAdvancePerGap = long.MaxValue;

    private static readonly (long Time, Action<CityState> Run)[] _script =
    [
        (0, city =>
        {
            Command(city, target => CityRules.StartResearch(target, Content, new ResearchId("trade_charter")));
            Place(city, "house", 0);
            Place(city, "house", 1);
        }),
        (30, city =>
        {
            Place(city, "woodcutter", 2);
            Place(city, "sawmill", 3);
        }),
        (750, city => Place(city, "trading_post", 4)),
    ];

    private static GameContent Content => GoldenContent.V1;

    /// <summary>The city at <paramref name="time"/>, after the actions due then.</summary>
    /// <param name="time">The city clock to stop at, in seconds.</param>
    /// <param name="largestStepSeconds">The longest single advance; <see cref="OneAdvancePerGap"/> or 1 for one second at a time.</param>
    public static CityState After(long time, long largestStepSeconds) => Run(time, largestStepSeconds, includeActionsAtTime: true);

    /// <summary>The city at <paramref name="time"/>, before the actions due then.</summary>
    /// <param name="time">The city clock to stop at, in seconds.</param>
    /// <param name="largestStepSeconds">The longest single advance; <see cref="OneAdvancePerGap"/> or 1 for one second at a time.</param>
    public static CityState Before(long time, long largestStepSeconds) => Run(time, largestStepSeconds, includeActionsAtTime: false);

    private static CityState Run(long time, long largestStepSeconds, bool includeActionsAtTime)
    {
        CityState city = CityFactory.CreateStarterCity(Content);
        foreach ((long actionTime, Action<CityState> action) in _script)
        {
            if (actionTime > time || (actionTime == time && !includeActionsAtTime))
            {
                break;
            }

            AdvanceTo(city, actionTime, largestStepSeconds);
            action(city);
        }

        AdvanceTo(city, time, largestStepSeconds);
        return city;
    }

    private static void AdvanceTo(CityState city, long time, long largestStepSeconds)
    {
        while (city.Clock < time)
        {
            Simulator.Advance(city, Content, Math.Min(time - city.Clock, largestStepSeconds));
        }
    }

    private static void Place(CityState city, string type, int x) =>
        Command(city, target => CityRules.PlaceBuilding(target, Content, new BuildingTypeId(type), x, 0));

    private static void Command(CityState city, Func<CityState, MutationResult> mutation)
    {
        RulesAssert.Ok(mutation(city));
        Simulator.Advance(city, Content, 0);
    }
}
