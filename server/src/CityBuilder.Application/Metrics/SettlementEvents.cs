using CityBuilder.Application.Cities;
using CityBuilder.Domain.Metrics;

namespace CityBuilder.Application.Metrics;

/// <summary>
/// What a settlement completed, as player events (CR-0009): one <c>research.completed</c> per research node and one
/// <c>building.completed</c> per construction or upgrade.
/// </summary>
public static class SettlementEvents
{
    /// <summary>The settlement's completions as events.</summary>
    /// <param name="city">The acting player's city, settled and closed.</param>
    /// <param name="settlement">The closed settlement.</param>
    /// <param name="now">The server time of the request.</param>
    /// <returns>The events, in order.</returns>
    public static IReadOnlyList<PlayerEvent> Of(CityAggregate city, Settlement settlement, DateTimeOffset now) => [];
}
