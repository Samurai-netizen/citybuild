using CityBuilder.Infrastructure.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The committed migrations on a real, empty PostgreSQL database: the server in Development migrates it at startup and the schema of
/// <c>06</c> is there afterwards (<see cref="MigrationTests"/> checks the migrations against the model without a database).
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class MigrationDatabaseTests(PostgresServer server) : PostgresTest(server, migrated: false)
{
    /// <summary>The tables of the M0 schema (<c>06</c>) and EF's migration history, by name.</summary>
    private static readonly string[] _tables =
    [
        "__EFMigrationsHistory", "cities", "city_buildings", "city_inventory", "city_research", "command_receipts", "ledger_entries", "players",
    ];

    /// <summary>
    /// In Development with the startup migration on, the server applies every committed migration to an empty database before it serves:
    /// all of them are recorded as applied, none is pending, and the tables of <c>06</c> exist.
    /// </summary>
    [Fact]
    public async Task Start_EmptyDatabaseInDevelopment_AppliesEveryMigration()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using WebApplicationFactory<Program> migrating = Factory.WithWebHostBuilder(host => host
            .UseEnvironment(Environments.Development)
            .UseSetting("Database:ApplyMigrationsAtStartup", "true"));

        _ = migrating.Services;

        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync(cancellationToken));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(cancellationToken));
        List<string> tables = await db.Database
            .SqlQueryRaw<string>("SELECT table_name::text AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync(cancellationToken);
        Assert.Equal(_tables, tables.Order(StringComparer.Ordinal));
    }

    /// <summary>Migrating a database a second time applies nothing: the migrations run once.</summary>
    [Fact]
    public async Task MigrateAsync_MigratedDatabase_AppliesNone()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Assert.NotEmpty(await DatabaseMigrator.MigrateAsync(Factory.Services, cancellationToken));

        IReadOnlyList<string> second = await DatabaseMigrator.MigrateAsync(Factory.Services, cancellationToken);

        Assert.Empty(second);
    }
}
