using System.Text.RegularExpressions;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The EF Core model of <see cref="CityBuilderDbContext"/> is the schema of core-instructions/06-data-model.md: tables, columns, types,
/// null rules, keys, unique rules, checks, indexes, foreign keys and the concurrency token. It reads EF's relational model, no database
/// and no Docker; the committed migrations are checked against this model by <see cref="MigrationTests"/>.
/// </summary>
public sealed partial class PersistenceModelTests
{
    /// <summary>Every column of every table as 06 lists it: name, PostgreSQL type and whether it may be null.</summary>
    private static readonly Dictionary<string, (string Name, string Type, bool Nullable)[]> _expectedColumns = new()
    {
        ["players"] = [("id", "uuid", false), ("dev_name", "text", false), ("created_at", "timestamp with time zone", false)],
        ["cities"] =
        [
            ("id", "uuid", false), ("player_id", "uuid", false), ("era", "smallint", false), ("clock_seconds", "bigint", false),
            ("last_settled_unix", "bigint", false), ("next_building_id", "integer", false), ("research_id", "text", true),
            ("research_started_at", "bigint", true), ("research_ends_at", "bigint", true), ("state_version", "bigint", false),
            ("content_version", "text", false), ("created_at", "timestamp with time zone", false),
            ("updated_at", "timestamp with time zone", false),
        ],
        ["city_inventory"] = [("city_id", "uuid", false), ("item_id", "text", false), ("quantity", "bigint", false)],
        ["city_buildings"] =
        [
            ("city_id", "uuid", false), ("building_id", "integer", false), ("type_id", "text", false), ("x", "smallint", false),
            ("y", "smallint", false), ("level", "smallint", false), ("assigned_workers", "smallint", false), ("phase", "text", false),
            ("phase_started_at", "bigint", true), ("phase_ends_at", "bigint", true), ("target_level", "smallint", true),
            ("cycle_labor", "bigint", true), ("labor_remaining", "bigint", true), ("segment_started_at", "bigint", true),
        ],
        ["city_research"] = [("city_id", "uuid", false), ("research_id", "text", false), ("completed_at", "bigint", false)],
        ["command_receipts"] =
        [
            ("player_id", "uuid", false), ("request_id", "uuid", false), ("command_type", "text", false), ("payload_hash", "bytea", false),
            ("response_json", "jsonb", false), ("created_at", "timestamp with time zone", false),
        ],
        ["ledger_entries"] =
        [
            ("id", "bigint", false), ("city_id", "uuid", false), ("item_id", "text", false), ("delta", "bigint", false),
            ("balance_after", "bigint", false), ("reason", "text", false), ("ref_type", "text", true), ("ref_id", "text", true),
            ("created_at", "timestamp with time zone", false),
        ],
    };

    /// <summary>The names of the tables in 06, for the theories.</summary>
    public static TheoryData<string> TableNames => [.. _expectedColumns.Keys];

    /// <summary>The model has exactly the seven tables of 06 (EF's own migrations history table is not part of the model).</summary>
    [Fact]
    public void Tables_Model_AreTheTablesOfTheDataModel()
    {
        Assert.Equal(_expectedColumns.Keys.Order(), Tables().Select(t => t.Name).Order());
    }

    /// <summary>Each table has exactly the columns of 06, with their PostgreSQL types and null rules.</summary>
    /// <param name="tableName">The table.</param>
    [Theory]
    [MemberData(nameof(TableNames))]
    public void Columns_EachTable_MatchTheDataModel(string tableName)
    {
        ITable table = Table(tableName);

        Assert.Equal(
            _expectedColumns[tableName].OrderBy(c => c.Name, StringComparer.Ordinal),
            table.Columns.Select(c => (c.Name, c.StoreType, c.IsNullable)).OrderBy(c => c.Name, StringComparer.Ordinal));
    }

    /// <summary>
    /// Wall times are <see cref="DateTimeOffset"/> on <c>timestamp with time zone</c> columns, and no property is a <see cref="DateTime"/>, whose
    /// kind could be local or unspecified: Npgsql writes a <see cref="DateTimeOffset"/> to <c>timestamptz</c> only with offset 0, so a wall time
    /// that isn't UTC fails the write instead of being stored shifted (06: wall timestamps are UTC). Every <c>bigint</c> column (simulation
    /// times, labor, quantities, the version, the ledger id) is a <see cref="long"/>, so no value is cut to 32 bits on the way.
    /// </summary>
    [Fact]
    public void ClrTypes_Model_WallTimesAreUtcOffsetsAndBigintsAreLongs()
    {
        var properties = Model().GetEntityTypes().SelectMany(e => e.GetProperties()
            .Select(p => (Name: $"{e.ClrType.Name}.{p.Name}", Type: Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType, Column: p.GetColumnType())))
            .ToList();

        Assert.DoesNotContain(properties, p => p.Type == typeof(DateTime));
        Assert.All(properties.Where(p => p.Column == "timestamp with time zone"), p => Assert.Equal(typeof(DateTimeOffset), p.Type));
        Assert.All(properties.Where(p => p.Type == typeof(DateTimeOffset)), p => Assert.Equal("timestamp with time zone", p.Column));
        Assert.All(properties.Where(p => p.Column == "bigint"), p => Assert.Equal(typeof(long), p.Type));
        Assert.All(properties.Where(p => p.Type == typeof(long)), p => Assert.Equal("bigint", p.Column));
    }

    /// <summary>Each table's primary key is the key of 06, in that column order.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="keyColumns">Its key columns, comma-separated.</param>
    [Theory]
    [InlineData("players", "id")]
    [InlineData("cities", "id")]
    [InlineData("city_inventory", "city_id,item_id")]
    [InlineData("city_buildings", "city_id,building_id")]
    [InlineData("city_research", "city_id,research_id")]
    [InlineData("command_receipts", "player_id,request_id")]
    [InlineData("ledger_entries", "id")]
    public void PrimaryKey_EachTable_IsTheKeyOfTheDataModel(string tableName, string keyColumns)
    {
        IPrimaryKeyConstraint key = Assert.IsAssignableFrom<IPrimaryKeyConstraint>(Table(tableName).PrimaryKey);

        Assert.Equal($"pk_{tableName}", key.Name);
        Assert.Equal(keyColumns.Split(','), key.Columns.Select(c => c.Name));
    }

    /// <summary>The unique rules of 06 are unique indexes, and they are the only ones.</summary>
    [Fact]
    public void UniqueIndexes_Model_AreTheUniqueRulesOfTheDataModel()
    {
        Assert.Equal(
            [
                ("cities", "ux_cities_player_id", "player_id"),
                ("city_buildings", "ux_city_buildings_city_id_x_y", "city_id,x,y"),
                ("players", "ux_players_dev_name", "dev_name"),
            ],
            Tables().SelectMany(t => t.Indexes.Where(i => i.IsUnique).Select(i => (t.Name, i.Name, Columns(i.Columns))))
                .OrderBy(i => i.Item2, StringComparer.Ordinal));
    }

    /// <summary>The only index that isn't unique is the ledger's <c>(city_id, created_at)</c>.</summary>
    [Fact]
    public void Indexes_Model_HaveTheLedgerIndexOfTheDataModel()
    {
        Assert.Equal(
            [("ledger_entries", "ix_ledger_entries_city_id_created_at", "city_id,created_at")],
            Tables().SelectMany(t => t.Indexes.Where(i => !i.IsUnique).Select(i => (t.Name, i.Name, Columns(i.Columns)))));
    }

    /// <summary>The check constraints are those of 06, each on its table.</summary>
    [Fact]
    public void CheckConstraints_Model_AreTheChecksOfTheDataModel()
    {
        Assert.Equal(
            [
                ("cities", "ck_cities_era", "era >= 1"),
                ("city_buildings", "ck_city_buildings_assigned_workers", "assigned_workers >= 0"),
                ("city_buildings", "ck_city_buildings_labor_remaining", "labor_remaining >= 0"),
                ("city_buildings", "ck_city_buildings_level", "level >= 1"),
                ("city_buildings", "ck_city_buildings_x", "x >= 0"),
                ("city_buildings", "ck_city_buildings_y", "y >= 0"),
                ("city_inventory", "ck_city_inventory_quantity", "quantity >= 0"),
            ],
            Tables().SelectMany(t => t.CheckConstraints.Select(c => (t.Name, c.Name, c.Sql)))
                .OrderBy(c => c.Item2, StringComparer.Ordinal));
    }

    /// <summary>
    /// The foreign keys of 06: a city's state rows go with the city (cascade), the city, the ledger and the receipts block the delete
    /// of what they belong to (restrict), because they are the audit trail.
    /// </summary>
    [Fact]
    public void ForeignKeys_Model_AreTheForeignKeysOfTheDataModel()
    {
        Assert.Equal(
            [
                ("cities", "fk_cities_players_player_id", "player_id", "players", ReferentialAction.Restrict),
                ("city_buildings", "fk_city_buildings_cities_city_id", "city_id", "cities", ReferentialAction.Cascade),
                ("city_inventory", "fk_city_inventory_cities_city_id", "city_id", "cities", ReferentialAction.Cascade),
                ("city_research", "fk_city_research_cities_city_id", "city_id", "cities", ReferentialAction.Cascade),
                ("command_receipts", "fk_command_receipts_players_player_id", "player_id", "players", ReferentialAction.Restrict),
                ("ledger_entries", "fk_ledger_entries_cities_city_id", "city_id", "cities", ReferentialAction.Restrict),
            ],
            Tables().SelectMany(t => t.ForeignKeyConstraints.Select(f =>
                    (t.Name, f.Name, Columns(f.Columns), f.PrincipalTable.Name, f.OnDeleteAction)))
                .OrderBy(f => f.Item2, StringComparer.Ordinal));
    }

    /// <summary><c>cities.state_version</c> is the only concurrency token (06, write transaction pattern).</summary>
    [Fact]
    public void ConcurrencyTokens_Model_OnlyCityStateVersion()
    {
        Assert.Equal(
            [(nameof(CityRow), nameof(CityRow.StateVersion))],
            Model().GetEntityTypes().SelectMany(e => e.GetProperties().Where(p => p.IsConcurrencyToken)
                .Select(p => (e.ClrType.Name, p.Name))));
    }

    /// <summary>
    /// The database numbers the ledger (identity column); every other key comes from the application or the content, never from a
    /// database default.
    /// </summary>
    [Fact]
    public void ValueGeneration_Model_OnlyLedgerIdIsAnIdentity()
    {
        IEntityType ledger = Model().FindEntityType(typeof(LedgerEntryRow))!;

        Assert.Equal(NpgsqlValueGenerationStrategy.IdentityByDefaultColumn,
            ledger.FindProperty(nameof(LedgerEntryRow.Id))!.GetValueGenerationStrategy());
        Assert.Equal(
            [(nameof(LedgerEntryRow), nameof(LedgerEntryRow.Id))],
            Model().GetEntityTypes().SelectMany(e => e.GetProperties().Where(p => p.ValueGenerated != ValueGenerated.Never)
                .Select(p => (e.ClrType.Name, p.Name))));
    }

    /// <summary>Every name in the schema (tables, columns, keys, indexes, foreign keys, checks) is snake_case (06, EF Core conventions).</summary>
    [Fact]
    public void Names_Model_AreAllSnakeCase()
    {
        IEnumerable<string> names = Tables().SelectMany(t =>
            new[] { t.Name, t.PrimaryKey!.Name }
                .Concat(t.Columns.Select(c => c.Name))
                .Concat(t.Indexes.Select(i => i.Name))
                .Concat(t.ForeignKeyConstraints.Select(f => f.Name))
                .Concat(t.CheckConstraints.Select(c => c.Name ?? "(a check without a name)")));

        Assert.All(names, name => Assert.Matches(SnakeCase(), name));
    }

    /// <summary>The tables of the relational model.</summary>
    /// <returns>Every table the context maps.</returns>
    private static IEnumerable<ITable> Tables() => Model().GetRelationalModel().Tables;

    /// <summary>One table of the relational model.</summary>
    /// <param name="name">The table's name.</param>
    /// <returns>The table; the test fails when it is missing.</returns>
    private static ITable Table(string name) => Assert.Single(Tables(), t => t.Name == name);

    /// <summary>Column names joined by commas, the form the expectations use.</summary>
    /// <param name="columns">The columns, in their order.</param>
    /// <returns>For example <c>city_id,x,y</c>.</returns>
    private static string Columns(IEnumerable<IColumn> columns) => string.Join(',', columns.Select(c => c.Name));

    /// <summary>
    /// The full design-time model (with check constraints and relational names), built from a context that never opens a connection.
    /// </summary>
    /// <returns>The model.</returns>
    private static IModel Model()
    {
        using CityBuilderDbContext context = PersistenceTestContext.Create();
        return context.GetService<IDesignTimeModel>().Model;
    }

    /// <summary>Lower-case words joined by single underscores.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$")]
    private static partial Regex SnakeCase();
}
