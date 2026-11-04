using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// Golden scenario #1, the first session of <c>core-instructions/23-mvp-content.md</c> (CR-0008), over <see cref="GoldenContent.V2"/>. A new
/// player's city (coins only) runs the script of the table: at t=0 a Cottage (1) on (0, 2) and a Woodcutter's Hut (2) on (0, 0); at t=30 the
/// Scriptorium (3) on (4, 0); at t=60 StartResearch <c>trade_charter</c>, a Sawmill (4) on (2, 0) and a Cottage (5) on (1, 2); at t=360 and
/// t=490 a trip of all the Woodcutter's logs to the Sawmill; at t=660 a Trading Post (6) on (2, 2), a trip of all the Sawmill's planks to it
/// and a trip of half the Woodcutter's logs to the Sawmill; nothing else. Each action runs as the server runs a command: the city is settled
/// to the action's time, the mutation must succeed, and a zero-length advance follows. The integration tests play the same script over HTTP
/// (<c>CityBuilder.IntegrationTests/Golden/GoldenScenario1Http</c>): a change to the script here changes it there too.
/// </summary>
internal static class GoldenScenario1
{
    /// <summary>A largest step that is never reached: one advance per gap between actions (and up to the time asked for).</summary>
    public const long OneAdvancePerGap = long.MaxValue;

    /// <summary>The script of the first session: each action's time and what the player does then, as in the table of <c>23</c>.</summary>
    private static readonly (long Time, Action<CityState> Run)[] _script =
    [
        (0, city =>
        {
            Place(city, "house", 0, 2);
            Place(city, "woodcutter", 0, 0);
        }),
        (30, city => Place(city, "scriptorium", 4, 0)),
        (60, city =>
        {
            Command(city, target => CityRules.StartResearch(target, Content, new ResearchId("trade_charter")));
            Place(city, "sawmill", 2, 0);
            Place(city, "house", 1, 2);
        }),
        (360, city => Trip(city, 2, 4, "logs", "all")),
        (490, city => Trip(city, 2, 4, "logs", "all")),
        (660, city =>
        {
            Place(city, "trading_post", 2, 2);
            Trip(city, 4, 6, "planks", "all");
            Trip(city, 2, 4, "logs", "half");
        }),
    ];

    /// <summary>The frozen content the scenario was checked against by hand (<see cref="GoldenContent.V2"/>).</summary>
    public static GameContent Content => GoldenContent.V2;

    /// <summary>The city at <paramref name="time"/>, after the actions due then.</summary>
    /// <param name="time">The city clock to stop at, in seconds.</param>
    /// <param name="largestStepSeconds">The longest single advance; <see cref="OneAdvancePerGap"/> or 1 for one second at a time.</param>
    public static CityState After(long time, long largestStepSeconds) => Run(time, largestStepSeconds, includeActionsAtTime: true);

    /// <summary>The city at <paramref name="time"/>, before the actions due then.</summary>
    /// <param name="time">The city clock to stop at, in seconds.</param>
    /// <param name="largestStepSeconds">The longest single advance; <see cref="OneAdvancePerGap"/> or 1 for one second at a time.</param>
    public static CityState Before(long time, long largestStepSeconds) => Run(time, largestStepSeconds, includeActionsAtTime: false);

    /// <summary>Plays the script on a new starter city up to <paramref name="time"/>, with or without the actions due exactly then.</summary>
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

    /// <summary>Advances the city to <paramref name="time"/> in steps of at most <paramref name="largestStepSeconds"/>.</summary>
    private static void AdvanceTo(CityState city, long time, long largestStepSeconds)
    {
        while (city.Clock < time)
        {
            Simulator.Advance(city, Content, Math.Min(time - city.Clock, largestStepSeconds));
        }
    }

    /// <summary>Places a building of the type on tile (x, y), as a command.</summary>
    private static void Place(CityState city, string type, int x, int y) =>
        Command(city, target => CityRules.PlaceBuilding(target, Content, new BuildingTypeId(type), x, y));

    /// <summary>Starts a manual trip of the item from one building to another with a preset amount, as a command.</summary>
    private static void Trip(CityState city, int sourceId, int targetId, string item, string preset) =>
        Command(city, target => TripRules.StartTrip(target, Content, sourceId, targetId, new ItemId(item), Amount.Preset(new AmountPresetId(preset))));

    /// <summary>Applies a mutation as the server runs a command: it must succeed, and a zero-length advance follows.</summary>
    private static void Command(CityState city, Func<CityState, MutationResult> mutation)
    {
        RulesAssert.Ok(mutation(city));
        Simulator.Advance(city, Content, 0);
    }
}
