using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace CityBuilder.IntegrationTests.Auth;

/// <summary>
/// The server refuses to start without a usable signing key, with a token lifetime out of range, or with the dev login on outside
/// Development (docs/getting-started.md → Configuration; core-instructions/07-security-model.md). In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server with the test settings; each test derives one with a changed setting.</param>
public sealed class AuthStartupTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>A missing or empty signing key, or one shorter than 32 bytes, fails the options validation at startup, naming the key.</summary>
    /// <param name="signingKey">The setting's value; <see langword="null"/> leaves the key out.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcde")]
    public void Start_NoOrShortSigningKey_FailsValidation(string? signingKey)
    {
        using WebApplicationFactory<Program> broken = factory.WithWebHostBuilder(host => host.UseSetting("Auth:SigningKey", signingKey));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures, failure => failure.StartsWith("Auth:SigningKey must hold", StringComparison.Ordinal));
    }

    /// <summary>A signing key of exactly 32 bytes is enough: the server starts.</summary>
    [Fact]
    public void Start_SigningKeyOfThirtyTwoBytes_Starts()
    {
        using WebApplicationFactory<Program> server =
            factory.WithWebHostBuilder(host => host.UseSetting("Auth:SigningKey", "0123456789abcdef0123456789abcdef"));

        Assert.NotNull(server.Services);
    }

    /// <summary>A token lifetime of 0, below it, or above a day fails the options validation at startup.</summary>
    /// <param name="minutes">The setting's value.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1441")]
    public void Start_TokenLifetimeOutOfRange_FailsValidation(string minutes)
    {
        using WebApplicationFactory<Program> broken =
            factory.WithWebHostBuilder(host => host.UseSetting("Auth:TokenLifetimeMinutes", minutes));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures,
            failure => failure.StartsWith("Auth:TokenLifetimeMinutes must be between 1 and 1440", StringComparison.Ordinal));
    }

    /// <summary>The dev login switched on outside Development stops the start: nobody can log in by name alone in production.</summary>
    /// <param name="environment">An environment other than Development.</param>
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Security_DevLoginEnabledOutsideDevelopment_StopsTheStart(string environment)
    {
        using WebApplicationFactory<Program> broken = factory.WithWebHostBuilder(host => host
            .UseEnvironment(environment)
            .UseSetting("Auth:DevLoginEnabled", "true"));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => broken.Services);

        Assert.Contains(exception.Failures,
            failure => failure.StartsWith("Auth:DevLoginEnabled may be true only in Development", StringComparison.Ordinal));
    }
}
