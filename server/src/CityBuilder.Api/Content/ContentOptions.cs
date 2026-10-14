namespace CityBuilder.Api.Content;

/// <summary>Configuration section <c>Content</c>: which content file the server loads at startup.</summary>
public sealed class ContentOptions
{
    /// <summary>The configuration section's name.</summary>
    public const string SectionName = "Content";

    /// <summary>
    /// <c>Content:FilePath</c>, required: the content file, absolute or relative to the app's base directory (where the build copies
    /// <c>server/content/*.json</c> into <c>content/</c>). The default in <c>appsettings.json</c> is <c>content/mvp-content.json</c>.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>The full path of <see cref="FilePath"/>.</summary>
    /// <returns>The path, resolved against the app's base directory when it is relative.</returns>
    public string GetFullFilePath() => Path.GetFullPath(FilePath, AppContext.BaseDirectory);
}
