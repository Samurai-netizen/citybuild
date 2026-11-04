using System.Text.Json.Nodes;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CityBuilder.IntegrationTests.Golden;

/// <summary>
/// Golden scenario #1 (<c>core-instructions/23-mvp-content.md</c>, CR-0008) for the tests over HTTP: the frozen content it was checked against,
/// the script as HTTP commands, and the same script on the engine alone, to compare the stored city with. The script is the engine test's
/// (<c>CityBuilder.Simulation.Tests/Golden/GoldenScenario1</c>): at t=0 a Cottage (1) on (0, 2) and a Woodcutter's Hut (2) on (0, 0); at t=30
/// the Scriptorium (3) on (4, 0); at t=60 StartResearch <c>trade_charter</c>, a Sawmill (4) on (2, 0) and a Cottage (5) on (1, 2); at t=360
/// and t=490 a trip of all the Woodcutter's logs to the Sawmill; at t=660 a Trading Post (6) on (2, 2), a trip of all the Sawmill's planks to
/// it and a trip of half the Woodcutter's logs to the Sawmill. A change to the script there changes both copies here.
/// </summary>
internal static class GoldenScenario1Http
{
    /// <summary>The city-clock seconds at which the script acts, in order.</summary>
    public static readonly long[] ActionTimes = [0, 30, 60, 360, 490, 660];

    /// <summary>
    /// A copy of the frozen content file <c>content-golden-v2.json</c> (linked into this project's output; protected, never edited, <c>15</c>)
    /// with the M0 offline production cap of 28 800 s added, written once to a temporary file. v2 was frozen without the cap, which the engine
    /// requires until CR-0008-T1 part H; the engine test adds it the same way (<c>GoldenContent.V2</c>, build log BL-20261102-07, decision
    /// 22). No row reaches 8 h, so the cap changes no expectation. Temporary: part H removes the cap and this copy.
    /// </summary>
    public static string ContentPath { get; } = WriteWithTheOfflineCap();

    /// <summary>The test's server with the frozen content in place of the real one; it shares the factory's clock and database.</summary>
    /// <param name="factory">The test's server on its database.</param>
    /// <returns>The server; the caller disposes it.</returns>
    public static WebApplicationFactory<Program> WithGoldenContent(PostgresApiFactory factory) =>
        factory.WithWebHostBuilder(host => host.UseSetting("Content:FilePath", ContentPath));

    /// <summary>Sends the script's commands due at <paramref name="time"/> for the player, over HTTP.</summary>
    /// <param name="player">The player.</param>
    /// <param name="time">One of <see cref="ActionTimes"/>.</param>
    /// <returns>A task that completes when every command has succeeded.</returns>
    public static async Task ActAsync(GoldenHttpPlayer player, long time)
    {
        switch (time)
        {
            case 0:
                await player.PlaceAsync("house", 0, 2);
                await player.PlaceAsync("woodcutter", 0, 0);
                break;
            case 30:
                await player.PlaceAsync("scriptorium", 4, 0);
                break;
            case 60:
                await player.StartResearchAsync("trade_charter");
                await player.PlaceAsync("sawmill", 2, 0);
                await player.PlaceAsync("house", 1, 2);
                break;
            case 360:
            case 490:
                await player.StartTripAsync(2, 4, "logs", "all");
                break;
            case 660:
                await player.PlaceAsync("trading_post", 2, 2);
                await player.StartTripAsync(4, 6, "planks", "all");
                await player.StartTripAsync(2, 4, "logs", "half");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(time), time, "The script does nothing at this time.");
        }
    }

    /// <summary>
    /// The engine's city at <paramref name="time"/>, after the script's actions due then: a starter city advanced from action to action, each
    /// action applied as the server applies a command (the mutation must succeed, a zero-length advance follows).
    /// </summary>
    /// <param name="content">The frozen content.</param>
    /// <param name="time">The city clock to stop at.</param>
    /// <returns>The city.</returns>
    public static CityState Replay(GameContent content, long time)
    {
        CityState city = CityFactory.CreateStarterCity(content);
        foreach (long actionTime in ActionTimes.Where(actionTime => actionTime <= time))
        {
            Simulator.Advance(city, content, actionTime - city.Clock);
            switch (actionTime)
            {
                case 0:
                    Apply(city, content, Place(city, content, "house", 0, 2));
                    Apply(city, content, Place(city, content, "woodcutter", 0, 0));
                    break;
                case 30:
                    Apply(city, content, Place(city, content, "scriptorium", 4, 0));
                    break;
                case 60:
                    Apply(city, content, CityRules.StartResearch(city, content, new ResearchId("trade_charter")));
                    Apply(city, content, Place(city, content, "sawmill", 2, 0));
                    Apply(city, content, Place(city, content, "house", 1, 2));
                    break;
                case 360:
                case 490:
                    Apply(city, content, Trip(city, content, 2, 4, "logs", "all"));
                    break;
                default:
                    Apply(city, content, Place(city, content, "trading_post", 2, 2));
                    Apply(city, content, Trip(city, content, 4, 6, "planks", "all"));
                    Apply(city, content, Trip(city, content, 2, 4, "logs", "half"));
                    break;
            }
        }

        Simulator.Advance(city, content, time - city.Clock);
        return city;
    }

    /// <summary>Places a building of the type on tile (x, y).</summary>
    /// <param name="city">The city.</param>
    /// <param name="content">The frozen content.</param>
    /// <param name="type">The building type id.</param>
    /// <param name="x">The tile's column.</param>
    /// <param name="y">The tile's row.</param>
    /// <returns>The mutation's result.</returns>
    private static MutationResult Place(CityState city, GameContent content, string type, int x, int y) =>
        CityRules.PlaceBuilding(city, content, new BuildingTypeId(type), x, y);

    /// <summary>Starts a manual trip with a preset amount.</summary>
    /// <param name="city">The city.</param>
    /// <param name="content">The frozen content.</param>
    /// <param name="sourceId">The building the goods come from.</param>
    /// <param name="targetId">The building they go to.</param>
    /// <param name="item">The good.</param>
    /// <param name="preset">The amount preset's id.</param>
    /// <returns>The mutation's result.</returns>
    private static MutationResult Trip(CityState city, GameContent content, int sourceId, int targetId, string item, string preset) =>
        TripRules.StartTrip(city, content, sourceId, targetId, new ItemId(item), Amount.Preset(new AmountPresetId(preset)));

    /// <summary>Checks that a mutation succeeded and follows it with the zero-length advance of a command.</summary>
    /// <param name="city">The city the mutation changed.</param>
    /// <param name="content">The frozen content.</param>
    /// <param name="result">The mutation's result.</param>
    private static void Apply(CityState city, GameContent content, MutationResult result)
    {
        Assert.True(result.IsOk, result.ToString());
        Simulator.Advance(city, content, 0);
    }

    /// <summary>Writes the copy of <see cref="ContentPath"/>: v2 with <c>rules.maxOfflineProductionSeconds</c> set to 28 800.</summary>
    /// <returns>The copy's path, in the temporary folder.</returns>
    private static string WriteWithTheOfflineCap()
    {
        string frozen = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-golden-v2.json"));
        JsonNode content = JsonNode.Parse(frozen) ?? throw new InvalidOperationException("content-golden-v2.json is JSON null.");
        content["rules"]!["maxOfflineProductionSeconds"] = 28_800;
        string path = Path.Combine(Path.GetTempPath(), "citybuilder-golden-v2-with-cap-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, content.ToJsonString());
        return path;
    }
}
