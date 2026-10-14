namespace CityBuilder.IntegrationTests.Content;

/// <summary>Where the tests find content files.</summary>
internal static class ContentFiles
{
    /// <summary>The copy of <c>server/content/mvp-content.json</c> that the Api build puts into this test project's output too.</summary>
    public static string RealMvpContentPath { get; } = Path.Combine(AppContext.BaseDirectory, "content", "mvp-content.json");
}
