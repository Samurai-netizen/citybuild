using CityBuilder.Api.Contracts;
using CityBuilder.Api.Http;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Application.Players;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CityBuilder.Api.Endpoints;

/// <summary>The city endpoint under <c>/api/v1</c> (core-instructions/05-api-contract.md).</summary>
public static class CityEndpoints
{
    /// <summary>Maps <c>GET /city</c> on the <c>/api/v1</c> group, which requires an access token.</summary>
    /// <param name="api">The <c>/api/v1</c> route group.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapCity(this RouteGroupBuilder api) => api.MapGet("/city", GetCityAsync).RequireRateLimit(RateLimitClass.City);

    /// <summary>
    /// Settles the acting player's city to now (<see cref="SettleCity"/>: in the city's transaction, saved only if anything changed) and answers
    /// with the snapshot. Safe to repeat, but not free of side effects: it materializes the elapsed time (ADR-0005). The city is the token's
    /// player's; nothing in the request names one.
    /// </summary>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="settleCity">The use case.</param>
    /// <param name="content">The server's content.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot; its <c>requestId</c> is null, because a read is no command.</returns>
    private static async Task<Ok<CitySnapshot>> GetCityAsync(
        ICurrentPlayer player, SettleCity settleCity, IContentProvider content, CancellationToken cancellationToken)
    {
        SettleCityResult result = await settleCity.ExecuteAsync(player.PlayerId, cancellationToken);
        return TypedResults.Ok(CitySnapshotMapper.ToSnapshot(
            requestId: null, result.City, result.Settlement, result.StateVersion, result.ServerTime, content.Content));
    }
}
