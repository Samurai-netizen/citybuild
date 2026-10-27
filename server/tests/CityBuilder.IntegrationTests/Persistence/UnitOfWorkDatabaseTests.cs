using CityBuilder.Application.Persistence;
using CityBuilder.Application.Players;
using CityBuilder.Domain.Players;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// How the unit of work reports PostgreSQL's errors on a real database: a unique violation becomes <see cref="DuplicateKeyException"/>, so a
/// use case can recognize a lost race without knowing EF Core or Npgsql. The lock timeout and the stale version are in
/// <see cref="CityTransactionDatabaseTests"/>.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class UnitOfWorkDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>A second player with a stored player's dev name is refused by <c>unique(dev_name)</c> as a duplicate key, naming the index.</summary>
    [Fact]
    public async Task SaveChangesAsync_DevNameAlreadyStored_ThrowsDuplicateKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using (AsyncServiceScope first = Factory.Services.CreateAsyncScope())
        {
            first.ServiceProvider.GetRequiredService<IPlayerRepository>().Add(new Player(new Guid("d0b1e000-0000-4000-8000-000000000001"), "dev1"));
            await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        await using AsyncServiceScope second = Factory.Services.CreateAsyncScope();
        second.ServiceProvider.GetRequiredService<IPlayerRepository>().Add(new Player(new Guid("d0b1e000-0000-4000-8000-000000000002"), "dev1"));

        DuplicateKeyException exception = await Assert.ThrowsAsync<DuplicateKeyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));

        Assert.Contains("ux_players_dev_name", exception.Message, StringComparison.Ordinal);
        Assert.IsType<PostgresException>(exception.InnerException?.InnerException);
    }
}
