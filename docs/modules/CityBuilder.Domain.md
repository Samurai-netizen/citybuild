# CityBuilder.Domain

**Path:** `server/src/CityBuilder.Domain/` · **Kind:** .NET class library (`net10.0`) ·
**References:** none yet (`CityBuilder.Simulation` from M0-03) ·
**Referenced by:** `CityBuilder.Application`, `CityBuilder.Infrastructure`, `CityBuilder.Domain.Tests`

> *Skeleton.* The project builds but has no types yet. The first ones arrive with persistence and the command
> pipeline *(planned, M0-19 on)*.

## Purpose
Server-only concepts that the shared engine doesn't have: player, ledger entry, command receipt, settlement record
(later: market order, mailbox delivery), as listed in `core-instructions/03-architecture.md`. Pure rules, no I/O.
Game rules that the client also runs belong in `CityBuilder.Simulation`, not here.

## Key types
| Type | Responsibility |
|---|---|
| — | none yet |

## Rules and invariants
- May reference only `CityBuilder.Simulation` and the BCL, never ASP.NET Core or EF Core (`03` dependency table).
  `CityBuilder.Domain.Tests` checks the assembly's references.
- Every public type and member has an XML doc comment that states its contract (`core-instructions/14-coding-standards.md`):
  the project writes its doc file, and a missing comment (CS1591) fails the build.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
Add one public type per file, named after it, with XML docs (`14`). Its database mapping goes to
`CityBuilder.Infrastructure`, never here (recipe "Database change" in `core-instructions/19-change-recipes.md`).

## Tests
`server/tests/CityBuilder.Domain.Tests` (xUnit v3, milliseconds). Today it checks the dependency rule.
Run: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- No types yet (skeleton).

Last updated: M0-02
