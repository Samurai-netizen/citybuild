# CityBuilder.Infrastructure

**Path:** `server/src/CityBuilder.Infrastructure/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Application`, `CityBuilder.Domain` (`CityBuilder.Simulation` comes with them; EF Core and Npgsql from M0-19) ·
**Referenced by:** `CityBuilder.Api`

> *Skeleton.* The project builds but has no types yet. The content JSON loader arrives in M0-07 and EF Core
> persistence in M0-19 *(planned)*.

## Purpose
The implementations of the Application ports: loading content JSON, EF Core persistence on PostgreSQL, row locks and
transactions (`core-instructions/03-architecture.md`, `06-data-model.md`). It must not reference the Api project or use
ASP.NET Core endpoint types.

## Key types
| Type | Responsibility |
|---|---|
| — | none yet |

## Rules and invariants
- May reference Application, Domain, Simulation, EF Core and Npgsql, never `CityBuilder.Api` (`03` dependency table).
  `InfrastructureDependencyTests` checks that the assembly has no reference to Api. The other cell of the table, "no ASP.NET endpoint
  types", is not tested yet (M0-65).
- EF Core mappings live in `Persistence/Configurations`; persistence models are not Simulation types and are mapped
  explicitly (`.claude/rules/server.md`).
- XML doc comments are optional here; the project writes its doc file only so that unnecessary usings (IDE0005)
  fail the build.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
- Database change: recipe "Database change" in `core-instructions/19-change-recipes.md` (a new migration, never an edited one).
- New port implementation: implement the Application interface here and register it in the Api composition root.

## Tests
No test project of its own. Today the only test is the dependency rule above: `InfrastructureDependencyTests` in
`server/tests/CityBuilder.IntegrationTests` (it runs in the fast suite and needs no Docker). Persistence is covered there with
real PostgreSQL *(planned, M0-21)*; the mapping round-trip tests come with M0-20.

## Known limitations
- No types yet (skeleton).

Last updated: M0-03
