using CityBuilder.Api.Contracts;
using CityBuilder.Api.Http;
using CityBuilder.Api.Time;
using CityBuilder.Application.Content;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CityBuilder.Api.Endpoints;

/// <summary>The content endpoint under <c>/api/v1</c> (core-instructions/05-api-contract.md).</summary>
public static class ContentEndpoints
{
    /// <summary>Maps <c>GET /content</c> on the <c>/api/v1</c> group, which requires an access token.</summary>
    /// <param name="api">The <c>/api/v1</c> route group.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapContent(this RouteGroupBuilder api) => api.MapGet("/content", GetContent).RequireRateLimit(RateLimitClass.Content);

    /// <summary>
    /// Answers with the game content the server runs on: items, buildings with their levels and recipes, the research tree, eras and rules.
    /// The content is loaded once at startup and is the same for every player, so the call reads no city and writes nothing. Its
    /// <c>rules.timeScale</c> is the server clock's (<see cref="ServerClock.TimeScaleOf"/>): above 1 only on a Development server with a fast clock.
    /// </summary>
    /// <param name="content">The server's content.</param>
    /// <param name="clock">The server clock, for its time scale.</param>
    /// <returns>200 with the content.</returns>
    private static Ok<ContentResponse> GetContent(IContentProvider content, TimeProvider clock) =>
        TypedResults.Ok(ContentResponseMapper.ToResponse(content.Content, ServerClock.TimeScaleOf(clock)));
}
