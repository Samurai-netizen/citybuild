using System.Text.RegularExpressions;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.IntegrationTests.EndToEnd;

/// <summary>
/// The end-to-end suite's script (M0-73): one player's Era I loop on the frozen content <c>Fixtures/content-e2e-v1.json</c>, as data, so
/// the same acts run over HTTP (<see cref="HttpEndToEndTests"/>) and on the engine alone (<see cref="Replay"/>), and the stored city is
/// compared with the engine's at every step. The story: a new player builds two cottages and the scriptorium, finances Trade Charter, builds
/// a woodcutter, a sawmill and a courier station with a route, then the trading post; the refused requests (a busy builder, a locked
/// building, a taken tile, an assignment above the free workers) change nothing; a worker rebalance and a third cottage; Sharpened Axes and
/// Frame Saw; an upgrade of the trading post that interrupts a cycle and refunds its inputs; Tally Sticks started, then 30 hours away.
/// Building ids follow the order of placement: 1 cottage, 2 scriptorium, 3 woodcutter, 4 cottage, 5 sawmill, 6 courier station,
/// 7 trading post, 8 cottage.
/// </summary>
internal static partial class EraOneLoop
{
    /// <summary>The presence grace of the tests' server in seconds (<c>Simulation:PresenceGraceSeconds</c> at DevTimeScale 1, <c>04</c> §2).</summary>
    public const long GraceSeconds = 300;

    /// <summary>When the player leaves for <see cref="AbsenceSeconds"/>: the last step before the long absence.</summary>
    public const long LeaveTime = 25_200;

    /// <summary>The long absence: 30 hours, longer than an access token lives (12 hours), so the player logs in again.</summary>
    public const long AbsenceSeconds = 30 * 3600;

    /// <summary>The city clock of the step after the absence.</summary>
    public const long ReturnTime = LeaveTime + AbsenceSeconds;

    /// <summary>The trading post: the building whose upgrade interrupts a cycle.</summary>
    public const int TradingPost = 7;

    /// <summary>The frozen content file, in this project's output (never edited, M0-B decision 3).</summary>
    public static string ContentPath { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-e2e-v1.json");

    /// <summary>The frozen content, loaded as the server loads it.</summary>
    public static GameContent Content { get; } = ContentFileLoader.Load(ContentPath);

    /// <summary>The script: city-clock second → the acts sent at it, in order. Every act is one request.</summary>
    public static IReadOnlyList<(long Time, LoopAct[] Acts)> Steps { get; } =
    [
        (0,
        [
            new PlaceAct("house", 0, 3),
            new PlaceAct("scriptorium", 5, 5),
            new RefusedAct(new PlaceAct("woodcutter", 0, 0), "NO_FREE_BUILDER"),
            new RefusedAct(new PlaceAct("trading_post", 4, 0), "BUILDING_LOCKED"),
        ]),
        (30,
        [
            new ResearchAct("trade_charter"),
            new PlaceAct("woodcutter", 0, 0),
            new PlaceAct("house", 1, 3),
            new RefusedAct(new PlaceAct("house", 0, 3), "TILE_OCCUPIED"),
        ]),
        (90, [new PlaceAct("sawmill", 2, 0), new PlaceAct("courier_station", 2, 2)]),
        (150, [new RouteAct(6, 1, 3, 5, "logs")]),
        (700, [new PlaceAct("trading_post", 4, 0), new RouteAct(6, 2, 5, 7, "planks")]),
        (1200,
        [
            new LookAct(),
            new RefusedAct(new AssignAct(TradingPost, 2), "INSUFFICIENT_WORKERS"),
            new AssignAct(3, 1),
            new AssignAct(TradingPost, 2),
            new PlaceAct("house", 2, 3),
        ]),
        (1260, [new AssignAct(3, 2)]),
        (3600, [new ResearchAct("sharp_axes"), new ReenergizeAct()]),
        (7200, [new ReenergizeAct()]),
        (10200, [new ResearchAct("frame_saw"), new RouteAct(6, 2, 5, 2, "planks"), new ReenergizeAct()]),
        (12600, [new RouteAct(6, 2, 5, TradingPost, "planks"), new ReenergizeAct()]),
        (16200, [new PauseAct(TradingPost, true), new ReenergizeAct()]),
        (18660, [new PauseAct(TradingPost, false), new AssignAct(TradingPost, 1), new UpgradeAct(TradingPost), new ReenergizeAct()]),
        (21600, [new AssignAct(TradingPost, 2), new ReenergizeAct()]),
        (LeaveTime, [new ResearchAct("tally_sticks"), new ReenergizeAct()]),
        (ReturnTime, [new LookAct()]),
    ];

    /// <summary>The script on the engine alone up to <paramref name="time"/>, as the server runs it: before each step the city settles to the
    /// step's time with the player away from <see cref="GraceSeconds"/> after the step before; each accepted act is applied with the
    /// zero-length advance after it; a refused act must be refused with its code and changes nothing.</summary>
    /// <param name="time">The city clock to stop at; one of the steps' times.</param>
    /// <returns>The city.</returns>
    public static CityState Replay(long time)
    {
        CityState city = CityFactory.CreateStarterCity(Content);
        long? lastRequest = null;
        foreach ((long stepTime, LoopAct[] acts) in Steps.Where(step => step.Time <= time))
        {
            Simulator.Advance(city, Content, stepTime - city.Clock, lastRequest + GraceSeconds);
            foreach (LoopAct act in acts)
            {
                Apply(city, act, stepTime);
            }

            lastRequest = stepTime;
        }

        return city;
    }

    /// <summary>The command path and JSON body of an act over HTTP (<c>05</c>).</summary>
    /// <param name="act">A command act (not <see cref="LookAct"/>, not <see cref="RefusedAct"/>).</param>
    /// <param name="requestId">The request id the body carries.</param>
    /// <returns>The path under <c>/api/v1/commands/</c> and the body.</returns>
    public static (string Command, object Body) Request(LoopAct act, Guid requestId) => act switch
    {
        PlaceAct place => ("place-building", new { requestId, buildingTypeId = place.Type, x = place.X, y = place.Y }),
        UpgradeAct upgrade => ("upgrade-building", new { requestId, buildingId = upgrade.BuildingId }),
        AssignAct assign => ("assign-workers", new { requestId, buildingId = assign.BuildingId, workers = assign.Workers }),
        ResearchAct research => ("start-research", new { requestId, researchId = research.ResearchId }),
        TripAct trip => ("start-trip", new { requestId, sourceId = trip.SourceId, targetId = trip.TargetId, itemId = trip.ItemId, amount = trip.Amount }),
        RouteAct route => ("set-route", new
        {
            requestId,
            stationId = route.StationId,
            slot = route.Slot,
            stops = new[]
            {
                new { buildingId = route.FromId, actions = new[] { new { kind = "take", itemId = route.ItemId, amount = "all" } } },
                new { buildingId = route.ToId, actions = new[] { new { kind = "put", itemId = route.ItemId, amount = "all" } } },
            },
        }),
        PauseAct pause => ("set-paused", new { requestId, buildingId = pause.BuildingId, paused = pause.Paused }),
        ReenergizeAct => ("reenergize-couriers", new { requestId }),
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, "Not a command."),
    };

    /// <summary>The API's error code for an engine refusal: <c>TileOccupied</c> → <c>TILE_OCCUPIED</c> (<c>05</c>).</summary>
    /// <param name="code">The engine's code.</param>
    /// <returns>The API's code.</returns>
    public static string ApiCode(MutationErrorCode code) => CapitalRegex().Replace(code.ToString(), "_$1").ToUpperInvariant();

    /// <summary>Applies one act to the engine's city as the server would, and fails with the city's description when the engine disagrees
    /// with the script.</summary>
    /// <param name="city">The city.</param>
    /// <param name="act">The act.</param>
    /// <param name="time">The step's time, for the failure message.</param>
    private static void Apply(CityState city, LoopAct act, long time)
    {
        if (act is LookAct)
        {
            return;
        }

        MutationResult result = Mutate(city, act is RefusedAct refused ? refused.Act : act);
        string where = $"t={time} {act}: {result}\n{Describe(city)}";
        if (act is RefusedAct expected)
        {
            Assert.True(result.ErrorCode is { } code && ApiCode(code) == expected.Code, where);
            return;
        }

        Assert.True(result.IsOk, where);
        Simulator.Advance(city, Content, 0);
    }

    /// <summary>The engine's mutation for a command act on the loop's content.</summary>
    /// <param name="city">The city.</param>
    /// <param name="act">The act.</param>
    /// <returns>The engine's answer; on success the city has changed.</returns>
    private static MutationResult Mutate(CityState city, LoopAct act) => Mutate(city, Content, act);

    /// <summary>The engine's mutation for a command act on <paramref name="content"/> (also the Era I charter player's, M1-02).</summary>
    /// <param name="city">The city.</param>
    /// <param name="content">The content the city runs on.</param>
    /// <param name="act">The act.</param>
    /// <returns>The engine's answer; on success the city has changed.</returns>
    public static MutationResult Mutate(CityState city, GameContent content, LoopAct act) => act switch
    {
        PlaceAct place => CityRules.PlaceBuilding(city, content, new BuildingTypeId(place.Type), place.X, place.Y),
        UpgradeAct upgrade => CityRules.UpgradeBuilding(city, content, upgrade.BuildingId),
        AssignAct assign => CityRules.AssignWorkers(city, content, assign.BuildingId, assign.Workers),
        ResearchAct research => CityRules.StartResearch(city, content, new ResearchId(research.ResearchId)),
        TripAct trip => TripRules.StartTrip(city, content, trip.SourceId, trip.TargetId, new ItemId(trip.ItemId), Amount.Preset(new AmountPresetId(trip.Amount))),
        RouteAct route => CourierRules.SetRoute(city, content, route.StationId, route.Slot,
        [
            new RouteStop(route.FromId, [new RouteAction(RouteActionKind.Take, new ItemId(route.ItemId), Amount.Preset(new AmountPresetId("all")))]),
            new RouteStop(route.ToId, [new RouteAction(RouteActionKind.Put, new ItemId(route.ItemId), Amount.Preset(new AmountPresetId("all")))]),
        ]),
        PauseAct pause => CityRules.SetPaused(city, content, pause.BuildingId, pause.Paused),
        ReenergizeAct => CourierRules.ReenergizeCouriers(city, Content),
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, "Not a command."),
    };

    /// <summary>The engine city in a few lines, for failure messages: clock, purse, research, and each building's phase, workers and store.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The text.</returns>
    public static string Describe(CityState city) => string.Join("\n",
    [
        $"clock {city.Clock} purse {city.Purse} research {city.ActiveResearch?.Id.ToString() ?? "-"} done {string.Join(",", city.CompletedResearch)}",
        .. city.Buildings.Select(building => $"#{building.Id} {building.TypeId} L{building.Level} w{building.AssignedWorkers} {building.Phase} " +
            $"store {string.Join(" ", building.Store.Select(good => $"{good.Key}={good.Value}"))} needs {string.Join(" ", building.Needs.Select(good => $"{good.Key}={good.Value}"))}"),
        .. city.Couriers.Select(courier => $"courier {courier.StationId}/{courier.Slot} {courier.State} stamina {courier.Stamina}"),
    ]);

    /// <summary>A capital letter after the first character, for <see cref="ApiCode"/>.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("(?<=.)([A-Z])")]
    private static partial Regex CapitalRegex();
}

/// <summary>One request of the end-to-end script (<see cref="EraOneLoop"/>).</summary>
internal abstract record LoopAct;

/// <summary>Place a building of <paramref name="Type"/> on (<paramref name="X"/>, <paramref name="Y"/>).</summary>
/// <param name="Type">The building type id.</param>
/// <param name="X">The tile's column.</param>
/// <param name="Y">The tile's row.</param>
internal sealed record PlaceAct(string Type, int X, int Y) : LoopAct;

/// <summary>Upgrade a building by one level.</summary>
/// <param name="BuildingId">The building.</param>
internal sealed record UpgradeAct(int BuildingId) : LoopAct;

/// <summary>Set a building's assigned workers.</summary>
/// <param name="BuildingId">The producer or courier station.</param>
/// <param name="Workers">The new number.</param>
internal sealed record AssignAct(int BuildingId, int Workers) : LoopAct;

/// <summary>Start a research.</summary>
/// <param name="ResearchId">The research id.</param>
internal sealed record ResearchAct(string ResearchId) : LoopAct;

/// <summary>Start a trip of free workers carrying a good from one building to another.</summary>
/// <param name="SourceId">The building the goods come from.</param>
/// <param name="TargetId">The building they go to.</param>
/// <param name="ItemId">The good.</param>
/// <param name="Amount">An amount preset id (<c>all</c>, <c>half</c>, <c>quarter</c>).</param>
internal sealed record TripAct(int SourceId, int TargetId, string ItemId, string Amount) : LoopAct;

/// <summary>Set a station's slot to a two-stop route: take all of a good at one building, put all of it at another.</summary>
/// <param name="StationId">The courier station.</param>
/// <param name="Slot">The slot (the courier).</param>
/// <param name="FromId">Where the courier takes the good.</param>
/// <param name="ToId">Where it puts it.</param>
/// <param name="ItemId">The good.</param>
internal sealed record RouteAct(int StationId, int Slot, int FromId, int ToId, string ItemId) : LoopAct;

/// <summary>Pause or resume a producer or courier station.</summary>
/// <param name="BuildingId">The building.</param>
/// <param name="Paused">Whether it is paused afterwards.</param>
internal sealed record PauseAct(int BuildingId, bool Paused) : LoopAct;

/// <summary>Refill every courier's stamina.</summary>
internal sealed record ReenergizeAct : LoopAct;

/// <summary>Look at the city (<c>GET /api/v1/city</c>): settles it and records the player's presence.</summary>
internal sealed record LookAct : LoopAct;

/// <summary>A command the server must refuse with <paramref name="Code"/>, changing nothing.</summary>
/// <param name="Act">The command.</param>
/// <param name="Code">The expected error code (<c>05</c>).</param>
internal sealed record RefusedAct(LoopAct Act, string Code) : LoopAct;
