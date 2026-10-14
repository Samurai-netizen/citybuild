using System.Text.Json.Nodes;
using CityBuilder.Application.Content;
using CityBuilder.Infrastructure.Content;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CityBuilder.IntegrationTests.Content;

/// <summary>
/// The server loads the content before it accepts requests and refuses to start with bad content (core-instructions/03-architecture.md,
/// content pipeline); in memory, no Docker.
/// </summary>
public sealed class ContentStartupTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly ContentFileDirectory _files = new();

    public void Dispose() => _files.Dispose();

    [Fact]
    public void Start_DefaultConfiguration_ServesTheRealContent()
    {
        IContentProvider provider = factory.Services.GetRequiredService<IContentProvider>();

        Assert.Equal(ContentFileLoader.Load(ContentFiles.RealMvpContentPath).ContentVersion, provider.Content.ContentVersion);
        // One load for the whole server: every request sees the same instance.
        Assert.Same(provider, factory.Services.GetRequiredService<IContentProvider>());
    }

    [Fact]
    public void Start_ContentWithRuleErrors_FailsListingEveryError()
    {
        JsonNode content = ContentFiles.ReadRealMvpContent();
        content["buildings"]![0]!["buildSeconds"] = 0;
        content["research"]![0]!["durationSeconds"] = 0;
        string path = _files.Write(content.ToJsonString());
        using WebApplicationFactory<Program> broken = WithContentFilePath(path);

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => broken.Services);

        Assert.Equal(path, exception.FilePath);
        Assert.Equal(2, exception.Errors.Count);
    }

    [Fact]
    public void Start_MissingContentFile_Fails()
    {
        string path = Path.Combine(_files.Directory, "missing.json");
        using WebApplicationFactory<Program> broken = WithContentFilePath(path);

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => broken.Services);

        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_EmptyContentFilePath_FailsValidation(string filePath)
    {
        using WebApplicationFactory<Program> broken = WithContentFilePath(filePath);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains("Content:FilePath must name the content file.", exception.Failures);
    }

    private WebApplicationFactory<Program> WithContentFilePath(string filePath) =>
        factory.WithWebHostBuilder(host => host.UseSetting("Content:FilePath", filePath));
}
