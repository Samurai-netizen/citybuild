# CityBuilder.Infrastructure

**Path:** `server/src/CityBuilder.Infrastructure/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Application`, `CityBuilder.Domain` (`CityBuilder.Simulation` comes with them); packages
`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 and `Microsoft.EntityFrameworkCore.Relational` 10.0.14 (EF Core 10) ·
**Referenced by:** `CityBuilder.Api`

> *Growing.* The content loader exists (M0-07b) and the EF Core model with its first migration (M0-19); the repository and mapping to the
> engine's `CityState` follow in M0-20, the locks and the write transaction in M0-22 *(planned)*.

## Purpose
The implementations of the Application ports: loading content JSON, EF Core persistence on PostgreSQL, row locks and
transactions (`core-instructions/03-architecture.md`, `06-data-model.md`). It must not reference the Api project or use
ASP.NET Core endpoint types.

## Key types
| Type | Responsibility |
|---|---|
| `Content/ContentFileLoader` | `Load(path)`: reads a content file with System.Text.Json into the engine's `GameContentData`, runs `GameContentFactory.Create` and returns the `GameContent`. Strict reader: camelCase, case-sensitive names; an unknown field, a repeated key or a number written as text is an error |
| `Content/ContentLoadException` | Every way a load fails: `FilePath`, `Errors` (one line each) and a message that lists them. A read or JSON problem is one error (with its JSON path and line); content that breaks rules gives every rule error (`ContentError.ToString()`: code, path, message) |
| `Content/ContentProvider` | `IContentProvider` over content loaded once; the Api registers it as a singleton |
| `Persistence/CityBuilderDbContext` | The EF Core context: one `DbSet` per table of `06` (`Players`, `Cities`, `CityInventory`, `CityBuildings`, `CityResearch`, `CommandReceipts`, `LedgerEntries`); applies each configuration by name |
| `Persistence/Rows/*Row` | One persistence model per table (`PlayerRow`, `CityRow`, …): plain properties, one per column, documented with the column name. Not Simulation types; M0-20 maps them to `CityState` |
| `Persistence/Configurations/*Configuration` | One `IEntityTypeConfiguration` per table: every table, column, key, unique index, index, check constraint and foreign key named explicitly in snake_case (`pk_`, `ux_`, `ix_`, `fk_`, `ck_`); `cities.state_version` is the concurrency token |
| `Persistence/PersistenceServiceCollectionExtensions` | `AddCityBuilderPersistence(services, connectionString)`: the context on Npgsql, scoped; the connection string comes from a callback, so this project reads no configuration |
| `Persistence/DatabaseMigrator` | `MigrateAsync(services, token)`: applies the pending migrations in a scope of its own and returns their ids; the Api calls it at startup in Development only |
| `Persistence/CityBuilderDbContextDesignTimeFactory` | The context for the dotnet-ef tool, with a placeholder connection string, so `migrations add` needs no settings and never starts the server |
| `Persistence/Migrations/` | The migrations dotnet-ef generated (`20261026193554_InitialCreate`) and the model snapshot. Generated code: `.editorconfig` marks it, the doc checker skips it, nobody edits it by hand |

## Rules and invariants
- May reference Application, Domain, Simulation, EF Core and Npgsql, never `CityBuilder.Api` (`03` dependency table).
  `InfrastructureDependencyTests` checks that the assembly has no reference to Api. The other cell of the table, "no ASP.NET endpoint
  types", is not tested yet (M0-65).
- The loader takes a plain path and reads no configuration; the Api resolves `Content:FilePath` (decision 2 of the M0-07b build log).
  All content checks are the engine's (`04` §14); the loader only adds the JSON layer.
- EF Core mappings live in `Persistence/Configurations`; persistence models are not Simulation types and are mapped explicitly
  (`.claude/rules/server.md`). Names are written out, not produced by a convention, so each one can be checked against `06`.
- Never edit an applied migration; add a new one (`docs/how-to/add-migration.md`). A model change without a migration fails
  `MigrationTests`.
- Every type and member, private ones included, has an XML doc comment (`core-instructions/14-coding-standards.md` → Documentation
  in code): a missing one on a public member fails the build (CS1591), and `CityBuilder.Repository.Tests` checks the rest.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
- Content change: `docs/how-to/add-building-or-item.md` (no code here).
- Database change: recipe "Database change" in `core-instructions/19-change-recipes.md` and the worked example in
  `docs/how-to/add-migration.md` (a new migration, never an edited one).
- New port implementation: implement the Application interface here and register it in the Api composition root.

## Tests
No test project of its own; its tests are in `server/tests/CityBuilder.IntegrationTests` and run in the fast suite without Docker:
`Content/ContentFileLoaderTests` (the real `mvp-content.json` loads; an unreadable file, broken JSON, an unknown or misspelt field, a repeated
key, a wrong number type, a missing value and rule errors each fail as described above; every broken file is the real one with one change)
and `InfrastructureDependencyTests` (the dependency rule). Persistence, still without a database: `Persistence/PersistenceModelTests` reads
EF's relational model and checks every table, column (type, null rule), key, unique index, index, check, foreign key, the concurrency token,
value generation and that every name is snake_case; `Persistence/MigrationTests` checks that no model change lacks a migration. Real
PostgreSQL comes with the Testcontainers tests *(planned, M0-21)*; the mapping round-trip tests come with M0-20. M0-19 applied the
migration to the compose database by hand (its build log → Verification).

## Known limitations
- A JSON problem stops the reader, so a file with a syntax error and a rule error shows only the syntax error until it is fixed.
- `city_buildings.phase` is free text: the database doesn't check that it names a phase of the engine; the mapping (M0-20) does.
- Nothing reads or writes rows yet: no repository, no transaction, no row lock *(planned, M0-20, M0-22)*.

Last updated: M0-19
