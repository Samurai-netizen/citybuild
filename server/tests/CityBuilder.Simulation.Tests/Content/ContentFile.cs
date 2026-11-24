using System.Text.Json;
using System.Text.Json.Serialization;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// Reads a content file of this test project's <c>Fixtures/</c> folder into a valid <see cref="GameContent"/>, as the server reads its
/// content file: the frozen golden contents, the performance budget's worst case and (M1-02) the server's
/// own content files.
/// </summary>
internal static class ContentFile
{
    /// <summary>
    /// The same strict reading as the server's <c>ContentFileLoader</c> (Infrastructure, which this project must not reference): camelCase and
    /// case-sensitive names, no unknown fields, no repeated keys, numbers only as numbers.
    /// </summary>
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>The content in <c>Fixtures/<paramref name="fileName"/></c>; the test fails with every error if it is not valid.</summary>
    public static GameContent Load(string fileName) => ContentAssert.Valid(Read(fileName));

    /// <summary>The data in <c>Fixtures/<paramref name="fileName"/></c>, read strictly but not checked yet, so a caller can complete it first.</summary>
    public static GameContentData Read(string fileName) =>
        ReadPath(Path.Combine(RepositoryPaths.Root, "server", "tests", "CityBuilder.Simulation.Tests", "Fixtures", fileName));

    /// <summary>The server's own content file <c>server/content/<paramref name="fileName"/></c> (M1-02), read strictly but not checked yet.</summary>
    public static GameContentData ReadServer(string fileName) => ReadPath(Path.Combine(RepositoryPaths.Root, "server", "content", fileName));

    /// <summary>The content data in the file at <paramref name="path"/>, read strictly.</summary>
    private static GameContentData ReadPath(string path) =>
        JsonSerializer.Deserialize<GameContentData>(File.ReadAllText(path), _options)
            ?? throw new InvalidOperationException($"{path} holds the JSON value null.");
}
