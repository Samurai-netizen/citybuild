using System.Text.Json;
using System.Text.Json.Serialization;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// Reads a content file of this test project's <c>Fixtures/</c> folder into a valid <see cref="GameContent"/>, as the server reads its
/// content file: the frozen golden contents and the performance budget's worst case.
/// </summary>
internal static class ContentFile
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

    /// <summary>The content in <c>Fixtures/<paramref name="fileName"/></c>; the test fails with every error if it is not valid.</summary>
    public static GameContent Load(string fileName)
    {
        string path = Path.Combine(RepositoryPaths.Root, "server", "tests", "CityBuilder.Simulation.Tests", "Fixtures", fileName);
        GameContentData data = JsonSerializer.Deserialize<GameContentData>(File.ReadAllText(path), _options)
            ?? throw new InvalidOperationException($"{path} holds the JSON value null.");
        return ContentAssert.Valid(data);
    }
}
