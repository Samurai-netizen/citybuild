using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;

namespace CityBuilder.IntegrationTests.Postgres;

/// <summary>
/// The fixture itself: the container runs the stack's PostgreSQL, a test's database is a migrated copy that no other test shares, and
/// disposing it drops it.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class PostgresServerTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The run's server.</summary>
    private readonly PostgresServer _server = server;

    /// <summary>The container runs PostgreSQL 18.6, the version of the stack and of docker-compose.yml.</summary>
    [Fact]
    public async Task CreateDatabaseAsync_Container_RunsPostgreSql186()
    {
        await using CityBuilderDbContext db = Database.CreateContext();

        string version = await db.Database.SqlQueryRaw<string>("SELECT current_setting('server_version') AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.StartsWith("18.6", version, StringComparison.Ordinal);
    }

    /// <summary>A migrated test database has every committed migration applied, none pending, and no rows.</summary>
    [Fact]
    public async Task CreateDatabaseAsync_Migrated_HasEveryMigrationAndNoRows()
    {
        await using CityBuilderDbContext db = Database.CreateContext();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync(cancellationToken));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(cancellationToken));
        Assert.Equal(0, await db.Players.CountAsync(cancellationToken));
    }

    /// <summary>A row written to one test's database is not in another's.</summary>
    [Fact]
    public async Task CreateDatabaseAsync_TwoDatabases_ShareNoRows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using TestDatabase other = await _server.CreateDatabaseAsync(migrated: true);
        await using (CityBuilderDbContext db = Database.CreateContext())
        {
            db.Players.Add(new PlayerRow { Id = Guid.NewGuid(), DevName = "alice", CreatedAt = PostgresApiFactory.Start });
            await db.SaveChangesAsync(cancellationToken);
        }

        await using CityBuilderDbContext otherDb = other.CreateContext();

        Assert.Equal(0, await otherDb.Players.CountAsync(cancellationToken));
    }

    /// <summary>Disposing a test database drops it from the server.</summary>
    [Fact]
    public async Task DisposeAsync_TestDatabase_DropsIt()
    {
        TestDatabase other = await _server.CreateDatabaseAsync(migrated: false);

        await other.DisposeAsync();

        await using CityBuilderDbContext db = Database.CreateContext();
        int count = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_database WHERE datname = {other.Name}")
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }
}
