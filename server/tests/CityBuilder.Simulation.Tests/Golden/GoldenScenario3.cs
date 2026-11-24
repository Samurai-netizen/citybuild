using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// Golden scenario #3, the bread chain with couriers (<c>core-instructions/28-era1-content.md</c>, M1-02), over <see cref="GoldenContent.V3"/>.
/// It starts from a built city at t=0, since reaching bread from a new city takes hours of research: the research of the chain done
/// (<c>trade_charter</c>, <c>stone_cutting</c>, <c>bridge_building</c>, <c>open_fields</c>, <c>milling</c>), a purse of 1000, and idle level-1
/// buildings without workers: Cottages 1–3 on (0, 7), (1, 7), (2, 7), a Farm (4) on (0, 0), a Windmill (5) on (2, 0), a Bakery (6) on (4, 0),
/// a Courier Station (7) on (2, 1) and the Scriptorium (8) on (7, 7). The script of the table: at t=0 the player gives the Farm 2 workers,
/// the Windmill 3, the Bakery 2 and the station 2 couriers, then sets courier 1's route (the Farm <c>take grain all</c>, the Windmill
/// <c>put grain all</c>) and courier 2's (the Windmill <c>take flour all</c>, the Bakery <c>put flour all</c>); every 300 s up to 1500 the
/// player looks. Requests run as in <see cref="GoldenScenario2"/>: settle with the player absent from 300 s after the request before, then
/// the request's mutations, each followed by a zero-length advance. With a look every 300 s the player is never absent, so no stamina is spent.
/// </summary>
internal static class GoldenScenario3
{
    /// <summary>A largest step that is never reached: one advance per gap between requests (and up to the time asked for).</summary>
    public const long OneAdvancePerGap = long.MaxValue;

    /// <summary>The presence grace of the scenario, in seconds: the player counts as away from 300 s after a request (<c>04</c> section 2).</summary>
    public const long GraceSeconds = 300;

    /// <summary>The Farm's id.</summary>
    public const int Farm = 4;

    /// <summary>The Windmill's id.</summary>
    public const int Windmill = 5;

    /// <summary>The Bakery's id.</summary>
    public const int Bakery = 6;

    /// <summary>The Courier Station's id.</summary>
    public const int Station = 7;

    /// <summary>The requests of the scenario: each one's time and its mutations, as in the table of <c>28</c>; a look has none.</summary>
    private static readonly (long Time, Action<CityState> Run)[] _requests =
    [
        (0, city =>
        {
            Command(city, target => CityRules.AssignWorkers(target, Content, Farm, 2));
            Command(city, target => CityRules.AssignWorkers(target, Content, Windmill, 3));
            Command(city, target => CityRules.AssignWorkers(target, Content, Bakery, 2));
            Command(city, target => CityRules.AssignWorkers(target, Content, Station, 2));
            Command(city, target => CourierRules.SetRoute(target, Content, Station, 1, Stops(
                (Farm, [(true, "grain", "all")]),
                (Windmill, [(false, "grain", "all")]))));
            Command(city, target => CourierRules.SetRoute(target, Content, Station, 2, Stops(
                (Windmill, [(true, "flour", "all")]),
                (Bakery, [(false, "flour", "all")]))));
        }),
        (300, _ => { }),
        (600, _ => { }),
        (900, _ => { }),
        (1200, _ => { }),
        (1500, _ => { }),
    ];

    /// <summary>The frozen content the scenario was checked against by hand (<see cref="GoldenContent.V3"/>).</summary>
    public static GameContent Content => GoldenContent.V3;

    /// <summary>The city at <paramref name="time"/>, after the requests due then, and the settle reports of the requests on the way.</summary>
    /// <param name="time">The city clock to stop at, in seconds.</param>
    /// <param name="largestStepSeconds">The longest single advance; <see cref="OneAdvancePerGap"/> or 1 for one second at a time.</param>
    /// <returns>The city and, per request up to <paramref name="time"/>, its time and the report of its settle (all its steps summed by the caller).</returns>
    public static (CityState City, List<(long Time, List<SettlementReport> Settle)> Requests) Run(long time, long largestStepSeconds)
    {
        CityState city = Start();
        var requests = new List<(long Time, List<SettlementReport> Settle)>();
        long? lastRequest = null;
        foreach ((long requestTime, Action<CityState> mutations) in _requests)
        {
            if (requestTime > time)
            {
                break;
            }

            requests.Add((requestTime, AdvanceTo(city, requestTime, largestStepSeconds, AbsentFrom(lastRequest))));
            mutations(city);
            lastRequest = requestTime;
        }

        AdvanceTo(city, time, largestStepSeconds, AbsentFrom(lastRequest));
        return (city, requests);
    }

    /// <summary>The city at <paramref name="time"/>, after the requests due then.</summary>
    /// <param name="time">The city clock to stop at, in seconds.</param>
    /// <param name="largestStepSeconds">The longest single advance; <see cref="OneAdvancePerGap"/> or 1 for one second at a time.</param>
    public static CityState After(long time, long largestStepSeconds) => Run(time, largestStepSeconds).City;

    /// <summary>The built city the scenario starts from at t=0 (the class comment lists it).</summary>
    private static CityState Start() => City(
        clock: 0,
        purse: 1000,
        completedResearch: ["bridge_building", "milling", "open_fields", "stone_cutting", "trade_charter"],
        buildings:
        [
            Idle(1, "house", 0, 7),
            Idle(2, "house", 1, 7),
            Idle(3, "house", 2, 7),
            Idle(Farm, "farm", 0, 0),
            Idle(Windmill, "windmill", 2, 0),
            Idle(Bakery, "bakery", 4, 0),
            Idle(Station, "courier_station", 2, 1),
            Idle(8, "scriptorium", 7, 7),
        ]);

    /// <summary>When the player counts as away in the settle after a request at <paramref name="lastRequest"/>; null before the first.</summary>
    private static long? AbsentFrom(long? lastRequest) => lastRequest + GraceSeconds;

    /// <summary>Advances the city to <paramref name="time"/> in steps of at most <paramref name="largestStepSeconds"/>, all with one absence start.</summary>
    /// <returns>The reports of the steps.</returns>
    private static List<SettlementReport> AdvanceTo(CityState city, long time, long largestStepSeconds, long? absentFrom)
    {
        var reports = new List<SettlementReport>();
        while (city.Clock < time)
        {
            reports.Add(Simulator.Advance(city, Content, Math.Min(time - city.Clock, largestStepSeconds), absentFrom));
        }

        return reports;
    }

    /// <summary>Applies a mutation as the server runs a command: it must succeed, and a zero-length advance follows.</summary>
    private static void Command(CityState city, Func<CityState, MutationResult> mutation)
    {
        RulesAssert.Ok(mutation(city));
        Simulator.Advance(city, Content, 0);
    }
}
