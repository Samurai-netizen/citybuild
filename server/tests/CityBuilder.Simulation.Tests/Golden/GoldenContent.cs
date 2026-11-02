using System.Text.Json;
using System.Text.Json.Nodes;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.Tests.Content;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// The frozen content files of the golden scenarios (<c>08</c>): copies of <c>server/content/mvp-content.json</c> that never change, so a
/// balance change can't rewrite a golden expectation. A content change that should reach a golden scenario gets a new file (v2) and new,
/// hand-checked expectations.
/// </summary>
internal static class GoldenContent
{
    /// <summary>
    /// <c>Fixtures/content-golden-v1.json</c>: the content <c>mvp-1</c> as it was in M0-11, for golden scenario #1, with the walking rules and
    /// road levels of <c>content-golden-v2.json</c> added (<see cref="WithWalkingOfV2"/>). The file stays as it was frozen.
    /// </summary>
    public static GameContent V1 { get; } = ContentAssert.Valid(WithWalkingOfV2(ContentFile.Read("content-golden-v1.json")));

    /// <summary>
    /// Adds the walking rules and road levels of <c>content-golden-v2.json</c> to content that predates them (v1, frozen in M0-11, before
    /// CR-0008). The engine needs them from CR-0008-T1 part C on; golden scenario #1 on v1 never walks or builds a road, so they change no
    /// expectation. Temporary: part E moves golden scenario #1 to v2 and deletes v1 (build log BL-20261102-07, decision 22).
    /// </summary>
    private static GameContentData WithWalkingOfV2(GameContentData v1)
    {
        // v2 has the fields of later parts too, which the strict reader refuses for now: only the walking values are taken from it.
        string v2Path = Path.Combine(RepositoryPaths.Root, "server", "tests", "CityBuilder.Simulation.Tests", "Fixtures", "content-golden-v2.json");
        JsonNode v2 = JsonNode.Parse(File.ReadAllText(v2Path)) ?? throw new InvalidOperationException("content-golden-v2.json holds null.");
        JsonNode v2Rules = v2["rules"] ?? throw new InvalidOperationException("content-golden-v2.json has no rules.");
        GameRulesData rules = v1.Rules ?? throw new InvalidOperationException("content-golden-v1.json has no rules.");
        rules.GroundTileMillis = v2Rules["groundTileMillis"]!.GetValue<long>();
        rules.CrossingSpeedPermille = v2Rules["crossingSpeedPermille"]!.GetValue<int>();
        rules.MinWalkSeconds = v2Rules["minWalkSeconds"]!.GetValue<long>();
        rules.MaxWalkBonusPermille = v2Rules["maxWalkBonusPermille"]!.GetValue<int>();
        v1.Roads = v2["roads"]!.Deserialize<List<RoadLevelData?>>(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return v1;
    }
}
