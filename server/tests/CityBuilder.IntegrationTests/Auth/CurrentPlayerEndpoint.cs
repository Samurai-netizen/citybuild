using CityBuilder.Application.Players;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Auth;

/// <summary>
/// A test-only endpoint, <see cref="Path"/>, that requires an access token and answers with <see cref="ICurrentPlayer.PlayerId"/> as text:
/// the token checks and the player resolution are tested over HTTP before the game endpoints that need a token exist (M0-28). It runs in
/// front of the server's own pipeline, with its own routing, authentication and authorization over the server's services; a request to any
/// other path goes on to the server.
/// </summary>
internal sealed class CurrentPlayerEndpoint : IStartupFilter
{
    /// <summary>The endpoint's path.</summary>
    public static readonly Uri Path = new("/test/current-player", UriKind.Relative);

    /// <summary>Puts the endpoint in front of the server's pipeline.</summary>
    /// <param name="next">The server's pipeline.</param>
    /// <returns>The pipeline with the endpoint first.</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints
            .MapGet(Path.OriginalString, (ICurrentPlayer player) => player.PlayerId.ToString("D"))
            .RequireAuthorization());
        next(app);
    };

    /// <summary>A copy of the server with this endpoint added.</summary>
    /// <param name="factory">The server.</param>
    /// <param name="configure">Further changes to the copy's host, or <see langword="null"/>.</param>
    /// <returns>The copy; the caller disposes it.</returns>
    public static WebApplicationFactory<Program> AddTo(WebApplicationFactory<Program> factory, Action<IWebHostBuilder>? configure = null) =>
        factory.WithWebHostBuilder(host =>
        {
            host.ConfigureTestServices(services => services.AddTransient<IStartupFilter, CurrentPlayerEndpoint>());
            configure?.Invoke(host);
        });
}
