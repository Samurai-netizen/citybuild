# CityBuilder.Application

**Path:** `server/src/CityBuilder.Application/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Domain` (`CityBuilder.Simulation` from M0-03) ·
**Referenced by:** `CityBuilder.Api`, `CityBuilder.Infrastructure`, `CityBuilder.Application.Tests`

> *Skeleton.* The project builds but has no types yet. The first port, `IContentProvider`, arrives in M0-07
> *(planned)*; the use cases and the command pipeline follow from M0-20.

## Purpose
The use cases (`SettleCity`, `PlaceBuilding`, `UpgradeBuilding`, `AssignWorkers`, `StartResearch`), the ports they
need (`ICityRepository`, `IUnitOfWork`, `IContentProvider`) and the command pipeline, as described in
`core-instructions/03-architecture.md`. It decides what happens; it doesn't know about HTTP or databases.

## Key types
| Type | Responsibility |
|---|---|
| — | none yet |

## Rules and invariants
- May reference only `CityBuilder.Domain`, `CityBuilder.Simulation` and the BCL, never ASP.NET Core or EF Core
  (`03` dependency table). `CityBuilder.Application.Tests` checks the assembly's references.
- Time comes only from an injected `TimeProvider`, never `DateTime.UtcNow` (`.claude/rules/server.md`).
- Every public type and member has an XML doc comment (`core-instructions/14-coding-standards.md`): the project writes
  its doc file, and a missing comment (CS1591) fails the build.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
- New command: recipe "New command" in `core-instructions/19-change-recipes.md`
  (`Commands/<Name>/`: command, handler, validator).
- A new port is an interface here, implemented in `CityBuilder.Infrastructure` (`14`: interfaces only at layer boundaries).

## Tests
`server/tests/CityBuilder.Application.Tests` (xUnit v3, milliseconds; use cases with fakes from M0-20 on). Today it
checks the dependency rule. Run: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- No types yet (skeleton).

Last updated: M0-02
