using CityBuilder.Application.Cities;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>A fake <see cref="ICityRepository"/> that keeps the cities in memory, for use case tests without a database.</summary>
internal sealed class InMemoryCityRepository : ICityRepository
{
    private readonly Dictionary<Guid, CityAggregate> _cities = [];

    /// <summary>How often a city was saved.</summary>
    public int SaveCount { get; private set; }

    public void Add(CityAggregate city) => _cities.Add(city.PlayerId, city);

    public Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken) => Task.FromResult(_cities[playerId]);

    public Task SaveAsync(CityAggregate city, CancellationToken cancellationToken)
    {
        _cities[city.PlayerId] = city;
        SaveCount++;
        return Task.CompletedTask;
    }
}
