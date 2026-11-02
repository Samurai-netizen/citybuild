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
/// Golden scenario #1 (<c>core-instructions/23-mvp-content.md</c>) for the tests over HTTP: the frozen content it was checked against, the
/// script as HTTP commands, and the same script on the engine alone, to compare the stored city with. The script is the engine test's
/// (<c>CityBuilder.Simulation.Tests/Golden/GoldenScenario1</c>): at t=0 StartResearch <c>trade_charter</c> and two Cottages (ids 1 and 2), at
/// t=30 a Woodcutter's Hut (3) and a Sawmill (4), at t=750 a Trading Post (5), standing in a row from tile (0, 0). A change to the script
/// there changes both copies here.
/// </summary>
internal static class GoldenScenario1Http
{
    /// <summary>The city-clock seconds at which the script acts, in order.</summary>
    public static readonly long[] ActionTimes = [0, 30, 750];

    /// <summary>
    /// The engine test's frozen content file, <c>content-golden-v1.json</c>, which the project file links into this project's output
    /// (it is a protected file: never edited; <c>15</c>), with the walking rules and road levels of <c>content-golden-v2.json</c> added, as the
    /// engine test adds them (<c>GoldenContent.V1</c>): written once into the output folder next to it.
    /// </summary>
    public static string ContentPath { get; } = WriteV1WithWalkingOfV2();

    /// <summary>
    /// Writes <c>content-golden-v1.json</c> with the walking rules and road levels of <c>content-golden-v2.json</c> added, the way
    /// <c>GoldenContent.V1</c> of the engine tests completes it: v1 predates them, the engine needs them from CR-0008-T1 part C on, and
    /// golden scenario #1 on v1 never walks. Temporary: part E moves the scenario to v2 and deletes v1 (BL-20261102-07, decision 22).
    /// </summary>
    /// <returns>The path of the completed file.</returns>
    private static string WriteV1WithWalkingOfV2()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        JsonObject v1 = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "content-golden-v1.json")))!.AsObject();
        JsonObject v2 = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "content-golden-v2.json")))!.AsObject();
        JsonObject rules = v1["rules"]!.AsObject();
        foreach (string name in new[] { "groundTileMillis", "crossingSpeedPermille", "minWalkSeconds", "maxWalkBonusPermille" })
        {
            rules[name] = v2["rules"]![name]!.DeepClone();
        }

        v1["roads"] = v2["roads"]!.DeepClone();
        string path = Path.Combine(folder, "content-golden-v1-with-walking.json");
        File.WriteAllText(path, v1.ToJsonString());
        return path;
    }

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
                await player.StartResearchAsync("trade_charter");
                await player.PlaceAsync("house", 0, 0);
                await player.PlaceAsync("house", 1, 0);
                break;
            case 30:
                await player.PlaceAsync("woodcutter", 2, 0);
                await player.PlaceAsync("sawmill", 3, 0);
                break;
            case 750:
                await player.PlaceAsync("trading_post", 4, 0);
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
                    Apply(city, content, CityRules.StartResearch(city, content, new ResearchId("trade_charter")));
                    Apply(city, content, CityRules.PlaceBuilding(city, content, new BuildingTypeId("house"), 0, 0));
                    Apply(city, content, CityRules.PlaceBuilding(city, content, new BuildingTypeId("house"), 1, 0));
                    break;
                case 30:
                    Apply(city, content, CityRules.PlaceBuilding(city, content, new BuildingTypeId("woodcutter"), 2, 0));
                    Apply(city, content, CityRules.PlaceBuilding(city, content, new BuildingTypeId("sawmill"), 3, 0));
                    break;
                default:
                    Apply(city, content, CityRules.PlaceBuilding(city, content, new BuildingTypeId("trading_post"), 4, 0));
                    break;
            }
        }

        Simulator.Advance(city, content, time - city.Clock);
        return city;
    }

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
