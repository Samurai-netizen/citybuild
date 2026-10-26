using CityBuilder.Application.Cities;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// A fake <see cref="ICityRepository"/> and <see cref="IUnitOfWork"/> in one, for use case tests without a database: it keeps the cities in
/// memory and counts the saves and the writes.
/// </summary>
internal sealed class InMemoryCityRepository : ICityRepository, IUnitOfWork
{
    /// <summary>The cities, by player id.</summary>
    private readonly Dictionary<Guid, CityAggregate> _cities = [];

    /// <summary>How often a city was staged with <see cref="Save"/>.</summary>
    public int SaveCount { get; private set; }

    /// <summary>How often the staged changes were written with <see cref="SaveChangesAsync"/>.</summary>
    public int SaveChangesCount { get; private set; }

    /// <summary>The ledger entries of the last <see cref="Save"/>, or <see langword="null"/> before the first.</summary>
    public IReadOnlyList<LedgerEntry>? LastLedger { get; private set; }

    /// <summary>The receipt of the last <see cref="Save"/>; <see langword="null"/> before the first or when it had none.</summary>
    public CommandReceipt? LastReceipt { get; private set; }

    /// <summary>Adds a player's city before the test runs; a second city for the same player throws.</summary>
    public void Add(CityAggregate city) => _cities.Add(city.PlayerId, city);

    /// <summary>The player's city. There is no row to lock in memory; a player without a city throws <see cref="KeyNotFoundException"/>.</summary>
    public Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken) => Task.FromResult(_cities[playerId]);

    /// <summary>Keeps the city in place of the player's city, remembers the ledger and the receipt, and counts the save in <see cref="SaveCount"/>.</summary>
    public void Save(CityAggregate city, IReadOnlyList<LedgerEntry> ledger, CommandReceipt? receipt)
    {
        _cities[city.PlayerId] = city;
        LastLedger = ledger;
        LastReceipt = receipt;
        SaveCount++;
    }

    /// <summary>Counts the write in <see cref="SaveChangesCount"/>; the cities are already in memory.</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}
