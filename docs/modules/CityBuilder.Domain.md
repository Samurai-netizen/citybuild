# CityBuilder.Domain

**Path:** `server/src/CityBuilder.Domain/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Simulation` ·
**Referenced by:** `CityBuilder.Application`, `CityBuilder.Infrastructure`, `CityBuilder.Domain.Tests`

> *Growing.* The player, the ledger entry and the command receipt exist (M0-20); the settlement record and the M2 concepts follow
> *(planned)*. Their database rows are Infrastructure's row classes (M0-19), mapped explicitly (M0-20).

## Purpose
Server-only concepts that the shared engine doesn't have: player, ledger entry, command receipt, settlement record
(later: market order, mailbox delivery), as listed in `core-instructions/03-architecture.md`. Pure rules, no I/O.
Game rules that the client also runs belong in `CityBuilder.Simulation`, not here.

## Key types
| Type | Responsibility |
|---|---|
| `Players/Player` | A player of the M0 dev login: `Id` (not empty) and `DevName` (not blank). Real accounts come in M1 |
| `Ledger/LedgerEntry` | One change of one item: `Item`, `Delta` (never 0), `BalanceAfter` (the quantities after and before are never below 0), `Reason`, and an optional reference (`RefType` and `RefId` both set or both null). The city and the wall time are added when it is stored |
| `Ledger/LedgerReason` | The M0 ledger reasons of `06` (`StartingGrant` … `CostResearch`); Infrastructure stores each as its text (`STARTING_GRANT`) |
| `Commands/CommandReceipt` | The stored answer to one successful command: `RequestId` (not empty), `CommandType`, `PayloadHash` (exactly 32 bytes, SHA-256, copied), `ResponseJson`. The player and the wall time are added when it is stored |

## Rules and invariants
- May reference only `CityBuilder.Simulation` and the BCL, never ASP.NET Core or EF Core (`03` dependency table).
  `CityBuilder.Domain.Tests` checks the assembly's references.
- Every type and member, private ones included, has an XML doc comment that states its contract (`core-instructions/14-coding-standards.md` → Documentation
  in code): a missing one on a public member fails the build (CS1591), and `CityBuilder.Repository.Tests` checks the rest.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
Add one public type per file, named after it, with XML docs (`14`). Its database mapping goes to
`CityBuilder.Infrastructure`, never here (recipe "Database change" in `core-instructions/19-change-recipes.md`).

## Tests
`server/tests/CityBuilder.Domain.Tests` (xUnit v3, milliseconds): the dependency rule and the constructor rules of each type.
Run: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- No settlement record yet; the settle use case reports through the engine's `SettlementReport` (M0-23 decides).

Last updated: M0-20
