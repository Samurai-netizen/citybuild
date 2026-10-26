# CityBuilder.Application

**Path:** `server/src/CityBuilder.Application/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Domain`, `CityBuilder.Simulation` ·
**Referenced by:** `CityBuilder.Api`, `CityBuilder.Infrastructure`, `CityBuilder.Application.Tests`

> *Growing.* The ports `IContentProvider` (M0-07b), `ICityRepository`, `IPlayerRepository` and `IUnitOfWork` (M0-20) exist, and the settle
> use case `SettleCity` as a skeleton (M0-13). The transaction (M0-22), the complete settle (M0-23), the commands and the command pipeline
> follow *(planned)*.

## Purpose
The use cases (`SettleCity`, `PlaceBuilding`, `UpgradeBuilding`, `AssignWorkers`, `StartResearch`), the ports they
need (`ICityRepository`, `IUnitOfWork`, `IContentProvider`) and the command pipeline, as described in
`core-instructions/03-architecture.md`. It decides what happens; it doesn't know about HTTP or databases.

## Key types
| Type | Responsibility |
|---|---|
| `Content/IContentProvider` | The port for the game content: `GameContent Content`, loaded and validated once before the server accepts requests and the same for every request. Implemented by `ContentProvider` in Infrastructure, registered by the Api |
| `Cities/CityAggregate` | A player's city as the server keeps it: `CityId`, `PlayerId`, the engine's `State` (`CityState`), `LastSettledUnixSec` (the server second of the last settle, wall time, separate from the city clock; `06` `last_settled_unix`), `StateVersion` (as loaded), `ContentVersion`, and `ResearchCompletedAt`, the city-clock second each completed research completed (the engine keeps only the ids; `06` `city_research.completed_at`). `Advance(content, seconds)` runs `Simulator.Advance` and records the completion clock of the research that completed (its `EndsAt`); advance a city only through it. `MarkSettled` never lets the last settled second decrease |
| `Cities/ICityRepository` | The port to the stored cities: `LoadForUpdateAsync(playerId)` (every player has a city) and `Save(city, ledger, receipt)`, which only stages the city, its `LedgerEntry` rows and the optional `CommandReceipt`; a city the repository didn't load is inserted. Implemented by `CityRepository` in Infrastructure; M0-22 adds the lock and the transaction |
| `Players/IPlayerRepository` | The port to the stored players: `FindByDevNameAsync(devName)` and `Add(player)` (staged), for the dev login (M0-25). Implemented by `PlayerRepository` in Infrastructure |
| `Persistence/IUnitOfWork` | `SaveChangesAsync()` writes everything the repositories staged, together. Implemented by `UnitOfWork` in Infrastructure |
| `Cities/SettleCity`, `Cities/SettleCityResult` | `ExecuteAsync(playerId)`: loads the city, floors the `TimeProvider`'s now to whole Unix seconds, computes the `SettlementWindow` from the last settled second, advances the city (`CityAggregate.Advance`) by its elapsed seconds, records the new last settled second, stages the city and writes it through `IUnitOfWork`. The result carries the city, the `SettlementReport` and the window, whose `IsClockBehind` calls for the warning of `04` §1 |

## Rules and invariants
- May reference only `CityBuilder.Domain`, `CityBuilder.Simulation` and the BCL, never ASP.NET Core or EF Core
  (`03` dependency table). `CityBuilder.Application.Tests` checks the assembly's references.
- Use cases get content only through `IContentProvider`, never by reading a file; the content never changes while the server runs.
- Time comes only from an injected `TimeProvider`, never `DateTime.UtcNow` (`.claude/rules/server.md`). Now is floored to whole seconds
  (`ToUnixTimeSeconds`), and every settle starts where the last one ended, so the dropped fractions are counted by a later settle, never lost
  and never counted twice.
- Every type and member, private ones included, has an XML doc comment (`core-instructions/14-coding-standards.md` → Documentation
  in code): a missing one on a public member fails the build (CS1591), and `CityBuilder.Repository.Tests` checks the rest.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
- New command: recipe "New command" in `core-instructions/19-change-recipes.md`
  (`Commands/<Name>/`: command, handler, validator).
- A new port is an interface here, in a folder named after its feature (like `Content/`), implemented in `CityBuilder.Infrastructure`
  (`14`: interfaces only at layer boundaries).

## Tests
`server/tests/CityBuilder.Application.Tests` (xUnit v3, milliseconds; use cases with fakes). It checks the dependency rule, on the
assembly of `IContentProvider`, and `SettleCity` in `Cities/SettleCityTests` with `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`):
0 s, 1 s, exactly 8 h and 8 h + 1 s, 30 days (timers finish, no cycle starts after the cap), a timer completing at its second, the clock behind
and catching up, null dependencies, 100 calls 300 ms apart (= one call at 29.7 s), the completion clock of a research completed in the window, and the
save (staged without ledger or receipt, written once). `Cities/CityAggregateTests` checks the aggregate's constructor rules and `Advance`. `SettleFixture` is a small content in code (the production cap
is the M0 value, 8 h) with a city built through `CityRules`; `InMemoryCityRepository` is the fake repository and unit of work in one. `FakeTimeProvider` can't go
back in time, so a clock behind is a last settled second ahead of now. The content port's implementation and its registration are tested in
`CityBuilder.IntegrationTests` (`Content/`). Run: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- `SettleCity` is a skeleton: no closing zero-length advance, no ledger rows, no transaction and no log line for a clock behind
  *(planned, M0-23)*. `ContentVersion` keeps the loaded value until M0-23 sets it on settle. Nothing registers the use case in the Api yet.
- No commands yet *(planned, M0-26 on)*.

Last updated: M0-20
