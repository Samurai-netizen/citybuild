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
        "__EFMigrationsHistory", "cities", "city_buildings", "city_courier_loads", "city_couriers", "city_project_needs", "city_research", "city_research_needs", "city_roads",
        "city_route_actions", "city_stores", "city_trips", "command_receipts", "ledger_entries", "players",
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

        // Up to ReceiptResponseAsJson only: the later reset migration (CR-0008 D10) deletes every receipt.
        await migrator.MigrateAsync("20261027092603_ReceiptResponseAsJson", cancellationToken: cancellationToken);

        Assert.Equal(
            "json",
            await db.Database.SqlQueryRaw<string>(
                    "SELECT data_type::text AS \"Value\" FROM information_schema.columns " +
                    "WHERE table_name = 'command_receipts' AND column_name = 'response_json'")
                .SingleAsync(cancellationToken));
        Assert.Equal("{\"a\": [2, 3], \"b\": 1}", await db.CommandReceipts.Select(row => row.ResponseJson).SingleAsync(cancellationToken));
    }

    /// <summary>
    /// The logistics migration resets the dev database (CR-0008 D10, the owner's OK): a player with a city, a receipt and ledger rows stored
    /// under <c>AddCityRoads</c> are all gone after <c>LogisticsStoresTripsAndReset</c>, so the dev name logs in to a new starting city.
    /// </summary>
    [Fact]
    public async Task Migrate_LogisticsMigration_DeletesEveryPlayerAndTheirRows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using CityBuilderDbContext db = Database.CreateContext();
        IMigrator migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103215115_AddCityRoads", cancellationToken: cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO players (id, dev_name, created_at) VALUES ('91a7e400-0000-4000-8000-000000000001', 'dev1', now()); " +
            "INSERT INTO cities (id, player_id, era, clock_seconds, last_settled_unix, next_building_id, state_version, content_version, " +
            "created_at, updated_at) VALUES ('c17e0000-0000-4000-8000-000000000001', '91a7e400-0000-4000-8000-000000000001', 1, 60, 0, 2, 3, " +
            "'mvp-1', now(), now()); " +
            "INSERT INTO city_inventory (city_id, item_id, quantity) VALUES ('c17e0000-0000-4000-8000-000000000001', 'coins', 500); " +
            "INSERT INTO city_roads (city_id, x, y, level) VALUES ('c17e0000-0000-4000-8000-000000000001', 1, 1, 1); " +
            "INSERT INTO ledger_entries (city_id, item_id, delta, balance_after, reason, created_at) VALUES " +
            "('c17e0000-0000-4000-8000-000000000001', 'coins', 500, 500, 'STARTING_GRANT', now()); " +
            "INSERT INTO command_receipts (player_id, request_id, command_type, payload_hash, response_json, created_at) VALUES " +
            "('91a7e400-0000-4000-8000-000000000001', '0b7e9c4a-0000-4000-8000-0000000000b1', 'place-building', " +
            "decode(repeat('07', 32), 'hex'), '{{}}', now());",
            cancellationToken);

        await migrator.MigrateAsync(cancellationToken: cancellationToken);

        Assert.Equal(0, await db.Players.CountAsync(cancellationToken));
        Assert.Equal(0, await db.Cities.CountAsync(cancellationToken));
        Assert.Equal(0, await db.CityRoads.CountAsync(cancellationToken));
        Assert.Equal(0, await db.LedgerEntries.CountAsync(cancellationToken));
        Assert.Equal(0, await db.CommandReceipts.CountAsync(cancellationToken));
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
