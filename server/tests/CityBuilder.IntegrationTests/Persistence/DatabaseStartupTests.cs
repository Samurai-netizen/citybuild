using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The server refuses to start without its database settings, and migrates at startup in Development only (docs/getting-started.md →
/// Configuration); in memory, no Docker.
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

    /// <summary>The startup migration switched on outside Development stops the start; nothing is migrated as a side effect.</summary>
    /// <param name="environment">An environment other than Development.</param>
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Start_MigrationsAtStartupOutsideDevelopment_FailsValidation(string environment)
    {
        using WebApplicationFactory<Program> broken = factory.WithWebHostBuilder(host => host
            .UseEnvironment(environment)
            .UseSetting("Database:ApplyMigrationsAtStartup", "true"));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures,
            failure => failure.StartsWith("Database:ApplyMigrationsAtStartup may be true only in Development", StringComparison.Ordinal));
    }

    /// <summary>
    /// In Development with the startup migration on, the server migrates before it accepts requests: with a database it can't reach,
    /// the start fails with the connection error instead of starting without a schema.
    /// </summary>
    [Fact]
    public void Start_MigrationsAtStartupInDevelopment_MigratesBeforeServing()
    {
        using WebApplicationFactory<Program> unreachable = factory.WithWebHostBuilder(host => host
            .UseEnvironment(Environments.Development)
            .UseSetting("Database:ApplyMigrationsAtStartup", "true"));

        Assert.ThrowsAny<NpgsqlException>(() => unreachable.Services);
    }
}
