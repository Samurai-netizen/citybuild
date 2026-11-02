using System.Net;
using System.Net.Http.Headers;
using CityBuilder.Api.Auth;
using CityBuilder.Api.Http;
using CityBuilder.IntegrationTests.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Logging;

/// <summary>
/// The client's build version in the server's log (M0-58, <c>05</c> Versioning): the <c>X-Client-Version</c> header becomes the <c>ClientVersion</c>
/// scope value of every line of the request, a missing or unusable header is logged as <c>none</c> or <c>invalid</c> (never as the text), and no
/// request is refused over it. Over HTTP on the real pipeline with a <see cref="FakeCityStore"/> and a <see cref="CapturingLogger"/>; no Docker.
/// </summary>
/// <param name="factory">The in-memory server (Development, limits relaxed); each test derives a copy with its store, clock and logger.</param>
public sealed class ClientVersionLogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>The player of the tests.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");

    /// <summary>The instant the fake clock starts at, which is also when the starter city was last settled.</summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Sends <c>GET /city</c> with the given <c>X-Client-Version</c> values (none for no header) and returns the answer and the settlement line (event 6000).</summary>
    /// <param name="versions">The header values to send; empty sends no header.</param>
    /// <returns>The HTTP status and the settlement log line of the request.</returns>
    private async Task<(HttpStatusCode Status, CapturingLogger.Entry Settled)> GetCityAsync(params string[] versions)
    {
        var logs = new CapturingLogger();
        var store = new FakeCityStore();
        store.Add(Starter(ContentOf(factory), Guid.NewGuid(), _playerId, _start.ToUnixTimeSeconds()));
        await using WebApplicationFactory<Program> server = logs.AddTo(store.Serve(factory, new FakeTimeProvider(_start)));
        using HttpClient client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/city");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Services.GetRequiredService<AccessTokens>().Issue(_playerId).Token);
        if (versions.Length > 0)
        {
            request.Headers.TryAddWithoutValidation(ClientVersionHeader.HeaderName, versions);
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.StatusCode, Assert.Single(logs.Events(6000)));
    }

    /// <summary>A request with a version header is logged with that version on its lines.</summary>
    [Fact]
    public async Task Get_WithClientVersion_LogsItOnTheRequestsLines()
    {
        (HttpStatusCode status, CapturingLogger.Entry settled) = await GetCityAsync("0.1.0");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("0.1.0", settled.Scope(ClientVersionHeader.ScopeName));
    }

    /// <summary>A request without the header is served and logged as <c>none</c>: nothing is refused over the version yet.</summary>
    [Fact]
    public async Task Get_WithoutClientVersion_IsServedAndLoggedAsNone()
    {
        (HttpStatusCode status, CapturingLogger.Entry settled) = await GetCityAsync();

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ClientVersionHeader.None, settled.Scope(ClientVersionHeader.ScopeName));
    }

    /// <summary>A header that is not a version is served and logged as <c>invalid</c>, and its text is in no part of the line.</summary>
    [Theory]
    [InlineData("1.0 beta")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("1.0.0\\u0007x")]
    [InlineData("11111111111111111111111111111111111111111111111111")]
    public async Task Security_Get_WithUnusableClientVersion_IsServedAndLoggedAsInvalid(string version)
    {
        (HttpStatusCode status, CapturingLogger.Entry settled) = await GetCityAsync(version);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ClientVersionHeader.Invalid, settled.Scope(ClientVersionHeader.ScopeName));
        Assert.DoesNotContain(version, settled.AllText(), StringComparison.Ordinal);
    }

    /// <summary>A repeated header is not one version.</summary>
    [Fact]
    public async Task Get_WithTwoClientVersions_LogsInvalid()
    {
        (HttpStatusCode status, CapturingLogger.Entry settled) = await GetCityAsync("0.1.0", "0.2.0");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ClientVersionHeader.Invalid, settled.Scope(ClientVersionHeader.ScopeName));
    }

    /// <summary>The rule on its own: what is a version and what is logged for the rest.</summary>
    [Theory]
    [InlineData(null, "none")]
    [InlineData("", "invalid")]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("1.2.3-beta.4+build_5", "1.2.3-beta.4+build_5")]
    [InlineData("a b", "invalid")]
    [InlineData("ä", "invalid")]
    [InlineData("12345678901234567890123456789012", "12345678901234567890123456789012")]
    [InlineData("123456789012345678901234567890123", "invalid")]
    public void ForLog_OneValue_IsTheVersionOrAMarker(string? header, string expected)
    {
        StringValues values = header is null ? StringValues.Empty : new StringValues(header);

        Assert.Equal(expected, ClientVersionHeader.ForLog(values));
    }
}
