using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Application.Cities;

/// <summary>What <see cref="SettleCity"/> did: the settled city, what happened in it, and the window it covered.</summary>
/// <param name="city">The city, settled to now.</param>
/// <param name="report">What happened during the window (<c>core-instructions/04-simulation-model.md</c>, section 11).</param>
/// <param name="window">The server seconds the settlement covered; <see cref="SettlementWindow.IsClockBehind"/> calls for a warning.</param>
public sealed class SettleCityResult(CityAggregate city, SettlementReport report, SettlementWindow window)
{
    /// <summary>The city, settled to now.</summary>
    public CityAggregate City { get; } = city;

    /// <summary>What happened during the window.</summary>
    public SettlementReport Report { get; } = report;

    /// <summary>The server seconds the settlement covered.</summary>
    public SettlementWindow Window { get; } = window;
}
