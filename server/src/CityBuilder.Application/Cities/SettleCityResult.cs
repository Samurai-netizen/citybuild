namespace CityBuilder.Application.Cities;

/// <summary>What <see cref="SettleCity"/> did: the settled city, the settlement, and whether the city was saved.</summary>
/// <param name="city">The city, settled to now.</param>
/// <param name="settlement">The settlement: its window and what happened during it (<c>core-instructions/04-simulation-model.md</c>,
/// section 11).</param>
/// <param name="saved">Whether anything changed, so the city was saved with a new <c>state_version</c>.</param>
public sealed class SettleCityResult(CityAggregate city, Settlement settlement, bool saved)
{
    /// <summary>The city, settled to now.</summary>
    public CityAggregate City { get; } = city;

    /// <summary>The settlement: its window and what happened during it.</summary>
    public Settlement Settlement { get; } = settlement;

    /// <summary>Whether the city was saved; <see langword="false"/> when the settle changed nothing (<c>state_version</c> stays).</summary>
    public bool Saved { get; } = saved;
}
