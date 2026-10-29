using CityBuilder.Api.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// A test-only endpoint, <c>/test/errors/{code}</c>, that answers with the catalogue's error of that code through <see cref="ApiProblem"/>
/// (and the <c>requestId</c> in the query, when there is one): the shape of every code is tested over HTTP, as a client reads it, without
/// a scenario for each. It runs in front of the server's own pipeline; a request to any other path goes on to the server.
/// </summary>
internal sealed class ErrorEndpoint : IStartupFilter
{
    /// <summary>Puts the endpoint in front of the server's pipeline.</summary>
    /// <param name="next">The server's pipeline.</param>
    /// <returns>The pipeline with the endpoint first.</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapGet(
            "/test/errors/{code}",
            (string code, Guid? requestId) => ApiProblem.Of(ErrorCatalogue.All.Single(error => error.Code == code), requestId ?? default)));
        next(app);
    };

    /// <summary>A copy of the server with this endpoint added.</summary>
    /// <param name="factory">The server.</param>
    /// <returns>The copy; the caller disposes it.</returns>
    public static WebApplicationFactory<Program> AddTo(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(host => host.ConfigureTestServices(services => services.AddTransient<IStartupFilter, ErrorEndpoint>()));
}
