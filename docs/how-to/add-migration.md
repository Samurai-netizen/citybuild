# Add a database migration

A schema change is a change to the persistence model in `server/src/CityBuilder.Infrastructure/Persistence/` plus a **new** migration that
`dotnet ef` generates from it. Recipe "Database change" in `core-instructions/19-change-recipes.md` is the checklist; this guide walks through
it with an example. The schema itself is described in `core-instructions/06-data-model.md`.

## Before you start
- A schema change needs a plan task or an approved CR. A change that breaks the contract of `06` (a removed or renamed column, a looser
  constraint) is G-Contract (`core-instructions/15-change-control.md`).
- **Never edit an applied migration** (`06` → EF Core conventions). A migration that is merged into `main` counts as applied: someone's
  database has it. Fix a mistake with a new migration.
- Restore the tool once per clone: `dotnet tool restore` (the repo pins `dotnet-ef` in `dotnet-tools.json`).

## Where things live
| What | Where |
|---|---|
| One row class per table (`PlayerRow`, `CityRow`, …), not Simulation types | `Persistence/Rows/` |
| One `IEntityTypeConfiguration` per table: names, keys, checks, indexes, foreign keys | `Persistence/Configurations/` |
| The context, which applies each configuration by name | `Persistence/CityBuilderDbContext.cs` |
| The generated migrations and the model snapshot (generated code: no doc comments, not checked, never edited) | `Persistence/Migrations/` |
| Tests of the model against `06`, and that no model change lacks a migration | `server/tests/CityBuilder.IntegrationTests/Persistence/` |

## Worked example: a `last_login_at` column on `players`
The column only shows the shape; it is not a design.

**1. The row.** Add the property to `Persistence/Rows/PlayerRow.cs`, with a doc comment that names the column:
```csharp
/// <summary><c>last_login_at</c>: when the player last logged in (UTC), or <see langword="null"/> before the first login.</summary>
public DateTimeOffset? LastLoginAt { get; set; }
```

**2. The configuration.** Every name is written out in snake_case (no naming convention does it for you). In `PlayerConfiguration.cs`:
```csharp
builder.Property(p => p.LastLoginAt).HasColumnName("last_login_at");
```
A constraint or index gets an explicit name too: `pk_<table>`, `ux_<table>_<columns>` (unique), `ix_<table>_<columns>`,
`fk_<table>_<principal>_<column>`, `ck_<table>_<column>`.

**3. The tests first.** `PersistenceModelTests` lists every column of `06`. Add `("last_login_at", "timestamp with time zone", true)` to
`players`. Run the fast suite: `MigrationTests.HasPendingModelChanges_CommittedMigrations_False` now fails, because no migration covers the
new column.

**4. The migration.** From the repo root, with a name that says what changes:
```bash
dotnet ef migrations add AddPlayerLastLoginAt \
  --project server/src/CityBuilder.Infrastructure --startup-project server/src/CityBuilder.Api \
  --output-dir Persistence/Migrations
```
It needs no database and no settings: the design-time factory (`CityBuilderDbContextDesignTimeFactory`) builds the context. Read the
generated `Up` and `Down` before you commit them. If they are wrong, `dotnet ef migrations remove` (same `--project` and `--startup-project`)
deletes the migration, as long as it was never applied anywhere else.

**5. Apply it.** With the compose database running (`docs/getting-started.md` → Database), `dotnet run --project server/src/CityBuilder.Api`
applies every pending migration at startup and logs which ones (event id 2000). That happens **only in Development**. In any other
environment `Database:ApplyMigrationsAtStartup=true` stops the start, and migrations are applied on purpose:
```bash
dotnet ef database update --project server/src/CityBuilder.Infrastructure --startup-project server/src/CityBuilder.Api \
  --connection "<connection string>"
```

**6. Docs.** Update the table in `06`, the module doc `docs/modules/CityBuilder.Infrastructure.md` if a table is new, and the build log's Docs
impact table. A column that changes what the engine stores also changes the mapping (M0-20) and its round-trip tests.

## Check it
- `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`: the model tests and `MigrationTests` pass.
- `dotnet format server/CityBuilder.slnx --verify-no-changes` and `dotnet build server/CityBuilder.slnx`: the generated files count as generated
  code (`.editorconfig`), so the analyzers and doc comment rules skip them.
- Against the compose database, list the tables and the applied migrations:
  `docker compose exec postgres psql -U citybuilder -d citybuilder -c '\dt' -c 'select * from "__EFMigrationsHistory"'`
  (use the user and database from your `.env`).

## When something goes wrong
- `HasPendingModelChanges` fails although you added a migration: the model changed again after it was generated. Remove the unapplied
  migration and generate it again.
- `dotnet ef` says it can't find the tool: `dotnet tool restore`.
- The server stops at startup with an `NpgsqlException`: the database isn't running or the connection string is wrong
  (`docs/getting-started.md` → Database).
