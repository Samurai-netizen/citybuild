using CityBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CityBuilder.DocTools.Pages;

/// <summary>
/// <c>db-schema.md</c>: the PostgreSQL schema as the EF Core model of <see cref="CityBuilderDbContext"/> defines it (the design-time model,
/// which also holds the check constraints): tables, columns, keys, foreign keys, indexes and check constraints, and the migrations in
/// order. Nothing connects to a database. The normative schema is <c>core-instructions/06-data-model.md</c>.
/// </summary>
public static class DbSchemaPage
{
    /// <summary>The page's file name.</summary>
    public const string FileName = "db-schema.md";

    /// <summary>Generates the page.</summary>
    /// <returns>The page.</returns>
    public static GeneratedPage Generate()
    {
        using CityBuilderDbContext context = new CityBuilderDbContextDesignTimeFactory().CreateDbContext([]);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        List<ITable> tables = [.. model.GetRelationalModel().Tables.OrderBy(table => table.Name, StringComparer.Ordinal)];
        var page = new MarkdownPage("Database schema",
            "the EF Core model of `CityBuilderDbContext` (`server/src/CityBuilder.Infrastructure/Persistence/`) and its migrations");

        page.Paragraph("Tables in alphabetical order. Each table's mapping is in `Persistence/Configurations/`; a change to it needs a new migration " +
                       "(`docs/how-to/add-migration.md`).")
            .Table(["Table", "Columns", "Primary key", "References"], tables.Select(table => (IReadOnlyList<string>)
            [
                MarkdownPage.Code(table.Name), PageFormat.Number(table.Columns.Count()), Columns(table.PrimaryKey?.Columns),
                PageFormat.Codes(table.ForeignKeyConstraints.Select(key => key.PrincipalTable.Name).Distinct().Order(StringComparer.Ordinal)),
            ]));

        foreach (ITable table in tables)
        {
            WriteTable(page, table);
        }

        page.Heading(2, "Migrations")
            .Paragraph("In the order they apply (`server/src/CityBuilder.Infrastructure/Persistence/Migrations/`).")
            .Table(["#", "Migration"], context.Database.GetMigrations().Select((migration, index) => (IReadOnlyList<string>)
                [PageFormat.Number(index + 1), MarkdownPage.Code(migration)]));
        return page.ToPage(FileName);
    }

    /// <summary>One table's section: its columns, then its keys, foreign keys, indexes and check constraints.</summary>
    /// <param name="page">The page.</param>
    /// <param name="table">The table.</param>
    private static void WriteTable(MarkdownPage page, ITable table)
    {
        page.Heading(2, table.Name)
            .Table(["Column", "Type", "Null", "Default"], table.Columns.Select(column => (IReadOnlyList<string>)
            [
                MarkdownPage.Code(column.Name), MarkdownPage.Code(column.StoreType), column.IsNullable ? "yes" : "no", Default(column),
            ]));

        var constraints = new List<IReadOnlyList<string>>();
        if (table.PrimaryKey is { } primaryKey)
        {
            constraints.Add([MarkdownPage.Code(primaryKey.Name), "primary key", Columns(primaryKey.Columns)]);
        }

        constraints.AddRange(table.UniqueConstraints.Where(unique => !unique.GetIsPrimaryKey()).OrderBy(unique => unique.Name, StringComparer.Ordinal)
            .Select(unique => (IReadOnlyList<string>)[MarkdownPage.Code(unique.Name), "unique", Columns(unique.Columns)]));
        constraints.AddRange(table.ForeignKeyConstraints.OrderBy(key => key.Name, StringComparer.Ordinal)
            .Select(key => (IReadOnlyList<string>)
            [
                MarkdownPage.Code(key.Name), "foreign key",
                $"{Columns(key.Columns)} → {MarkdownPage.Code(key.PrincipalTable.Name)} ({Columns(key.PrincipalColumns)}), on delete {MarkdownPage.Code(key.OnDeleteAction.ToString())}",
            ]));
        constraints.AddRange(table.Indexes.OrderBy(index => index.Name, StringComparer.Ordinal)
            .Select(index => (IReadOnlyList<string>)
            [
                MarkdownPage.Code(index.Name), index.IsUnique ? "unique index" : "index",
                Columns(index.Columns) + (index.Filter is { } filter ? " where " + MarkdownPage.Code(filter) : string.Empty),
            ]));
        constraints.AddRange(table.CheckConstraints.OrderBy(check => check.Name, StringComparer.Ordinal)
            .Select(check => (IReadOnlyList<string>)[MarkdownPage.Code(check.Name), "check", MarkdownPage.Code(check.Sql)]));
        page.Table(["Constraint or index", "Kind", "Definition"], constraints);
    }

    /// <summary>A column's default: its SQL, its value, or a dash.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The cell.</returns>
    private static string Default(IColumn column) =>
        column.DefaultValueSql is { } sql ? MarkdownPage.Code(sql)
        : column.DefaultValue is { } value ? MarkdownPage.Code(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))
        : "—";

    /// <summary>Column names as inline code in key order, or a dash for none.</summary>
    /// <param name="columns">The columns.</param>
    /// <returns>The cell.</returns>
    private static string Columns(IEnumerable<IColumn>? columns) => PageFormat.Codes(columns?.Select(column => column.Name) ?? []);
}
