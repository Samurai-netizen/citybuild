# CityBuilder.Application

**Path:** `server/src/CityBuilder.Application/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Domain`, `CityBuilder.Simulation` ·
**Referenced by:** `CityBuilder.Api`, `CityBuilder.Infrastructure`, `CityBuilder.Application.Tests`

> *Growing.* The ports `IContentProvider` (M0-07b), `ICityRepository`, `IPlayerRepository` and `IUnitOfWork` (M0-20) exist, the city
> transaction `ICityTransactionRunner` (M0-22), and the settle use case `SettleCity` on it (M0-13, complete with M0-23); the dev login
> `DevLogin`, `ICurrentPlayer` and `INewCityRepository` (M0-25). The commands and the command pipeline follow *(planned, M0-26 on)*.

## Purpose
The use cases (`SettleCity`, `DevLogin`, `PlaceBuilding`, `UpgradeBuilding`, `AssignWorkers`, `StartResearch`), the ports they
need (`ICityRepository`, `IUnitOfWork`, `IContentProvider`) and the command pipeline, as described in
`core-instructions/03-architecture.md`. It decides what happens; it doesn't know about HTTP or databases.

## Key types
| Type | Responsibility |
|---|---|
| `Content/IContentProvider` | The port for the game content: `GameContent Content`, loaded and validated once before the server accepts requests and the same for every request. Implemented by `ContentProvider` in Infrastructure, registered by the Api |
| `Cities/CityAggregate` | A player's city as the server keeps it: `CityId`, `PlayerId`, the engine's `State` (`CityState`), `LastSettledUnixSec` (the server second of the last settle, wall time, separate from the city clock; `06` `last_settled_unix`), `StateVersion` (as loaded), `ContentVersion`, and `ResearchCompletedAt`, the city-clock second each completed research completed (the engine keeps only the ids; `06` `city_research.completed_at`). `Advance(content, seconds)` runs `Simulator.Advance` and records the completion clock of the research that completed (its `EndsAt`); advance a city only through it. `MarkSettled(second, contentVersion)` records the settlement's end and the content it ran with, and never lets the last settled second decrease |
| `Cities/ICityRepository` | The port to the stored cities: `LoadForUpdateAsync(playerId)` locks the city row for the rest of the transaction and loads it (every player has a city; outside a transaction, or for a second city, it throws) and `Save(city, ledger, receipt)`, which only stages the city, its `LedgerEntry` rows and the optional `CommandReceipt`; a city the repository didn't load is inserted. Implemented by `CityRepository` in Infrastructure. Only the runner uses it |
| `Cities/ICityTransactionRunner`, `Cities/CityTransactionRunner` | The one way a use case changes a city (`06`, write transaction pattern): `RunAsync(playerId, work)` begins the transaction (`IUnitOfWork.BeginTransactionAsync`), locks and loads the acting player's city, runs the work, and commits when it returns; any exception rolls everything back and propagates. Scoped, one per request, and it runs once: a second or nested run throws before it locks anything, so a request never locks a second city. Registered by Infrastructure's `AddCityBuilderPersistence` |
| `Cities/CityTransaction` | What the work gets: the locked `City` and `SaveAsync(ledger, receipt)`, which stages the city and writes it inside the transaction (`state_version` + 1), once; after the work ends it throws |
| `Persistence/ConcurrencyConflictException` | Another request got in the way: the lock wasn't granted within the lock timeout, or the stored `state_version` wasn't the loaded one. Nothing was committed; the API answers `CONCURRENCY_CONFLICT` (`05`; the mapping comes with the command pipeline, M0-26, and the catalogue, M0-34) |
| `Players/IPlayerRepository` | The port to the stored players: `FindByDevNameAsync(devName)` and `Add(player)` (staged), for the dev login. Implemented by `PlayerRepository` in Infrastructure |
| `Players/ICurrentPlayer` | The acting player of the request: `PlayerId`, taken only from the verified access token (never a body); it throws when the request has no verified token. Implemented by `HttpCurrentPlayer` in the Api |
| `Players/DevLogin`, `Players/DevLoginResult` | The dev login (Development only, `05`): `IsValidDisplayName(name)` (1 to 32 ASCII letters, digits, `_`, `-`, `.`; compared exactly); `ExecuteAsync(displayName)` returns the player of the name and whether this login created them. The first login creates, in one transaction (`IUnitOfWork`), the player, the starter city (`CityFactory.CreateStarterCity`, clock 0, settled at the server's second, the content's version; ids UUID v7) through `INewCityRepository`, and one `STARTING_GRANT` ledger entry per item of the starting stock. A first login that loses a race on the name (`DuplicateKeyException`) returns the winner's player |
| `Cities/INewCityRepository` | `Add(city, startingGrant)` stages a new player's city with all its rows and its starting grant (stored at state version 1): the one city write without the row lock, because there is no row yet (ADR-0009). Implemented by `CityRepository` in Infrastructure. Only `DevLogin` uses it |
| `Persistence/DuplicateKeyException` | A unique constraint refused a write: another request committed the same key first. The loser's transaction is rolled back; the use case reads what the winner stored |
| `Persistence/IUnitOfWork`, `Persistence/IUnitOfWorkTransaction` | `SaveChangesAsync()` writes everything the repositories staged, together (a stale version or a lock timeout throws `ConcurrencyConflictException`, a unique violation `DuplicateKeyException`); `BeginTransactionAsync()` begins the request's one transaction with the configured lock timeout and returns it: `CommitAsync()` commits, disposing it uncommitted rolls back and leaves nothing staged for a later save. Implemented by `UnitOfWork` in Infrastructure |
| `Cities/SettleCity`, `Cities/SettleCityResult` | `ExecuteAsync(playerId)`: in the city's transaction (`ICityTransactionRunner`), floors the `TimeProvider`'s now (read under the lock) to whole Unix seconds, computes the `SettlementWindow` from the last settled second, advances the city (`CityAggregate.Advance`) by its elapsed seconds, records the new last settled second and the content version, and runs the closing zero-length advance (`04` §7), which restarts production after a window longer than the cap. Only if anything changed (the last settled second, the content version, the totals, or a building, compared field by field) it saves the city with the settlement's ledger rows (`CityTransaction.SaveAsync`, so `state_version` + 1); otherwise the transaction commits without a write. Before the save it checks that each item's last ledger balance is the city's stock (an engine bug would throw and commit nothing). The result carries the city, the `Settlement` and `Saved`; `Settlement.Window.IsClockBehind` calls for the warning of `04` §1, which the endpoint logs (M0-28) |
| `Cities/Settlement` | One settlement: the window's advance and the closing zero-length advance as one. Its `Id` (a UUID v7 per settlement), `Window`, `ElapsedSeconds`, `ProductionSeconds`, and the totals of both advances (`Produced`, `Consumed`, `CompletedBuildings`, `CompletedResearch`, `EraChanged`; `IsEmpty`). `LedgerEntries(quantitiesBefore)` gives its ledger rows: per item in ordinal order `SETTLEMENT_PRODUCED`, then `SETTLEMENT_CONSUMED` (a negative delta), each balance running on from the quantity before; `ref_type` `settlement` (`LedgerRefType`), `ref_id` the id (`06`) |

## Rules and invariants
- May reference only `CityBuilder.Domain`, `CityBuilder.Simulation` and the BCL, never ASP.NET Core or EF Core
  (`03` dependency table). `CityBuilder.Application.Tests` checks the assembly's references.
- Use cases get content only through `IContentProvider`, never by reading a file; the content never changes while the server runs.
- A use case changes a city only through `ICityTransactionRunner`, which locks only the acting player's city, once per request (`06`,
  lock ordering). `ApplicationDependencyTests` checks that no Application type but the runner and its `CityTransaction` holds an
  `ICityRepository`, and that only `DevLogin` holds `INewCityRepository` (the insert of a new city, the one write without the lock).
- The acting player comes only from `ICurrentPlayer`, never from a request body (`07`).
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
assembly of `IContentProvider`, that only the runner holds `ICityRepository`, and that only `DevLogin` holds `INewCityRepository`. `Cities/CityTransactionRunnerTests` checks the
transaction's lifecycle on the fakes: begin, lock and load, work, write, commit in that order; a commit without writes when nothing was
saved; a rollback after an exception, a conflict or a failed load; one save per transaction, none after the work; a second or nested run
throwing before it locks anything. `SettleCity` in `Cities/SettleCityTests` with `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`):
0 s, 1 s, exactly 8 h and 8 h + 1 s, 30 days (timers finish, no cycle starts after the cap, the closing advance restarts the woodcutter), a timer
completing at its second, the clock behind and catching up, null dependencies, 100 calls 300 ms apart (= one call at 29.7 s), the completion clock of
a research completed in the window, and the save (written once, inside the transaction). `Cities/SettleCityLedgerTests`: the rows of logs made and
used in one window (produced before consumed, balances), their reference to the settlement, and the inputs the closing advance takes after the cap.
`Cities/SettleCitySaveTests`: no save with no time elapsed or at the same instant again; a save for a new content version and for a stored city that
isn't canonical. The expected numbers are worked out from the recipes in each test's comment. `Cities/CityAggregateTests` checks the aggregate's
constructor rules, `Advance` and `MarkSettled`. `SettleFixture` is a small content in code (the production cap is the M0 value, 8 h) with cities
built through `CityRules` (a woodcutter city, and a sawmill city whose logs are made and used in one window); `InMemoryCityRepository` is the fake repository and unit of work in one: it records what happened in order and, like the real one, loads only
inside a transaction; `SettleFixture.RunnerPerRequest` gives each call a new runner, as each request gets one. `FakeTimeProvider` can't go
back in time, so a clock behind is a last settled second ahead of now. The content port's implementation and its registration are tested in
`CityBuilder.IntegrationTests` (`Content/`). Run: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- No log line for a clock behind here: Application has no logging package (`14`); the endpoint that serves `GET /city` logs it *(planned, M0-28)*.
  The Api registers `SettleCity`, but no endpoint calls it yet *(planned, M0-28)*.
- No commands yet *(planned, M0-26 on)*. Nothing maps `ConcurrencyConflictException` to `CONCURRENCY_CONFLICT` yet *(planned, M0-26)*.
- `DevLogin` has no tests with fakes: what it does is all persistence (one transaction, a unique index, a race), so it is tested on
  PostgreSQL (`CityBuilder.IntegrationTests`, `Auth/DevLoginDatabaseTests`).

Last updated: M0-25
