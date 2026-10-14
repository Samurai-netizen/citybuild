using System.Text.Json.Nodes;

namespace CityBuilder.IntegrationTests.Content;

/// <summary>Where the tests find content files.</summary>
internal static class ContentFiles
{
    /// <summary>The copy of <c>server/content/mvp-content.json</c> that the Api build puts into this test project's output too.</summary>
    public static string RealMvpContentPath { get; } = Path.Combine(AppContext.BaseDirectory, "content", "mvp-content.json");

    /// <summary>The real content as a JSON tree that a test may change, for example to break one value.</summary>
    public static JsonNode ReadRealMvpContent() =>
        JsonNode.Parse(File.ReadAllText(RealMvpContentPath)) ?? throw new InvalidOperationException("The real content file holds null.");
}

/// <summary>A new temporary directory for the content files of one test; deleted with everything in it on dispose.</summary>
internal sealed class ContentFileDirectory : IDisposable
{
    private int _fileCount;

    public ContentFileDirectory()
    {
        Directory = Path.Combine(Path.GetTempPath(), "citybuilder-content-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
    }

    public string Directory { get; }

    /// <summary>Writes a new file with the text and returns its path.</summary>
    public string Write(string text)
    {
        _fileCount++;
        string path = Path.Combine(Directory, $"content-{_fileCount}.json");
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}
