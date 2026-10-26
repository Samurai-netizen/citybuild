using CityBuilder.Application.Cities;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>A fake <see cref="ICityRepository"/> that keeps the cities in memory, for use case tests without a database.</summary>
internal sealed class InMemoryCityRepository : ICityRepository
{
    /// <summary>The cities, by player id.</summary>
    private readonly Dictionary<Guid, CityAggregate> _cities = [];

    /// <summary>How often a city was saved.</summary>
    public int SaveCount { get; private set; }

    /// <summary>Adds a player's city before the test runs; a second city for the same player throws.</summary>
    public void Add(CityAggregate city) => _cities.Add(city.PlayerId, city);

    /// <summary>The player's city. There is no row to lock in memory; a player without a city throws <see cref="KeyNotFoundException"/>.</summary>
    public Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken) => Task.FromResult(_cities[playerId]);

    /// <summary>Stores the city in place of the player's city and counts the save in <see cref="SaveCount"/>.</summary>
    public Task SaveAsync(CityAggregate city, CancellationToken cancellationToken)
    {
        _cities[city.PlayerId] = city;
        SaveCount++;
        return Task.CompletedTask;
    }
}
