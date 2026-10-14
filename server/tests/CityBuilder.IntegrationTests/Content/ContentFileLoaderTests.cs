using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation.Content;

namespace CityBuilder.IntegrationTests.Content;

/// <summary>The content file loader (core-instructions/03-architecture.md, content pipeline); runs in memory, no Docker.</summary>
public sealed class ContentFileLoaderTests
{
    [Fact]
    public void Load_RealMvpContent_ReturnsValidContent()
    {
        // The real server/content/mvp-content.json, as the Api build copies it next to the app. A rule error fails here with every
        // error in the message (ContentLoadException), so a broken content change names what to fix.
        GameContent content = ContentFileLoader.Load(ContentFiles.RealMvpContentPath);

        Assert.False(string.IsNullOrEmpty(content.ContentVersion));
    }
}
