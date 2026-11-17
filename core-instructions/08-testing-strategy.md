# Testing Strategy

Tests protect **gameplay invariants**, not a coverage percentage. A feature without tests is not done.

## Layers

| Layer | Project | Speed | What it proves |
|---|---|---|---|
| Simulation | `CityBuilder.Simulation.Tests` | ms | Engine rules, labor, research, invariants, golden scenarios, performance budget |
| Domain/Application | `CityBuilder.Domain.Tests`, `CityBuilder.Application.Tests` | ms | Use cases with fakes: validation, pipeline order, idempotency logic, error codes |
| Integration | `CityBuilder.IntegrationTests` | s | Real PostgreSQL (Testcontainers) + in-process API (`WebApplicationFactory`): persistence, locks, receipts, HTTP contract. In process without a database: host, content loading, the EF model against `06` and its migrations, the city mapping's round trip and what
the repositories stage in EF's change tracker. In the `Integration` category, on one PostgreSQL 18.6 container per run with a fresh database
per test (how to run: `docs/testing/README.md`): migrations on an empty database, the repositories through the server, the constraints |
| Repository | `CityBuilder.Repository.Tests` | ms | Checks over the whole source tree: every C# file says what it is for and every declaration has a doc comment, Unity scripts included (`14` → Documentation in code); each rule is tested on small samples |
| Unity EditMode | `Assets/_Project/Tests/EditMode` | ms | DTO (de)serialization, client state store, grid math, retry policy, display prediction, view-models |
| Unity PlayMode | `Assets/_Project/Tests/PlayMode` | s | Bootstrap starts; the city view builds from a snapshot; the build menu builds its cards from the content; the placement flow draws tile hints and a pending ghost and ends in a building or an error; every command result shows as a toast and no error code is without a message |
| Manual smoke | `docs/testing/UNITY-SMOKE-TEST.md` *(planned, M0-72)* | min | Full loop on a real build |

Mark integration tests with `[Trait("Category", "Integration")]` so the fast suite runs without Docker.

## Test-first loop (every behaviour, every layer)
A test is written, and seen failing, **before** the code it tests (owner directive OD-20, `15`; the steps are in `16` §C).
1. **List.** The task spec names the behaviours ("Tests first"). Add the neighbours you see: the error codes, the boundaries, the
   replay. Don't pad: a test protects a rule, an invariant or a contract, not a line of code.
2. **Red.** Write those tests with the least code that compiles. Run only them. Each fails because the behaviour is missing; a test
   that passes before its code exists tests nothing, so fix the test.
3. **Green.** Write the code until the same focused run passes.
4. **Clean.** Refactor with the tests green.

| Kind of work | The test that comes first |
|---|---|
| Engine rule, effect, content rule | Rule tests; a property when it is an invariant; a golden table when it is a scenario |
| Command, endpoint | Rule tests of the mutation, application tests with fakes, integration tests (every error code, replay, another player), `Security_` tests |
| Persistence, migration | Integration tests on the database state, from the previous schema where data exists |
| Client logic (store, view-model, formatting, planner) | Unity EditMode tests |
| Client screen behaviour | PlayMode test of the flow where one exists for the area; else EditMode on the view-model |
| Bug | The regression test that fails with the bug |
| Pure looks (layout, colour, motion, art, sound choice) | No test: a line in the task's `[MANUAL]` list, and the static checks (`MobileLayoutTests`, `check-ui-rules.py`) |
| Tools and scripts | Their test project, or a script that proves each outcome (as `test-guard-bash.sh` does) |

The evidence is in git: a `test(<ID>): red, …` save before the `feat(<ID>): …` that makes it green.

## Test tiers: what to run, and when
Run the smallest set that can fail (OD-21). `scripts/dev/test.sh` prints one line per area and only the failing tests.

| Tier | Runs | When | Command |
|---|---|---|---|
| **T0 focus** | The class or method under work | Every red and green of the loop | `bash scripts/dev/test.sh sim CourierRouteTests` |
| **T1 area** | The test projects of the files a part changed | At each checkpoint | `bash scripts/dev/test.sh changed` (`client/` or `shared/` changed: `test-unity.sh` too, where `UNITY_EDITOR` is set) |
| **T2 full** | Everything CI runs, locally | **Once, at the finish**, and at the early points below | `bash scripts/dev/quality.sh` |
| **T3 CI** | Everything, on a clean machine | Every PR | GitHub Actions (`18`) |

**An early full run (T2, or `test.sh fast`) is worth its cost** only when a part: changes the engine's shared rules or a type the
server and the client both use · changes content JSON or a golden fixture · renames or moves across projects · merges `main` into the
branch · comes right before a `[MANUAL]` playtest or a question that depends on everything working. The task spec's "Full run" line
names these points; outside them, T0 and T1 are enough, and CI is the net.

Areas of `test.sh`: `sim` (engine) · `domain` · `app` · `int` (in-process, no Docker) · `int-db` (PostgreSQL, needs Docker) · `repo`
(documentation and architecture checks over the source tree) · `doctools` · `fast` (all but `int-db`) · `changed`.
A changed engine runs `sim` and `app`; changed content runs `sim`, `int` and `doctools`; any changed C# file runs `repo`.

## Simulation test kinds (the most important ones)
- **Rule tests:** one rule per test. Start rule, completion rule, ordering (research → completions → starts),
  storage cap, labor ÷ workers with ceiling, worker change mid-cycle, frozen cycle, auto-staffing, research effects
  (unlock, labor reduction, storage bonus), upgrade refund, production for the whole window until a store is full (the offline cap and
  its dormant tail went in CR-0008-T1 part H). CR-0008 adds a rule test per logistics rule: built for the path rule (with a brute-force
  check on small maps) and roads (CR-0008-T1 part C), stores and limits, the purse, projects and what a delivery fills first, trips,
  amounts and rounding (part E), couriers and routes (part F), and stamina, the absence start and re-energizing (part H).
- **Golden scenarios:** scripted cities with exact expected values at exact clock times. Golden scenario #1 is
  the first-session table in `23-mvp-content.md`. Golden tests use a **frozen** content file. Golden scenario #1 also runs
  end to end through the HTTP API on PostgreSQL (Integration layer, M0-36): every row via `GET /city`, the stored city equal
  to the engine's, and the ledger reconciled with the stock. CR-0008 rewrote #1 (a first session with the science building, manual trips
  and deliveries to a construction site; built in CR-0008-T1 part E, in the engine and over HTTP, on `content-golden-v2.json`, and
  `content-golden-v1.json` is gone) and adds #2 (a courier route, a road, an absence, tired couriers, re-energize), in `23`, in the engine
  and over HTTP (part H; over HTTP only the script's own requests are sent, as every request is presence).
- **Property tests:** seeded random cities and random elapsed splits (fixed seeds, ≥ 500 cases each), covering
  determinism, split invariance, timer invariance, conservation, non-negativity, caps, worker limits, labor accounting,
  builder/research slots. Print the seed and the operation list on failure. CR-0008: conservation over stores, loads, deliveries and
  the purse, store limits (only refunds and returned loads exceed them), workers on trips counted and loads within the carry limit, with
  manual trips in the generated histories (built, CR-0008-T1 part E); couriers and routes (part F); generated absences, split and timer
  invariance for one absence start and any lengths, and stamina spent exactly after the absence start (invariant 14, part H). The path
  rule (invariant 12) is checked against a brute force on small maps (part C).
  From a failing seed to a rule test: `docs/how-to/debug-determinism-failure.md`.
- **Ledger audit** (Integration layer, M0-64): seeded players play a long random game of every command and settles over HTTP with
  `FakeTimeProvider`; then `LedgerAuditor` audits every stored city: zero mismatches (grant first, an unbroken `balance_after` chain, each
  item's sum equal to its city total). The game must reach each command's write path and the ledger reasons it can write; a failure names
  the seed and step. How to run it: `docs/testing/README.md`.
- **Performance budget:** the worst-case city stays within budget, with the event count asserted: 64 producers over 8 hours and over 7
  days after them, until their stores stop them (part H); from CR-0008 part I the worst case of `04` §12 (couriers on the shortest walks
  for their whole stamina).

## Time
Server code takes `TimeProvider`. Tests use `FakeTimeProvider` and advance it explicitly.
No test may call `Thread.Sleep` or depend on the real clock. Production code is held to it by the build: `server/BannedSymbols.txt` bans the clock
properties and `Thread.Sleep` in every `src/` project (`14`).

## Required negative tests for every command
Unauthenticated · malformed body · unknown ids · out-of-bounds or out-of-range values · each business-rule error code ·
replay with the same payload · replay with a different payload · another player's ids · concurrent duplicates.
Assert on the **database state and ledger**, not only on the HTTP status. A rule's refusal stores its one player event (CR-0009) and
nothing else (`DatabaseCommands.AssertStoredWithRefusalsAsync`); a shape refusal, a replay and a conflict store none. The command's
`EventDetail` has a case in `CommandEventDetailTests`.

## Naming
`Method_Scenario_ExpectedResult`, e.g. `Advance_SawmillLosesWorkersMidCycle_KeepsProgressAndFreezes`.
Security tests name the property they protect: `Security_ClientCannotSetCost_…`.
Every test class has a doc comment that says what it covers, every test one that says the behaviour it proves, and every helper one
that says what it builds (`14` → Documentation in code).

## Commands
```
bash scripts/dev/test.sh <area> [filter]                               # T0 and T1: quiet, focused (areas above)
bash scripts/dev/quality.sh                                            # T2: everything CI runs
dotnet test server/CityBuilder.slnx --filter "Category!=Integration"   # the fast suite, raw output
dotnet test server/CityBuilder.slnx --filter "Category=Integration"    # needs Docker
UNITY_EDITOR=<Unity program> scripts/dev/test-unity.sh  (or .ps1)     # Unity batchmode; the Editor must be closed
```
When a Unity test fails, read `result="Failed"` in `client/CityBuilder/Logs/test-unity/*.xml`, never the whole log.

## Where tests run
- **CI (GitHub Actions):** server fast and integration tests on every PR (`18`).
- **Locally only:** Unity EditMode and PlayMode (`scripts/dev/test-unity.sh`). Record the results in the build log Verification table.
- Record test counts before → after in the task report. A drop in the count needs a G-Tests approval.

## Rules for AI coders
- Tests first: no behaviour without a test that was red before it (above).
- Run the smallest tier that can fail; never the full suite after every change (above).
- Never delete, skip or loosen an assertion to get green. If a test is wrong, explain why first.
- A bug fix starts with a failing regression test.
- If you could not run a suite (no Docker, no Unity), say so explicitly in the task report.
