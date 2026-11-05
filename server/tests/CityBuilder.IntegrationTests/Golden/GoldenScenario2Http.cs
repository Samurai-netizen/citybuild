using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.IntegrationTests.Golden;

/// <summary>
/// Golden scenario #2, couriers, a road and an absence (<c>core-instructions/23-mvp-content.md</c>, CR-0008), for the tests over HTTP: the
/// script as HTTP requests, and the same script on the engine alone with the server's absences, to compare the stored city with. The script
/// is the engine test's (<c>CityBuilder.Simulation.Tests/Golden/GoldenScenario2</c>): at t=0 a Cottage (1) on (0, 3) and a Woodcutter's Hut
/// (2) on (0, 0); at t=30 a Courier Station (3) on (0, 1); at t=60 a Warehouse (4) on (3, 0) and dirt paths on (1, 0) and (2, 0); at t=90
/// courier 1's route (the Woodcutter <c>take logs all</c>, the Warehouse <c>put logs all</c>); the player looks (<c>GET /city</c>) at t=745,
/// t=25000 and t=25005, and re-energizes the couriers at t=25000. Every request is presence, so the script sends no other request: the
/// player is away from 300 s after each one (the server's grace). A change to the script there changes both copies here.
/// </summary>
internal static class GoldenScenario2Http
{
    /// <summary>The city-clock seconds of the script's requests, in order.</summary>
    public static readonly long[] RequestTimes = [0, 30, 60, 90, 745, 25000, 25005];

    /// <summary>The server's presence grace in seconds, at the test server's real-time clock (<c>04</c> section 2).</summary>
    public const long GraceSeconds = 300;

    /// <summary>Sends the script's requests due at <paramref name="time"/> for the player, over HTTP; a look is <c>GET /city</c>.</summary>
    /// <param name="player">The player.</param>
    /// <param name="time">One of <see cref="RequestTimes"/>.</param>
    /// <returns>The snapshot of the last request.</returns>
    public static async Task<System.Text.Json.Nodes.JsonNode> ActAsync(GoldenHttpPlayer player, long time)
    {
        switch (time)
        {
            case 0:
                await player.PlaceAsync("house", 0, 3);
                return await player.PlaceAsync("woodcutter", 0, 0);
            case 30:
                return await player.PlaceAsync("courier_station", 0, 1);
            case 60:
                await player.PlaceAsync("warehouse", 3, 0);
                return await player.PlaceRoadsAsync((1, 0), (2, 0));
            case 90:
                return await player.SetRouteAsync(3, 1, (2, "take", "logs", "all"), (4, "put", "logs", "all"));
            case 745:
            case 25000:
            case 25005:
                return await player.GetCityAsync();
            default:
                throw new ArgumentOutOfRangeException(nameof(time), time, "The script sends nothing at this time.");
        }
    }

    /// <summary>
    /// The engine's city at <paramref name="time"/>, after the script's requests due then: a starter city advanced from request to request with
    /// the player away from 300 s after the one before (as the server's settle does), each command applied as the server applies it (the
    /// mutation must succeed, a zero-length advance follows). The re-energizing at 25000 runs when <paramref name="withReenergizing"/>.
    /// </summary>
    /// <param name="content">The frozen content.</param>
    /// <param name="time">The city clock to stop at; one of <see cref="RequestTimes"/>.</param>
    /// <param name="withReenergizing">Whether the re-energizing at 25000 has been sent by <paramref name="time"/>.</param>
    /// <returns>The city.</returns>
    public static CityState Replay(GameContent content, long time, bool withReenergizing)
    {
        CityState city = CityFactory.CreateStarterCity(content);
        long? lastRequest = null;
        foreach (long requestTime in RequestTimes.Where(requestTime => requestTime <= time))
        {
            Simulator.Advance(city, content, requestTime - city.Clock, lastRequest + GraceSeconds);
            switch (requestTime)
            {
                case 0:
                    Apply(city, content, Place(city, content, "house", 0, 3));
                    Apply(city, content, Place(city, content, "woodcutter", 0, 0));
                    break;
                case 30:
                    Apply(city, content, Place(city, content, "courier_station", 0, 1));
                    break;
                case 60:
                    Apply(city, content, Place(city, content, "warehouse", 3, 0));
                    Apply(city, content, RoadRules.PlaceRoads(city, content, [(1, 0), (2, 0)]));
                    break;
                case 90:
                    Apply(city, content, CourierRules.SetRoute(city, content, 3, 1,
                    [
                        new RouteStop(2, [new RouteAction(RouteActionKind.Take, new ItemId("logs"), All)]),
                        new RouteStop(4, [new RouteAction(RouteActionKind.Put, new ItemId("logs"), All)]),
                    ]));
                    break;
                case 25000 when withReenergizing:
                    Apply(city, content, CourierRules.ReenergizeCouriers(city, content));
                    break;
                default:
                    break;
            }

            lastRequest = requestTime;
        }

        return city;
    }

    /// <summary>The preset <c>all</c>.</summary>
    private static Amount All => Amount.Preset(new AmountPresetId("all"));

    /// <summary>Places a building of the type on tile (x, y).</summary>
    /// <param name="city">The city.</param>
    /// <param name="content">The frozen content.</param>
    /// <param name="type">The building type id.</param>
    /// <param name="x">The tile's column.</param>
    /// <param name="y">The tile's row.</param>
    /// <returns>The mutation's result.</returns>
    private static MutationResult Place(CityState city, GameContent content, string type, int x, int y) =>
        CityRules.PlaceBuilding(city, content, new BuildingTypeId(type), x, y);

    /// <summary>Checks that a mutation succeeded and follows it with the zero-length advance of a command.</summary>
    /// <param name="city">The city the mutation changed.</param>
    /// <param name="content">The frozen content.</param>
    /// <param name="result">The mutation's result.</param>
    private static void Apply(CityState city, GameContent content, MutationResult result)
    {
        Assert.True(result.IsOk, result.ToString());
        Simulator.Advance(city, content, 0);
    }
}
