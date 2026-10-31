# CityBuilder.Application

**Path:** `server/src/CityBuilder.Application/` · **Kind:** .NET class library (`net10.0`) ·
**References:** `CityBuilder.Domain`, `CityBuilder.Simulation` ·
**Referenced by:** `CityBuilder.Api`, `CityBuilder.Infrastructure`, `CityBuilder.Application.Tests`

> *Growing.* The ports `IContentProvider` (M0-07b), `ICityRepository`, `IPlayerRepository` and `IUnitOfWork` (M0-20) exist, the city
> transaction `ICityTransactionRunner` (M0-22), and the settle use case `SettleCity` on it (M0-13, complete with M0-23); the dev login
> `DevLogin`, `ICurrentPlayer` and `INewCityRepository` (M0-25); the command pipeline `CommandPipeline` with `ICityCommand` and the receipt
> port, and the settle's steps shared with it, `CitySettler` (M0-26); the commands `PlaceBuildingCommand` (M0-29), `UpgradeBuildingCommand`
> (M0-31), `AssignWorkersCommand` (M0-32) and `StartResearchCommand` (M0-33).

## Purpose
The use cases (`SettleCity`, `DevLogin`, `PlaceBuilding`, `UpgradeBuilding`, `AssignWorkers`, `StartResearch`), the ports they
need (`ICityRepository`, `IUnitOfWork`, `IContentProvider`, `ICommandReceiptRepository`) and the command pipeline, as described in
`core-instructions/03-architecture.md`. It decides what happens; it doesn't know about HTTP or databases.

## Key types
| Type | Responsibility |
|---|---|
| `Content/IContentProvider` | The port for the game content: `GameContent Content`, loaded and validated once before the server accepts requests and the same for every request. Implemented by `ContentProvider` in Infrastructure, registered by the Api |
| `Cities/CityAggregate` | A player's city as the server keeps it: `CityId`, `PlayerId`, the engine's `State` (`CityState`), `LastSettledUnixSec` (the server second of the last settle, wall time, separate from the city clock; `06` `last_settled_unix`), `StateVersion` (as loaded), `ContentVersion`, and `ResearchCompletedAt`, the city-clock second each completed research completed (the engine keeps only the ids; `06` `city_research.completed_at`). `Advance(content, seconds)` runs `Simulator.Advance` and records the completion clock of the research that completed (its `EndsAt`); advance a city only through it. `MarkSettled(second, contentVersion)` records the settlement's end and the content it ran with, and never lets the last settled second decrease |
| `Cities/ICityRepository` | The port to the stored cities: `LoadForUpdateAsync(playerId)` locks the city row for the rest of the transaction and loads it (a player without a city throws `CityNotFoundException`; outside a transaction, or for a second city, it throws `InvalidOperationException`) and `Save(city, ledger, receipt)`, which only stages the city, its `LedgerEntry` rows and the optional `CommandReceipt`; a city the repository didn't load is inserted. Implemented by `CityRepository` in Infrastructure. Only the runner uses it |
| `Cities/ICityTransactionRunner`, `Cities/CityTransactionRunner` | The one way a use case changes a city (`06`, write transaction pattern): `RunAsync(playerId, work)` begins the transaction (`IUnitOfWork.BeginTransactionAsync`), locks and loads the acting player's city, runs the work, and commits when it returns; any exception rolls everything back and propagates. Scoped, one per request, and it runs once: a second or nested run throws before it locks anything, so a request never locks a second city. Registered by Infrastructure's `AddCityBuilderPersistence` |
| `Cities/CityTransaction` | What the work gets: the locked `City` and `SaveAsync(ledger, receipt)`, which stages the city and writes it inside the transaction (`state_version` + 1), once; after the work ends it throws |
| `Persistence/ConcurrencyConflictException` | Another request got in the way: the lock wasn't granted within the lock timeout, or the stored `state_version` wasn't the loaded one. Nothing was committed; the API answers `CONCURRENCY_CONFLICT` (`05`; the command endpoints and the global handler map it, M0-34); the pipeline lets it propagate |
| `Persistence/CityNotFoundException` | The player named by a verified token has no city (a reset development database). Nothing was committed; the API answers `AUTHENTICATION_REQUIRED` (`05`), and the client logs in again (CR-0004) |
| `Players/IPlayerRepository` | The port to the stored players: `FindByDevNameAsync(devName)` and `Add(player)` (staged), for the dev login. Implemented by `PlayerRepository` in Infrastructure |
| `Players/ICurrentPlayer` | The acting player of the request: `PlayerId`, taken only from the verified access token (never a body); it throws when the request has no verified token. Implemented by `HttpCurrentPlayer` in the Api |
| `Players/DevLogin`, `Players/DevLoginResult` | The dev login (Development only, `05`): `IsValidDisplayName(name)` (1 to 32 ASCII letters, digits, `_`, `-`, `.`; compared exactly); `ExecuteAsync(displayName)` returns the player of the name and whether this login created them. The first login creates, in one transaction (`IUnitOfWork`), the player, the starter city (`CityFactory.CreateStarterCity`, clock 0, settled at the server's second, the content's version; ids UUID v7) through `INewCityRepository`, and one `STARTING_GRANT` ledger entry per item of the starting stock. A first login that loses a race on the name (`DuplicateKeyException`) returns the winner's player |
| `Cities/INewCityRepository` | `Add(city, startingGrant)` stages a new player's city with all its rows and its starting grant (stored at state version 1): the one city write without the row lock, because there is no row yet (ADR-0009). Implemented by `CityRepository` in Infrastructure. Only `DevLogin` uses it |
| `Persistence/DuplicateKeyException` | A unique constraint refused a write: another request committed the same key first. The loser's transaction is rolled back; the use case reads what the winner stored |
| `Persistence/IUnitOfWork`, `Persistence/IUnitOfWorkTransaction` | `SaveChangesAsync()` writes everything the repositories staged, together (a stale version or a lock timeout throws `ConcurrencyConflictException`, a unique violation `DuplicateKeyException`); `BeginTransactionAsync()` begins the request's one transaction with the configured lock timeout and returns it: `CommitAsync()` commits, disposing it uncommitted rolls back and leaves nothing staged for a later save. Implemented by `UnitOfWork` in Infrastructure |
| `Cities/SettleCity`, `Cities/SettleCityResult` | `ExecuteAsync(playerId)`: in the city's transaction (`ICityTransactionRunner`), floors the `TimeProvider`'s now (read under the lock) to whole Unix seconds, computes the `SettlementWindow` from the last settled second, advances the city (`CityAggregate.Advance`) by its elapsed seconds, records the new last settled second and the content version, and runs the closing zero-length advance (`04` §7), which restarts production after a window longer than the cap. Only if anything changed (the last settled second, the content version, the totals, or a building, compared field by field) it saves the city with the settlement's ledger rows (`CityTransaction.SaveAsync`, so `state_version` + 1); otherwise the transaction commits without a write. Before the save it checks that each item's last ledger balance is the city's stock (an engine bug would throw and commit nothing). The result carries the city, the `Settlement`, `Saved`, the `ServerTime` the settle read (as the clock gave it) and `StateVersion` (the loaded version + 1 when saved, the loaded one otherwise; the city itself keeps the version it was loaded with), which `GET /city` puts in the snapshot (M0-28); `Settlement.Window.IsClockBehind` (with `ClockBehindSeconds`) calls for the warning of `04` §1, which the Api's settlement log writes (event id 6001, M0-35) |
| `Cities/CitySettler` (internal), `Cities/LedgerWriter` (internal) | The settle's steps, shared by `SettleCity` and `CommandPipeline`, in a fixed order: `Begin(city, content, now)` records the city and advances it by the window; `RecordCommand(applied, costReason, refundReason, requestId)` takes an applied mutation's refunds and costs (once); `Close()` runs the closing zero-length advance and gives the `Settlement`; then `Changed` and `LedgerEntries()`, which first checks the stock adds up (with the command's lines) and then writes the rows in the order things happened: without a command one row per item and direction, with one the advance's rows, the command's (`ref_type` `command`, `ref_id` the request id; refunds before costs), then the closing advance's (`06`, decision 042). `LedgerWriter` keeps the running balances and leaves out a change of 0 |
| `Commands/ICityCommand` | A command: `RequestId`, `CommandType` (stored in the receipt), `CostReason`, `RefundReason`, `ValidateShape()` (null or the `INVALID_ARGUMENT` detail) and `Apply(state, content)`, the mutation through `CityRules`. It never sees the transaction or a repository. Implement it as an immutable record whose properties are its data |
| `Commands/CommandPayload` | `Canonical(command)`: the command type, `\n`, and the command's data as JSON with its properties in ordinal name order (the members of `ICityCommand` other than `RequestId` left out); `Hash(command)`: SHA-256 of its UTF-8 bytes (decision 042; a test pins the value) |
| `Commands/CommandPipeline` | `ExecuteAsync(playerId, command, writeResponse)`: the shape check (an empty request id too) before the database; the city's transaction; the receipt lookup under the lock (same hash → `Replayed` with the stored text, another → `IdempotencyConflict`, both before the settle and writing nothing); the settle (`CitySettler`); `command.Apply`; on a rejection the closing advance, a save of the settlement only if it changed, no receipt (`Rejected` with the `MutationErrorCode`); otherwise the closing advance, the response from `writeResponse` (inside the transaction) and one save of the city, its ledger and the receipt (`Succeeded`). A `DuplicateKeyException` on the save reads the stored receipt after the rollback and answers from it (none: rethrown). Scoped; registered by the Api |
| `Commands/CommandResult`, `Commands/CommandOutcome`, `Commands/CommandSuccess` | The pipeline's answer: `Outcome` (`Succeeded`, `Replayed`, `Rejected`, `InvalidArgument`, `IdempotencyConflict`) with `ResponseJson`, `ErrorCode` or `Detail`, and the `Settlement` of a success or a rejection (null for the outcomes decided before the settle), which the Api logs (M0-35). `CommandSuccess` is what the response writer gets: request id, the saved city, the settlement, the new state version, the server time |
| `Commands/ICommandReceiptRepository` | The port that reads a receipt: `FindAsync(playerId, requestId)` (the key includes the player). Implemented by `CommandReceiptRepository` in Infrastructure; receipts are written with the city (`CityTransaction.SaveAsync`) |
| `Commands/PlaceBuilding/PlaceBuildingCommand` | `place-building` (M0-29): `RequestId`, `BuildingTypeId`, `X`, `Y`; `Apply` is `CityRules.PlaceBuilding`; pays `COST_PLACE`, never refunds; `ValidateShape` refuses a type id that can't be a content id (`ContentIds`: blank, over 64 characters, other characters; M0-30), and a negative coordinate (OD-13, M0-31), so neither ever locks the city (coordinates past the grid are the rule's `OUT_OF_BOUNDS`). Its payload hash is pinned in a test |
| `Commands/UpgradeBuilding/UpgradeBuildingCommand` | `upgrade-building` (M0-31): `RequestId`, `BuildingId`; `Apply` is `CityRules.UpgradeBuilding`; pays `COST_UPGRADE` and gives a running cycle's inputs back as `REFUND_UPGRADE`; `ValidateShape` refuses an id below 1, which no city has (OD-13), so it never locks the city; an id of 1 or more the acting player's city doesn't have (ids count from 1 in each city) is the rule's `BUILDING_NOT_FOUND`. Its payload hash is pinned in a test |
| `Commands/StartResearch/StartResearchCommand` | `start-research` (M0-33): `RequestId`, `ResearchId`; `Apply` is `CityRules.StartResearch`; `COST_RESEARCH` for the cost paid up front, no refund reason; `ValidateShape` refuses an id that can't be a content id (`ContentIds`, OD-13), so it never locks the city; an id of the right form the content doesn't have is the rule's `UNKNOWN_RESEARCH`. The research completes in a later settle, which applies its effects. Its payload hash is pinned in a test |
| `Commands/AssignWorkers/AssignWorkersCommand` | `assign-workers` (M0-32): `RequestId`, `BuildingId`, `Workers` (absolute, 0 pauses); `Apply` is `CityRules.AssignWorkers`; no ledger reasons (workers are no resource); `ValidateShape` refuses an id below 1 and a negative count (OD-13), so neither ever locks the city; a count above the level's max is the rule's `WORKER_LIMIT`, more than the free workers `INSUFFICIENT_WORKERS`. Its payload hash is pinned in a test |
| `Cities/Settlement` | One settlement: the window's advance and the closing zero-length advance as one. Its `Id` (a UUID v7 per settlement), `Window`, `ClockBehindSeconds` (how far the server second was behind the last settled second; 0 unless `Window.IsClockBehind`), `ElapsedSeconds`, `ProductionSeconds`, and the totals of both advances (`Produced`, `Consumed`, `CompletedBuildings`, `CompletedResearch`, `EraChanged`; `IsEmpty`). `LedgerEntries(quantitiesBefore)` gives its ledger rows: per item in ordinal order `SETTLEMENT_PRODUCED`, then `SETTLEMENT_CONSUMED` (a negative delta), each balance running on from the quantity before; `ref_type` `settlement` (`LedgerRefType`), `ref_id` the id (`06`) |

## Rules and invariants
- May reference only `CityBuilder.Domain`, `CityBuilder.Simulation` and the BCL, never ASP.NET Core or EF Core
  (`03` dependency table). `CityBuilder.Application.Tests` checks the assembly's references.
- Use cases get content only through `IContentProvider`, never by reading a file; the content never changes while the server runs.
- A use case changes a city only through `ICityTransactionRunner`, which locks only the acting player's city, once per request (`06`,
  lock ordering). `ApplicationDependencyTests` checks that no Application type but the runner and its `CityTransaction` holds an
  `ICityRepository`, that only `DevLogin` holds `INewCityRepository` (the insert of a new city, the one write without the lock), and that
  only `SettleCity` and `CommandPipeline` hold `ICityTransactionRunner`.
- A command changes a city only through `CommandPipeline` (ADR-0004): it implements `ICityCommand` and gets the state and the content,
  nothing that writes. A successful command always saves, its receipt included; a rejection never stores a receipt.
- The acting player comes only from `ICurrentPlayer`, never from a request body (`07`).
- Time comes only from an injected `TimeProvider`, never `DateTime.UtcNow` (`.claude/rules/server.md`). Now is floored to whole seconds
  (`ToUnixTimeSeconds`), and every settle starts where the last one ended, so the dropped fractions are counted by a later settle, never lost
  and never counted twice.
- Every type and member, private ones included, has an XML doc comment (`core-instructions/14-coding-standards.md` → Documentation
  in code): a missing one on a public member fails the build (CS1591), and `CityBuilder.Repository.Tests` checks the rest.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
- New command: recipe "New command" in `core-instructions/19-change-recipes.md` (`Commands/<Name>/`): a record implementing `ICityCommand`
  whose `Apply` calls the engine rule; the endpoint passes it to `CommandPipeline` with the snapshot writer. No handler of its own.
  Worked example: `docs/how-to/add-command.md` (PlaceBuilding).
- A new port is an interface here, in a folder named after its feature (like `Content/`), implemented in `CityBuilder.Infrastructure`
  (`14`: interfaces only at layer boundaries).

## Tests
`server/tests/CityBuilder.Application.Tests` (xUnit v3, milliseconds; use cases with fakes). It checks the dependency rule, on the
assembly of `IContentProvider`, that only the runner holds `ICityRepository`, that only `DevLogin` holds `INewCityRepository`, and that
only `SettleCity` and `CommandPipeline` hold the runner. `Commands/` covers the pipeline on the fakes: `CommandPayloadTests` (the canonical
text, the hash pinned to a value computed with `shasum`, declaration order, another value or type), `CommandPipelineTests` (the order of the
steps, the receipt, what the response writer gets, the cost after the settlement's rows), `CommandPipelineIdempotencyTests` (replay, conflicts,
another player's request id, the receipt race won by the same or another payload, a duplicate key without a receipt) and
`CommandPipelineRuleTests` (shape, empty request id, a rejection commits only the settlement, a retried rejection runs again, settle before
mutation, a refund the closing advance consumes). `PipelineFixture` runs the pipeline over the fakes with two test commands
(`PlaceTestCommand`, `UpgradeTestCommand`) and a response writer that records `respond`. `PlaceBuildingCommandTests`, `UpgradeBuildingCommandTests`, `AssignWorkersCommandTests` and `StartResearchCommandTests` cover
the real commands: the shape check, the pinned payload and hash, the ledger reasons, `Apply` equal to the rule, one run through the pipeline
(the upgrade of a producing sawmill: its refund and cost rows; 0 workers on a producing sawmill: frozen, no command ledger row; a research started: its `COST_RESEARCH` row). On PostgreSQL (M0-27), `CityBuilder.IntegrationTests`
→ `Commands/` runs the server's registered pipeline with test commands of its own (`DatabaseCommands`): `CommandIdempotencyDatabaseTests` (a
retry writes nothing, also on a restarted host; conflicts; another player's request id), `CommandConcurrencyDatabaseTests` (ten identical
requests queued behind the lock apply once; the receipt race through the unique key, with the same or another payload) and
`CommandRejectionDatabaseTests` (a rejection commits only the settlement; the same request runs again). `Cities/CityTransactionRunnerTests` checks the
transaction's lifecycle on the fakes: begin, lock and load, work, write, commit in that order; a commit without writes when nothing was
saved; a rollback after an exception, a conflict or a failed load; one save per transaction, none after the work; a second or nested run
throwing before it locks anything. `SettleCity` in `Cities/SettleCityTests` with `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`):
0 s, 1 s, exactly 8 h and 8 h + 1 s, 30 days (timers finish, no cycle starts after the cap, the closing advance restarts the woodcutter), a timer
completing at its second, the clock behind and catching up, null dependencies, 100 calls 300 ms apart (= one call at 29.7 s), the completion clock of
a research completed in the window, and the save (written once, inside the transaction). `Cities/SettleCityLedgerTests`: the rows of logs made and
used in one window (produced before consumed, balances), their reference to the settlement, and the inputs the closing advance takes after the cap.
`Cities/SettleCitySaveTests`: no save with no time elapsed or at the same instant again; a save for a new content version and for a stored city that
isn't canonical; the result's next `StateVersion` and the unfloored `ServerTime` (saved), the loaded version (not saved). The expected numbers are worked out from the recipes in each test's comment. `Cities/CityAggregateTests` checks the aggregate's
constructor rules, `Advance` and `MarkSettled`. `SettleFixture` is a small content in code (the production cap is the M0 value, 8 h) with cities
built through `CityRules` (a woodcutter city, and a sawmill city whose logs are made and used in one window); `InMemoryCityRepository` is the fake repository, unit of work and receipt store in one: it records what happened in order and, like the real
one, loads only inside a transaction; a receipt is visible once written, a second one with its key fails as `DuplicateKeyException`, and
`BeforeNextWrite` lets another request commit between a lookup and a write; `SettleFixture.RunnerPerRequest` gives each call a new runner, as each request gets one. `FakeTimeProvider` can't go
back in time, so a clock behind is a last settled second ahead of now. The content port's implementation and its registration are tested in
`CityBuilder.IntegrationTests` (`Content/`). Run: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- No log lines here: Application has no logging package (`14`). Its results carry what the Api logs (the settlement, `ClockBehindSeconds`).
- The four commands of M0 (PlaceBuilding, UpgradeBuilding, AssignWorkers, StartResearch); the pipeline's own tests keep their test commands.
- `DevLogin` has no tests with fakes: what it does is all persistence (one transaction, a unique index, a race), so it is tested on
  PostgreSQL (`CityBuilder.IntegrationTests`, `Auth/DevLoginDatabaseTests`).

Last updated: CR-0004-T1
