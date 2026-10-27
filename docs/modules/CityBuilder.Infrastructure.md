# CityBuilder.Infrastructure

**Path:** `server/src/CityBuilder.Infrastructure/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Application`, `CityBuilder.Domain` (`CityBuilder.Simulation` comes with them); packages
`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 and `Microsoft.EntityFrameworkCore.Relational` 10.0.14 (EF Core 10) ·
**Referenced by:** `CityBuilder.Api`

> *Growing.* The content loader exists (M0-07b), the EF Core model with its first migration (M0-19), the mapping, repositories and
> unit of work (M0-20), and the row lock and the write transaction (M0-22).

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
| `Persistence/Rows/*Row` | One persistence model per table (`PlayerRow`, `CityRow`, …): plain properties, one per column, documented with the column name. Not Simulation types; `CityMapping` maps them |
| `Persistence/Mapping/CityMapping`, `CityRows` | `ToRows(city)` and `ToAggregate(rows)`: a `CityAggregate` ↔ its `cities` row and its inventory, building and research rows, explicitly and without a database. Only the engine's own state is stored (derived values never). A building's nullable columns are null exactly when its phase doesn't use them; the phase is stored as the API spells it (`Producing`); a completed research needs its completion clock. Load rejects a row that breaks the engine's or the mapping's rules with `InvalidOperationException` naming the city |
| `Persistence/Configurations/*Configuration` | One `IEntityTypeConfiguration` per table: every table, column, key, unique index, index, check constraint and foreign key named explicitly in snake_case (`pk_`, `ux_`, `ix_`, `fk_`, `ck_`); `cities.state_version` is the concurrency token |
| `Persistence/Mapping/LedgerMapping` | `ToRow` for a `LedgerEntry` (with its city) and a `CommandReceipt` (with its player), both stamped with a given wall time; `ReasonText(reason)`: the `06` text of each ledger reason (`STARTING_GRANT` …) |
| `Persistence/Repositories/CityRepository` | `ICityRepository`: `LoadForUpdateAsync` locks the city row by player with `SELECT * FROM cities WHERE player_id = … FOR UPDATE` (the statement of `06`, not composed further), then loads its child rows, tracked, and maps them; it throws outside a transaction (the lock would end with the statement) and for a second city in one context, and a lock timeout becomes `ConcurrencyConflictException`; `Save` maps the city and merges the rows into the tracked ones (changed values written, new rows inserted, a used-up item set to 0, a building or research no longer in the state deleted), inserts a city it doesn't track with all its rows, writes `state_version + 1` (the loaded version stays EF's original value, the concurrency check), and adds the ledger rows and the receipt; `created_at`/`updated_at` from one reading of `TimeProvider` |
| `Persistence/Repositories/PlayerRepository` | `IPlayerRepository`: `FindByDevNameAsync` (not tracked) and `Add` (stamps `created_at`) |
| `Persistence/Repositories/UnitOfWork` | `IUnitOfWork`: one `SaveChangesAsync` of the request's context; a stale `state_version` (EF's `DbUpdateConcurrencyException`) or a lock timeout throws `ConcurrencyConflictException` with EF's exception inside. `BeginTransactionAsync` begins the request's one transaction (read committed, PostgreSQL's default) and sets its `lock_timeout` from the settings with `set_config('lock_timeout', …, true)`, for this transaction only |
| `Persistence/LockTimeouts` | `IsLockTimeout(exception)`: whether the exception or one it wraps is PostgreSQL's `lock_not_available` (`55P03`) |
| `Persistence/PersistenceSettings` | The connection string and the lock timeout in milliseconds, built by the Api from `ConnectionStrings:CityBuilder` and `Database:LockTimeoutMilliseconds` |
| `Persistence/PersistenceServiceCollectionExtensions` | `AddCityBuilderPersistence(services, settings)`: the `PersistenceSettings` (singleton), the context on Npgsql, the three ports over it and Application's `ICityTransactionRunner`, all scoped, so a request's repositories, unit of work and runner share one context; the repositories take the `TimeProvider` the Api registers. The settings come from a callback, so this project reads no configuration |
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
value generation and that every name is snake_case; `Persistence/MigrationTests` checks that no model change lacks a migration. `Persistence/CityMappingTests` (no database) maps the cities of
`Persistence/SampleCities` (every phase, a frozen cycle, a cycle started at clock 0, completed and running research, era 2; a new empty city) to
rows and back and compares every value, checks the columns of each phase, and rejects each kind of broken row.
`Persistence/CityRepositoryTests` reads EF's change tracker after `Save` (no database), and checks that a load outside a transaction throws: a new city inserted with every row, a loaded city
(rows attached) merged row by row (city row, inventory, buildings, research), an unchanged city writing only the version and `updated_at`,
ledger rows and the receipt stamped with now. `Persistence/PlayerAndLedgerPersistenceTests`: every reason's text, a new player's row, and the
ports' registration.
On real PostgreSQL (`Category=Integration`, Docker running; `docs/testing/README.md`): `Persistence/MigrationDatabaseTests` (the committed
migrations on an empty database, once), `Persistence/CityRepositoryDatabaseTests` (through the server's registered ports, a scope per
request, loads inside a transaction: the content's starter city and the full sample city save and load equal; a save writes every table; a
loaded city's changes merge, a used-up item staying as a 0 row; a stale `state_version` throws `ConcurrencyConflictException` with EF's
exception inside), `Persistence/CityTransactionDatabaseTests` (the runner: an exception after the writes rolls back the city, the ledger and
the receipt and frees the lock; a version changed without the lock is a conflict, rolled back; two transactions on one city serialize, the
second seeing the first's commit; a lock wait beyond the timeout is a conflict caused by `55P03`; the default timeout is 3 s; a request can't
lock a second city, through the runner or the repository; one transaction per request), `Persistence/SettleCityDatabaseTests` (`SettleCity`
through the server: the starter city after 0 s, 30 s and 2 h, saved only when time passed; a 1-hour research left alone for 30 h, its
`city_research` clock and the same ledger totals as a settle at 8 h 2 min; ledger sums equal the stock deltas over four settles; repeated settles
at one instant change no row; a settle on a restarted host continues as one settle would; helpers in `Persistence/DatabaseCities`) and
`Persistence/ConstraintDatabaseTests` (the unique
tile index, `quantity >= 0` and `assigned_workers >= 0` refuse broken rows, by constraint name).

## Known limitations
- A JSON problem stops the reader, so a file with a syntax error and a rule error shows only the syntax error until it is fixed.
- `city_buildings.phase` is free text: the database doesn't check that it names a phase of the engine; `CityMapping` does on load.
- A deadlock (`40P01`) or a serialization failure isn't mapped to a conflict: with one row lock per transaction, taken first, neither can
  happen in M0. The market's lock ordering (M2, `06`) revisits it.

Last updated: M0-23
