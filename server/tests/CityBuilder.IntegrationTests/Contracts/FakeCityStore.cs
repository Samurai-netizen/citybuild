using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Persistence;
using CityBuilder.Domain.Commands;
using CityBuilder.Domain.Ledger;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// The cities of a server under test, in memory: a fake <see cref="ICityRepository"/>, <see cref="IUnitOfWork"/> and
/// <see cref="ICommandReceiptRepository"/> in one, so the real <see cref="SettleCity"/>, command pipeline, transaction runner and endpoints run
/// on cities a test built by hand, without a database. Like the real repository, a save keeps the city with the next <c>state_version</c>
/// and the next load sees it, a command's receipt is found by the next request with its id, and the ledger rows of every save are kept for
/// the test to read. There is no row to lock and nothing to roll back in memory.
/// </summary>
internal sealed class FakeCityStore : ICityRepository, IUnitOfWork, ICommandReceiptRepository
{
    /// <summary>The cities, by player id.</summary>
    private readonly Dictionary<Guid, CityAggregate> _cities = [];

    /// <summary>The receipts of the saved commands, by player id and request id.</summary>
    private readonly Dictionary<(Guid Player, Guid Request), CommandReceipt> _receipts = [];

    /// <summary>How often a city was saved.</summary>
    public int SaveCount { get; private set; }

    /// <summary>The ledger rows of every save, in order.</summary>
    public List<LedgerEntry> Ledger { get; } = [];

    /// <summary>Adds a player's city before the test runs.</summary>
    /// <param name="city">The city; its player must not have one yet.</param>
    public void Add(CityAggregate city) => _cities.Add(city.PlayerId, city);

    /// <summary>The player's city as stored now.</summary>
    /// <param name="playerId">The player.</param>
    /// <returns>The stored city.</returns>
    public CityAggregate Stored(Guid playerId) => _cities[playerId];

    /// <summary>A copy of the server with this store as its cities, <paramref name="clock"/> as its clock, and optionally other content.</summary>
    /// <param name="factory">The in-memory server.</param>
    /// <param name="clock">The server clock of the copy.</param>
    /// <param name="contentFilePath">The content file the copy loads, or <see langword="null"/> for the real content.</param>
    /// <returns>The copy; the caller disposes it.</returns>
    public WebApplicationFactory<Program> Serve(WebApplicationFactory<Program> factory, FakeTimeProvider clock, string? contentFilePath = null) =>
        factory.WithWebHostBuilder(host =>
        {
            if (contentFilePath is not null)
            {
                host.UseSetting("Content:FilePath", contentFilePath);
            }

            host.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICityRepository>();
                services.RemoveAll<IUnitOfWork>();
                services.RemoveAll<ICommandReceiptRepository>();
                services.AddSingleton<ICityRepository>(this);
                services.AddSingleton<IUnitOfWork>(this);
                services.AddSingleton<ICommandReceiptRepository>(this);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
        });

    /// <inheritdoc />
    public Task<CityAggregate> LoadForUpdateAsync(Guid playerId, CancellationToken cancellationToken) =>
        Task.FromResult(_cities.TryGetValue(playerId, out CityAggregate? city)
            ? city
            : throw new InvalidOperationException($"Player {playerId} has no city."));

    /// <inheritdoc />
    public void Save(CityAggregate city, IReadOnlyList<LedgerEntry> ledger, CommandReceipt? receipt)
    {
        // The real repository writes state_version + 1; the aggregate keeps the version it was loaded with, so the stored copy is a new one.
        _cities[city.PlayerId] = new CityAggregate(
            city.CityId, city.PlayerId, city.State, city.ResearchCompletedAt, city.LastSettledUnixSec, checked(city.StateVersion + 1), city.ContentVersion);
        Ledger.AddRange(ledger);
        if (receipt is not null)
        {
            _receipts.Add((city.PlayerId, receipt.RequestId), receipt);
        }

        SaveCount++;
    }

    /// <inheritdoc />
    public Task<CommandReceipt?> FindAsync(Guid playerId, Guid requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_receipts.GetValueOrDefault((playerId, requestId)));

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IUnitOfWorkTransaction>(new NoTransaction());

    /// <summary>A transaction that commits nothing, because nothing is written until <see cref="Save"/>.</summary>
    private sealed class NoTransaction : IUnitOfWorkTransaction
    {
        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
