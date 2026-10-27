using CityBuilder.Infrastructure.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Hosting;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The committed migrations on a real, empty PostgreSQL database: the server in Development migrates it at startup and the schema of
/// <c>06</c> is there afterwards; a later migration keeps the rows stored under an earlier one (<see cref="MigrationTests"/> checks the migrations against the model without a database).
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

    /// <summary>
    /// A receipt stored under <c>InitialCreate</c> (<c>response_json</c> as <c>jsonb</c>) survives <c>ReceiptResponseAsJson</c>: the column
    /// becomes <c>json</c> and keeps the response's value (in the form <c>jsonb</c> had normalized it to).
    /// </summary>
    [Fact]
    public async Task Migrate_ReceiptStoredUnderInitialCreate_KeepsItsResponse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using CityBuilderDbContext db = Database.CreateContext();
        IMigrator migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261026193554_InitialCreate", cancellationToken: cancellationToken);
        // Raw SQL reads braces as format placeholders, so the JSON's are doubled.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO players (id, dev_name, created_at) VALUES ('91a7e400-0000-4000-8000-000000000001', 'dev1', now()); " +
            "INSERT INTO command_receipts (player_id, request_id, command_type, payload_hash, response_json, created_at) VALUES " +
            "('91a7e400-0000-4000-8000-000000000001', '0b7e9c4a-0000-4000-8000-0000000000b1', 'place-building', " +
            "decode(repeat('07', 32), 'hex'), '{{\"b\": 1,\"a\":[2, 3]}}', now());",
            cancellationToken);

        await migrator.MigrateAsync(cancellationToken: cancellationToken);

        Assert.Equal(
            "json",
            await db.Database.SqlQueryRaw<string>(
                    "SELECT data_type::text AS \"Value\" FROM information_schema.columns " +
                    "WHERE table_name = 'command_receipts' AND column_name = 'response_json'")
                .SingleAsync(cancellationToken));
        Assert.Equal("{\"a\": [2, 3], \"b\": 1}", await db.CommandReceipts.Select(row => row.ResponseJson).SingleAsync(cancellationToken));
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
