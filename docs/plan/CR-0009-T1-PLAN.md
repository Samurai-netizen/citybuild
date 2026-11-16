# CR-0009-T1 — Player metrics: recording · implementation plan

> **For the coder who implements this later (written for Claude Opus 5.5, `[O]`).** You have no memory of the planning session; this file
> and the pages it names are everything. Spec: `bash scripts/dev/show-task.sh CR-0009-T1` (the CR: `docs/changes/CR-0009-player-metrics.md`).
> Planned 2026-11-16 against `main` at `a99cf02` (after M0-66). No code of this task exists yet.
> This file is a plan, not a rule: where the code has moved since, follow the code and log the difference as a decision.

## 0. Before you start
1. **Owner decisions.** Section 2 lists four points where this plan differs from the approved CR. The task is `[PLAN]` (G-Plan): show the
   owner this file, get a yes or no on each point, record the words in the build log (Owner input) and in the CR. **No code before that.**
2. **Start:** `/start-task CR-0009-T1`. The build log entry **already exists**: `docs/build-log/2026/2026-11-16-01-CR-0009-T1.md`
   (status Partial). Do **not** run `new-log-entry.sh` again; append "Session 2", set the status to In progress, and reuse its Decisions
   (1 to 5) and its Docs impact table. Decision 1 there (the branch name) belonged to the planning session's tool; use
   `task/CR-0009-T1-recording` unless your session has the same limit.
3. **Read first:** `02`, `15`, `16`, `14` (Documentation in code: every declaration gets a doc comment, OD-12), `06`, `07` → Logging
   policy, `08`, `19` → Database change and New configuration key, `docs/how-to/add-migration.md`, `docs/how-to/add-command.md`.
4. **Baseline:** `dotnet build server/CityBuilder.slnx` and the fast suite were green at planning time (2575 tests). The database tests
   need Docker (`docs/testing/README.md`); if your machine has none, say so in the report (rule 26) and let CI run them.

## 1. What this task builds (and what it does not)
The server writes one row to a new table `player_events` for each thing a player did or reached. Nothing reads the table in this task;
the report is CR-0009-T2. **No file under `client/`, no endpoint, no response field, no header changes** (owner: "Client should not
know this thing exists"). The engine in `shared/Simulation/` does not change. Events never influence the game.

Events of this task:

| `event_type` | `detail` | `outcome` | `value` | Written when |
|---|---|---|---|---|
| `command.<commandType>` (e.g. `command.place-building`) | from the command, see step 4 | `accepted`, or the rule's error code | null | a command passed the receipt check and was applied or refused by a game rule |
| `research.completed` | the research id | null | null | a settlement completed it |
| `building.completed` | the building's type id | null | the level it now has | a settlement completed a construction or an upgrade |
| `session.start` | null | null | null | a settling request arrives more than the session gap after the last seen second; and at the first login |
| `player.created` | null | null | null | the first login creates the player |

Every row also has `player_id`, `city_clock` (the city clock when it was recorded) and `created_at` (server time).

## 2. Points that differ from the CR (the owner decides each; the plan assumes "yes")
| # | Point | If the owner says no |
|---|---|---|
| A | **No `production.first` event.** `ledger_entries` already has a `SETTLEMENT_PRODUCED` row per city, item and settlement; T2 reads the first one. An event would cost a lookup in every producing settlement. Changes one acceptance criterion of the CR | add a step: a partial unique index on `(player_id, detail) WHERE event_type = 'production.first'` and an insert with `ON CONFLICT DO NOTHING` per produced item |
| B | **A session gap of its own:** `Metrics:SessionGapSeconds`, default 1800, not the presence grace (300 s, a gameplay number for courier stamina) | use `PresencePolicy.GraceSeconds` and drop the options class of step 6 |
| C | **A shape refusal is not recorded** (`INVALID_ARGUMENT` before the lock): OD-13 says such a request stores nothing, and tests pin it. Neither are a replay, an `IDEMPOTENCY_CONFLICT`, a `CONCURRENCY_CONFLICT` or a rate-limit refusal | stop and ask: recording them contradicts OD-13 |
| D | **A column `value bigint null`** next to the CR's columns: the level of `building.completed`, so `detail` stays a plain content id that a checkpoint can match | put the level into `detail` as `<type>:<level>` |

After the answers, edit the CR's events table, schema line and acceptance criteria to match (it is `In progress`, not merged history).

## 3. Design (the parts that need care)
- **One transaction.** An event is inserted in the city's transaction and committed with it. A rollback drops it. (`02` rules 9, 10.)
- **Staging.** `CityTransaction.Record(PlayerEvent)` adds to a list. The rows reach the database in one of two ways:
  1. `CityTransaction.SaveAsync` hands the list to `IPlayerEventRepository.Add` before `_unitOfWork.SaveChangesAsync`;
  2. if the work returned without `SaveAsync` and the list is not empty, `CityTransactionRunner.RunAsync` calls an internal
     `CityTransaction.FlushEventsAsync` (repository `Add`, then `SaveChangesAsync`) **after the work and before the commit**.
  Case 2 exists for a command refused at an instant where nothing else changed (`settler.Changed` is false). It writes event rows only:
  `CityRepository.Save` was not called, so no city row is touched and `state_version` stays. Confirm this with a PostgreSQL test.
- **Who creates events.** A small static class `SettlementEvents` (Application, `Metrics/`) turns a closed `Settlement` into events;
  `SettleCity` and `CommandPipeline` both call it, so `GET /city` and commands record the same facts. The pipeline adds the command event.
- **No reads.** `IPlayerEventRepository` has `Add` only. Application and the engine never read events (a test checks it).
- **Privacy.** A row holds the player's uuid and content ids. Never a display name, token, address or request text. `detail` only ever
  holds an id that exists in the loaded content (step 4), so client-typed text cannot reach the table.
- **Naming.** Check `core-instructions/31-glossary.md` before naming; "event" here is a *player event*. Do not reuse the name of
  `InMemoryCityRepository.Events` (a test call log) for the new fake's list: call that one `PlayerEvents`.

## 4. Steps (each ends green, with its docs, and is checkpointed: `/checkpoint`)

### Step 1 — the domain type and the port
- New `server/src/CityBuilder.Domain/Metrics/PlayerEvent.cs`: an immutable class or record with `PlayerId` (not `Guid.Empty`), `EventType`
  (not blank, at most 64 characters, lower-case letters, digits, `.`, `-`, `_`), `Detail` (null, or not blank and at most 64), `Outcome`
  (null, or not blank and at most 64), `Value` (`long?`), `CityClock` (0 or more), `OccurredAt` (`DateTimeOffset`). The constructor
  throws `ArgumentException` like `Domain/Ledger/LedgerEntry.cs` does.
- New `server/src/CityBuilder.Domain/Metrics/PlayerEventTypes.cs`: constants `CommandPrefix = "command."`, `ResearchCompleted`,
  `BuildingCompleted`, `SessionStart`, `PlayerCreated`, `OutcomeAccepted = "accepted"`, and `ForCommand(string commandType)`.
- New `server/src/CityBuilder.Application/Metrics/IPlayerEventRepository.cs`: `void Add(IReadOnlyList<PlayerEvent> events);`
  (stages rows; the unit of work writes them).
- Tests: `server/tests/CityBuilder.Domain.Tests/Metrics/PlayerEventTests.cs` (each refusal, the limits, a valid one).
- Docs: `docs/modules/CityBuilder.Domain.md`, `CityBuilder.Application.md`.

### Step 2 — the table
- New `Infrastructure/Persistence/Rows/PlayerEventRow.cs` and `Configurations/PlayerEventConfiguration.cs`, written like
  `LedgerEntryRow` and `LedgerEntryConfiguration`: table `player_events`; `id` bigint identity (`pk_player_events`); `player_id`;
  `event_type`; `detail` null; `outcome` null; `value` null; `city_clock`; `created_at`; indexes
  `ix_player_events_player_id_created_at` and `ix_player_events_event_type_detail`; check `ck_player_events_city_clock`
  (`city_clock >= 0`); FK `fk_player_events_players_player_id` with `DeleteBehavior.Cascade` (analytics, not audit: unlike the ledger).
- `CityBuilderDbContext`: `DbSet<PlayerEventRow> PlayerEvents`.
- New `Repositories/PlayerEventRepository.cs` (`Add` maps and `AddRange`s; `created_at` = the event's `OccurredAt`); register it in
  `PersistenceServiceCollectionExtensions` as scoped.
- Migration: `dotnet tool restore`, then the `dotnet ef migrations add AddPlayerEvents …` line of `AGENTS.md`. It must only create the
  table, its indexes and its constraints. Never edit an older migration.
- Tests (`server/tests/CityBuilder.IntegrationTests/Persistence/`, look at `PlayerAndLedgerPersistenceTests`, `MigrationDatabaseTests`,
  `ConstraintDatabaseTests`, `PersistenceModelTests` for the patterns; database tests carry `[Trait("Category", "Integration")]`):
  round-trip of a row with and without the nullable columns; migration from empty and from `AddBuildingPaused`; deleting a player
  deletes their events; `city_clock < 0` is refused; **`PlayerEvents_Columns_AreExactlyThese`** pins the column list (the privacy guard).
- Docs: `06` (table row, the "Built so far" line, foreign keys paragraph), regenerate `docs/reference/generated/db-schema.md`
  (`dotnet run --project tools/DocTools -- generate`), `docs/modules/CityBuilder.Infrastructure.md`.

### Step 3 — recording in the city's transaction
- `CityTransaction`: constructor takes `IPlayerEventRepository`; `public void Record(PlayerEvent playerEvent)` (throws after `Close()`
  and when the event's player is not the city's); `SaveAsync` stages pending events before saving; `internal Task FlushEventsAsync(...)`.
- `CityTransactionRunner`: constructor takes `IPlayerEventRepository`; after `await work(...)` and before `CommitAsync`, flush.
- Test fakes: `InMemoryCityRepository` (Application.Tests) also implements `IPlayerEventRepository` with a `PlayerEvents` list that
  only keeps what was staged **and** followed by `SaveChangesAsync`; update `SettleFixture.RunnerPerRequest` and every
  `new CityTransactionRunner(` in the tests (`grep -rn "new CityTransactionRunner(" server/tests`).
- Tests: `CityTransactionRunnerTests` (events with a save; events without a save are flushed once; an exception in the work stores
  none; `Record` after the work throws; another player's event throws). PostgreSQL, in `CityTransactionDatabaseTests`:
  **`Events_WithoutACitySave_AreStoredAndStateVersionStays`**, `Events_WhenTheWorkThrows_AreNotStored`.
- Docs: `06` → Write transaction pattern (the "Nothing changed" and "Business-rule rejection" bullets and the SQL sketch),
  `03` → Request lifecycle, module doc Application.

### Step 4 — command events
- `ICityCommand`: add `string? EventDetail(CityState city, GameContent content);` with a doc comment saying: the content id this
  command is about, for the metrics; only an id that exists in `content`; `null` when it has none. A member declared on the interface is
  not part of the payload hash (`CommandPayload`); confirm with `CommandPayloadTests`.
- Implement it in every command (`server/src/CityBuilder.Application/Commands/*/`):

  | Command | `EventDetail` |
  |---|---|
  | `PlaceBuildingCommand` | `BuildingTypeId` if the content has that building type, else null |
  | `UpgradeBuildingCommand`, `AssignWorkersCommand`, `SetPausedCommand` | the type id of the city's building with `BuildingId`, else null |
  | `StartResearchCommand` | `ResearchId` if the content has it, else null |
  | `StartTripCommand` | `ItemId` if the content has it, else null |
  | `SetRouteCommand`, `CancelDeliveryCommand`, `ReenergizeCouriersCommand`, the three road commands | null |

  Also the test commands `PlaceTestCommand` and `UpgradeTestCommand`.
- `CommandPipeline.RunAsync`: read `string? detail = command.EventDetail(city.State, content)` **after `CitySettler.Begin` and before
  `command.Apply`** (the settled state, before the mutation). On a rule error: `transaction.Record(command event with outcome =
  errorCode)` **before** the `if (settler.Changed)` save. On success: record with `accepted` before `transaction.SaveAsync`. The replay
  and conflict path (`Answer`) and the shape checks in `ExecuteAsync` record nothing. Use the error code's API spelling, the same text
  `CommandLog.Rejected` logs (look at how `CommandEndpoints` turns `CommandResult.Rejected` into the `code`).
- Tests (Application.Tests/Commands): accepted → one event with the type, detail and `accepted`; each rule error → one event with the
  code, also at an unchanged instant (no city save, one event); replay → none; idempotency conflict → none; shape refusal → none and
  no transaction; the duplicate-key race (`FailNextWrite = new DuplicateKeyException`) → none. One `[Theory]` per command for
  `EventDetail`, including an id the content does not have → null.
  **`EveryCityCommand_HasAnEventDetailCase`**: reflection over the Application assembly finds every `ICityCommand` type and fails when
  one is missing from the theory's data, so a later command cannot skip it.
  PostgreSQL (`server/tests/CityBuilder.IntegrationTests/Commands/` or `Security/`): over HTTP, an accepted and a refused command each
  leave exactly one row; two identical requests at once leave one; the existing OD-13 "stores nothing" tests must also count 0 events
  (add the count to their shared "nothing stored" assertion if there is one).
- Docs: `docs/how-to/add-command.md` (the new member), `08` → Required negative tests, `19` → New command (one phrase), module doc.

### Step 5 — settlement events
- New `server/src/CityBuilder.Application/Metrics/SettlementEvents.cs`:
  `static IReadOnlyList<PlayerEvent> Of(CityAggregate city, Settlement settlement, DateTimeOffset now)`: one `research.completed` per
  `settlement.CompletedResearch`, one `building.completed` per `settlement.CompletedBuildings` (type and level from `city.State.Buildings`
  after the settle; `Settlement` already makes both lists distinct and ordered). `city_clock` = `city.State.Clock`.
- Call it in `SettleCity.SettleAsync` and in both branches of `CommandPipeline.RunAsync`, after `settler.Close()`, before the save.
  Order of a request's events: `session.start`, the settlement's, then the command's.
- Tests: `SettleCity` and the pipeline with a fixture whose research and construction end inside the window (see `SettleCityTests`,
  `SettleFixture`); a long offline window with two completions → two events, once each; a second settle at the same instant → none;
  an upgrade → `value` is the new level; nothing completed → no event.
- Limitation to write into `06`: an offline completion carries the clock of the request that settled it, not of the moment it finished.

### Step 6 — session and player events
- New `server/src/CityBuilder.Application/Metrics/SessionPolicy.cs` (like `PresencePolicy`): `GapSeconds` (0 or more);
  `bool StartsSession(long lastSeenUnixSec, long nowUnixSec)` = `now - lastSeen > gap`.
- New options (`19` → New configuration key; pattern: `server/src/CityBuilder.Api/Time/SimulationOptions.cs` and `ServerClock.cs`,
  where `PresencePolicy` is registered): `Metrics:SessionGapSeconds`, default 1800, range 60 to 86400, validated at start, multiplied
  by `Simulation:DevTimeScale` like the presence grace (a Development clock runs faster). The option's doc comment states meaning,
  default and range. Regenerate `docs/reference/generated/configuration.md`.
- `SettleCity` and `CommandPipeline` take `SessionPolicy`; read `city.LastSeenUnixSec` **before** `CitySettler.Begin` (it marks the
  city seen) and record `session.start` when the policy says so. A session start always changes `last_seen_unix`, so the city saves.
- `DevLogin.CreateAsync`: takes `IPlayerEventRepository`; in the creating transaction add `player.created` and `session.start` for the
  new player (`city_clock` = the new city's clock). A login of an existing player records nothing (the next settling request decides).
  The lost race (`DuplicateKeyException`) stores none: its transaction rolled back.
- Tests: policy unit tests (the boundary: exactly the gap is not a new session); `SettleCityPresenceTests`-style tests for the gap;
  first login → exactly two events, second login → none, two first logins at once → two events in total (PostgreSQL); options
  validation fails on bad values.
- Update every constructor call the new parameters break: `Program.cs`, `PipelineFixture.Pipeline`, `SettleFixture`, the integration
  tests that build these by hand (`grep -rn "new SettleCity(\|new CommandPipeline(\|new DevLogin(" server`).

### Step 7 — guards
- `server/tests/CityBuilder.Application.Tests`: `IPlayerEventRepository_HasNoReadMember` (reflection: one method, `Add`).
- `server/tests/CityBuilder.Repository.Tests/Architecture/` (source scans, see `EngineSourceTests`): `PlayerEvents` (the `DbSet`) is
  named only in the context, the configuration, the snapshot/migrations and `PlayerEventRepository.cs`; `shared/Simulation/` and
  `client/` never contain `PlayerEvent` or `player_events`.
- Run and confirm unchanged: the golden tests, the determinism and property tests, `PerformanceBudgetTests` (re-run alone if the
  machine is loaded, `13` → Known broken), the contract tests, `ClientInputInventoryTests`, the OpenAPI test, and
  `git diff --stat origin/main -- client/ shared/ docs/reference/generated/endpoints.md docs/reference/generated/errors.md` = empty.

### Step 8 — docs, board, finish
- Close every row of the build log's Docs impact table. Beyond the pages named in the steps: `07` (a row: the events table, what it
  holds and never holds; Logging policy), `10` and `30` (one line each: the funnel part of analytics is built in M0 by CR-0009, with
  no SDK), `05` (no change expected: write `no change: no endpoint, field or header`), `31` (player event, checkpoint), `12` (a row
  if you judge "events are written in the city's transaction, never read by game code" project-wide), `docs/testing/README.md`,
  `docs/modules/CityBuilder.Api.md` (the options), `CHANGELOG.md`.
- Plan board, as CR-0008 did it (`docs/plan/M0-PLAN.md`: the count in its first lines, the board rows near `CR-0008-T3`, section 0.00
  and 7 if they list CRs): rows `CR-0009-T1` and `CR-0009-T2`, 88 → 90 tasks; `13-progress.md`: the count, a Milestone row
  "Player metrics (CR-0009)", a line under Deviations. `AGENTS.md` needs no edit in T1 (no new command).
- Then `/finish-task` (it runs by itself, OD-15): `quality.sh`, `check-docs.sh --base origin/main`, the handbook build, a line-by-line
  self-review (this is persistence and security), the PR, the task report.

## 5. Acceptance criteria of T1 (from the CR, with the section 2 points applied)
- [ ] Every command applied or refused by a game rule writes exactly one `player_events` row in its transaction; a replay, a conflict,
      a shape refusal and a rolled-back command write none.
- [ ] A command added later is recorded with no metrics code beyond its `EventDetail`; the reflection test enforces the case.
- [ ] A completed research and a completed construction or upgrade are recorded once each, also inside a long offline settlement.
- [ ] `player.created` once per player; `session.start` at the first login and after each gap longer than the setting.
- [ ] The table holds no display name, token, address or body; the column list is pinned by a test.
- [ ] A refused command at an unchanged instant stores its event and leaves `state_version` as it was.
- [ ] Engine results, the API contract and the client are unchanged (step 7).
- [ ] Docs, generated docs, plan board; `check-docs.sh`, the handbook build and `quality.sh` pass.

## 6. Traps
- `--no-build` after an edit runs stale binaries (`16` §C). Build first.
- The Stop hook blocks ending a turn with uncheckpointed code: checkpoint, don't argue.
- Never loosen an existing test to fit (G-Tests). If a "nothing is stored" test now sees an event, the code is wrong, not the test.
- If a step fails twice, log what you tried and ask (`16` → Stuck rule).
- No agents without the owner's approval (OD-9).
