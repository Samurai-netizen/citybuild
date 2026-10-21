# CityBuilder.Infrastructure

**Path:** `server/src/CityBuilder.Infrastructure/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Application`, `CityBuilder.Domain` (`CityBuilder.Simulation` comes with them; EF Core and Npgsql from M0-19) ·
**Referenced by:** `CityBuilder.Api`

> *Growing.* The content loader exists (M0-07b); EF Core persistence follows in M0-19 *(planned)*.

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

## Rules and invariants
- May reference Application, Domain, Simulation, EF Core and Npgsql, never `CityBuilder.Api` (`03` dependency table).
  `InfrastructureDependencyTests` checks that the assembly has no reference to Api. The other cell of the table, "no ASP.NET endpoint
  types", is not tested yet (M0-65).
- The loader takes a plain path and reads no configuration; the Api resolves `Content:FilePath` (decision 2 of the M0-07b build log).
  All content checks are the engine's (`04` §14); the loader only adds the JSON layer.
- EF Core mappings will live in `Persistence/Configurations` *(from M0-19)*; persistence models are not Simulation types and are mapped
  explicitly (`.claude/rules/server.md`).
- XML doc comments are optional here; the project writes its doc file only so that unnecessary usings (IDE0005)
  fail the build.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
- Content change: `docs/how-to/add-building-or-item.md` (no code here).
- Database change: recipe "Database change" in `core-instructions/19-change-recipes.md` (a new migration, never an edited one).
- New port implementation: implement the Application interface here and register it in the Api composition root.

## Tests
No test project of its own; its tests are in `server/tests/CityBuilder.IntegrationTests` and run in the fast suite without Docker:
`Content/ContentFileLoaderTests` (the real `mvp-content.json` loads; an unreadable file, broken JSON, an unknown or misspelt field, a repeated
key, a wrong number type, a missing value and rule errors each fail as described above; every broken file is the real one with one change)
and `InfrastructureDependencyTests` (the dependency rule). Persistence is covered there with real PostgreSQL *(planned, M0-21)*; the mapping
round-trip tests come with M0-20.

## Known limitations
- A JSON problem stops the reader, so a file with a syntax error and a rule error shows only the syntax error until it is fixed.

Last updated: CR-0002-T1
