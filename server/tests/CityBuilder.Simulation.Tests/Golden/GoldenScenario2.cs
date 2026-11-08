using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Rules;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// Golden scenario #2, couriers, a road and an absence (<c>core-instructions/23-mvp-content.md</c>, CR-0008), over <see cref="GoldenContent.V2"/>.
/// The script of the table: at t=0 a Cottage (1) on (0, 3) and a Woodcutter's Hut (2) on (0, 0); at t=30 a Courier Station (3) on (0, 1); at
/// t=60 a Warehouse (4) on (3, 0) and dirt paths on (1, 0) and (2, 0); at t=90 courier 1's route: the Woodcutter <c>take logs all</c>, the
/// Warehouse <c>put logs all</c>; at t=745 and t=25000 the player looks; at t=25000 the couriers are re-energized; at t=25005 the player
/// looks. Each of those is a request, run as the server runs it: the city is settled to the request's time with the player absent from 300 s
/// after the request before (<c>04</c> section 2), then the request's mutations, each followed by a zero-length advance. A time between
/// requests is the city a settle at that second would show (the same absence).
/// </summary>
internal static class GoldenScenario2
{
    /// <summary>A largest step that is never reached: one advance per gap between requests (and up to the time asked for).</summary>
    public const long OneAdvancePerGap = long.MaxValue;

    /// <summary>The presence grace of the scenario, in seconds: the player counts as away from 300 s after a request (<c>04</c> section 2).</summary>
    public const long GraceSeconds = 300;

    /// <summary>The requests of the scenario: each one's time and its mutations, as in the table of <c>23</c>; a look has none.</summary>
    private static readonly (long Time, Action<CityState> Run)[] _requests =
    [
        (0, city =>
        {
            Place(city, "house", 0, 3);
            Place(city, "woodcutter", 0, 0);
        }),
        (30, city => Place(city, "courier_station", 0, 1)),
        (60, city =>
        {
            Place(city, "warehouse", 3, 0);
            Command(city, target => RoadRules.PlaceRoads(target, Content, [(1, 0), (2, 0)]));
        }),
        (90, city => Command(city, target => CourierRules.SetRoute(target, Content, 3, 1,
        [
            new RouteStop(2, [new RouteAction(RouteActionKind.Take, new ItemId("logs"), All)]),
            new RouteStop(4, [new RouteAction(RouteActionKind.Put, new ItemId("logs"), All)]),
        ]))),
        (745, _ => { }),
        (25000, city => Command(city, target => CourierRules.ReenergizeCouriers(target, Content))),
        (25005, _ => { }),
    ];

    /// <summary>The frozen content the scenario was checked against by hand (<see cref="GoldenContent.V2"/>).</summary>
    public static GameContent Content => GoldenContent.V2;

    /// <summary>The preset <c>all</c>.</summary>
    private static Amount All => Amount.Preset(new AmountPresetId("all"));

    /// <summary>The city at <paramref name="time"/>, after the requests due then, and the settle reports of the requests on the way.</summary>
    /// <param name="time">The city clock to stop at, in seconds.</param>
    /// <param name="largestStepSeconds">The longest single advance; <see cref="OneAdvancePerGap"/> or 1 for one second at a time.</param>
    /// <returns>The city and, per request up to <paramref name="time"/>, its time and the report of its settle (all its steps summed by the caller).</returns>
    public static (CityState City, List<(long Time, List<SettlementReport> Settle)> Requests) Run(long time, long largestStepSeconds)
    {
        CityState city = CityFactory.CreateStarterCity(Content);
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

    /// <summary>Places a building of the type on tile (x, y), as a command.</summary>
    private static void Place(CityState city, string type, int x, int y) =>
        Command(city, target => CityRules.PlaceBuilding(target, Content, new BuildingTypeId(type), x, y));

    /// <summary>Applies a mutation as the server runs a command: it must succeed, and a zero-length advance follows.</summary>
    private static void Command(CityState city, Func<CityState, MutationResult> mutation)
    {
        RulesAssert.Ok(mutation(city));
        Simulator.Advance(city, Content, 0);
    }
}
