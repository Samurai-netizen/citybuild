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
| Unity PlayMode | `Assets/_Project/Tests/PlayMode` | s | Bootstrap starts; the city view builds from a snapshot |
| Manual smoke | `docs/testing/UNITY-SMOKE-TEST.md` *(planned, M0-72)* | min | Full loop on a real build |

Mark integration tests with `[Trait("Category", "Integration")]` so the fast suite runs without Docker.

## Simulation test kinds (the most important ones)
- **Rule tests:** one rule per test. Start rule, completion rule, ordering (research → completions → starts),
  storage cap, labor ÷ workers with ceiling, worker change mid-cycle, frozen cycle, auto-staffing, research effects
  (unlock, labor reduction, storage bonus), upgrade refund, dormant tail after the production cap.
- **Golden scenarios:** scripted cities with exact expected values at exact clock times. Golden scenario #1 is
  the first-session table in `23-mvp-content.md`. Golden tests use a **frozen** content file.
- **Property tests:** seeded random cities and random elapsed splits (fixed seeds, ≥ 500 cases each), covering
  determinism, split invariance, timer invariance, conservation, non-negativity, caps, worker limits, labor accounting,
  builder/research slots, and no starts after the cap. Print the seed and the operation list on failure.
  From a failing seed to a rule test: `docs/how-to/debug-determinism-failure.md`.
- **Performance budget:** the worst-case city over the full production cap stays within budget, with the event count asserted.

## Time
Server code takes `TimeProvider`. Tests use `FakeTimeProvider` and advance it explicitly.
No test may call `Thread.Sleep` or depend on the real clock.

## Required negative tests for every command
Unauthenticated · malformed body · unknown ids · out-of-bounds or out-of-range values · each business-rule error code ·
replay with the same payload · replay with a different payload · another player's ids · concurrent duplicates.
Assert on the **database state and ledger**, not only on the HTTP status.

## Naming
`Method_Scenario_ExpectedResult`, e.g. `Advance_SawmillLosesWorkersMidCycle_KeepsProgressAndFreezes`.
Security tests name the property they protect: `Security_ClientCannotSetCost_…`.
Every test class has a doc comment that says what it covers, every test one that says the behaviour it proves, and every helper one
that says what it builds (`14` → Documentation in code).

## Commands
```
dotnet test server/CityBuilder.slnx --filter "Category!=Integration"   # fast, no Docker
dotnet test server/CityBuilder.slnx --filter "Category=Integration"    # needs Docker
UNITY_EDITOR=<Unity program> scripts/dev/test-unity.sh  (or .ps1)     # Unity batchmode; the Editor must be closed
```

## Where tests run
- **CI (GitHub Actions):** server fast and integration tests on every PR (`18`).
- **Locally only:** Unity EditMode and PlayMode (`scripts/dev/test-unity.sh`). Record the results in the build log Verification table.
- Record test counts before → after in the task report. A drop in the count needs a G-Tests approval.

## Rules for AI coders
- Never delete, skip or loosen an assertion to get green. If a test is wrong, explain why first.
- A bug fix starts with a failing regression test.
- If you could not run a suite (no Docker, no Unity), say so explicitly in the task report.
