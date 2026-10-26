using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The server refuses to start without its database settings (docs/getting-started.md → Configuration); in memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server with the test settings; each test derives one with a changed setting.</param>
public sealed class DatabaseStartupTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>A missing, empty or blank connection string fails the options validation at startup, naming the key to set.</summary>
    /// <param name="connectionString">The setting's value; <see langword="null"/> leaves the key out.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_NoConnectionString_FailsValidation(string? connectionString)
    {
        using WebApplicationFactory<Program> broken =
            factory.WithWebHostBuilder(host => host.UseSetting("ConnectionStrings:CityBuilder", connectionString));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures, failure => failure.StartsWith("ConnectionStrings:CityBuilder must hold", StringComparison.Ordinal));
    }
}
