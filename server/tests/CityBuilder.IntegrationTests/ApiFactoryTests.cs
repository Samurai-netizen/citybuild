using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests;

/// <summary>What the test servers of <see cref="ApiFactory"/> need to run by the hundred in one process.</summary>
/// <param name="factory">The in-memory server.</param>
public sealed class ApiFactoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// No test server watches a configuration file: with hundreds of them, <c>FileSystemWatcher</c> stalled on macOS and the test project,
    /// run without a filter, never ended (CR-0004).
    /// </summary>
    [Fact]
    public void Server_Configuration_WatchesNoFile()
    {
        var configuration = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();

        FileConfigurationProvider[] files = [.. configuration.Providers.OfType<FileConfigurationProvider>()];

        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.False(file.Source.ReloadOnChange, file.ToString()));
    }
}
