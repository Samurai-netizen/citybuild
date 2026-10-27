namespace CityBuilder.Application.Cities;

/// <summary>
/// What <see cref="SettleCity"/> did: the settled city, the settlement, whether the city was saved, and the server time it settled at. It
/// carries what the snapshot response needs (<c>core-instructions/05-api-contract.md</c>, snapshot), as <see cref="Commands.CommandSuccess"/>
/// does for a command.
/// </summary>
/// <param name="city">The city, settled to now.</param>
/// <param name="settlement">The settlement: its window and what happened during it (<c>core-instructions/04-simulation-model.md</c>,
/// section 11).</param>
/// <param name="saved">Whether anything changed, so the city was saved with a new <c>state_version</c>.</param>
/// <param name="serverTime">The server time the settlement ran at, as the clock gave it (not floored to the second).</param>
public sealed class SettleCityResult(CityAggregate city, Settlement settlement, bool saved, DateTimeOffset serverTime)
{
    /// <summary>The city, settled to now.</summary>
    public CityAggregate City { get; } = city;

    /// <summary>The settlement: its window and what happened during it.</summary>
    public Settlement Settlement { get; } = settlement;

    /// <summary>Whether the city was saved; <see langword="false"/> when the settle changed nothing (<c>state_version</c> stays).</summary>
    public bool Saved { get; } = saved;

    /// <summary>The server time the settlement ran at (<c>serverTimeUtc</c> of the snapshot, floored to the second there).</summary>
    public DateTimeOffset ServerTime { get; } = serverTime;

    /// <summary>
    /// The city's <c>state_version</c> as stored once the settlement is committed: the loaded version plus 1 when the city was saved, the
    /// loaded version when the settlement changed nothing. The city itself keeps the version it was loaded with.
    /// </summary>
    public long StateVersion => Saved ? checked(City.StateVersion + 1) : City.StateVersion;
}
