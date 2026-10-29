using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.RateLimits;

/// <summary>
/// Gives a request of the in-memory server the remote address its <c>X-Test-Address</c> header names (the test server has none, so every
/// request would be "unknown"): the dev login is limited per address, and a test needs several.
/// </summary>
internal sealed class RemoteAddressFilter : IStartupFilter
{
    /// <summary>The header that names the address.</summary>
    public const string Header = "X-Test-Address";

    /// <summary>Puts the step in front of the server's pipeline.</summary>
    /// <param name="next">The server's pipeline.</param>
    /// <returns>The pipeline with the step first.</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, forward) =>
        {
            if (context.Request.Headers[Header].ToString() is { Length: > 0 } address)
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(address);
            }

            return forward(context);
        });
        next(app);
    };

    /// <summary>A copy of the server with this step added.</summary>
    /// <param name="factory">The server.</param>
    /// <returns>The copy; the caller disposes it.</returns>
    public static WebApplicationFactory<Program> AddTo(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(host => host.ConfigureTestServices(services => services.AddTransient<IStartupFilter, RemoteAddressFilter>()));
}
