using System.Text.Json;
using System.Text.Json.Serialization;
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
    // The same strict reading as the server's ContentFileLoader (Infrastructure, which this project must not reference): camelCase and
    // case-sensitive names, no unknown fields, no repeated keys, numbers only as numbers.
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary><c>Fixtures/content-golden-v1.json</c>: the content <c>mvp-1</c> as it was in M0-11, for golden scenario #1.</summary>
    public static GameContent V1 { get; } = Load("content-golden-v1.json");

    private static GameContent Load(string fileName)
    {
        string path = Path.Combine(RepositoryPaths.Root, "server", "tests", "CityBuilder.Simulation.Tests", "Fixtures", fileName);
        GameContentData data = JsonSerializer.Deserialize<GameContentData>(File.ReadAllText(path), _options)
            ?? throw new InvalidOperationException($"{path} holds the JSON value null.");
        return ContentAssert.Valid(data);
    }
}
