# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Each merged task adds one line under **Unreleased**, with its task id. Milestone releases move the lines into a version section.

## Unreleased

### Added
- Instruction kit: specs, AI workflow (session protocol, change control, build log, documentation system), Claude Code skills,
  subagents and hooks, git hooks, CI, M0 plan with 84 tasks (KIT).
- Kit verified on macOS; `new-log-entry.sh` fixed for the first entry of a day; GitHub `main` ruleset and security settings;
  public-repo directive OD-7; skeleton folders; how-to for resuming an interrupted session; kit audit (M0-01).
- Server solution skeleton: .NET SDK 10.0.401 pin, `server/CityBuilder.slnx` (Api, Application, Domain, Infrastructure and three
  xUnit v3 test projects), shared build settings, central package versions with lock files, and an Api host with `/health`,
  ProblemDetails, OpenAPI in Development, `TimeProvider.System` and JSON console logs on http://localhost:8080 (M0-02).
- Shared engine skeleton: the Unity package `shared/Simulation` and the .NET project `shared/Simulation.Build` (`netstandard2.1`, C# 9),
  `IntegerMath` (`CeilDiv`, `ApplyPermilleReduction`) and `ItemId`, the `CityBuilder.Simulation.Tests` project, Domain and Application
  reference the engine, and tests for the dependency rules, the Unity package files and the build project settings (M0-03).
- Unity client skeleton: the owner's Unity 6000.3.25f1 project committed with its `.gitignore`; the local package `com.citybuilder.simulation`,
  Newtonsoft 3.2.2 and Input System 1.20.0, the editor's own versions (1.12.0 does not compile on 6000.3), in the manifest; the five asmdefs and the `.meta` files (also for
  `shared/Simulation`); `GameRoot` with a bootstrap that creates it when the `Bootstrap` scene loads, and a UI Toolkit "Not connected" label;
  module docs, tests for the client asmdefs and manifest (`ClientAssemblyTests`), `client/README.md` with the [MANUAL] steps for the Bootstrap scene (M0-04).
- The owner's `Bootstrap` scene (scene 0 of the build list); the three pre-release AI packages are removed from the Unity manifest and the manifest names
  the `UnityWebRequest` module, which they had been enabling (M0-04, owner request after PR #8).
- Standing owner directive OD-9: no agent or Workflow starts without the owner's approval of a proposal with options (no agents / small / big: why, how
  many, which model); the coder reviews its own branch by default; `ask` permission rules for the `Agent` and `Workflow` tools back it up; the agent
  definitions default to Sonnet (`15`, `02` rule 27, `CLAUDE.md`, `11`, `16`, `17`, `18`, skills, PR template, `.claude/settings.json`) (KIT-AGENT-APPROVAL).
- Unity test foundation: `AppInfo.Version` in `Client.Core`; EditMode tests for the engine inside Unity (`CeilDiv` through the local package) and
  `AppInfo`; PlayMode tests on the real Bootstrap scene (build list, one `GameRoot`, no error logged, the label, a reload); and
  `scripts/dev/test-unity.sh` and `.ps1`, which run both platforms headless from `UNITY_EDITOR`, print a summary and fail on a failed test,
  a run with no tests or a missing result (M0-05).
- Day 1 checkpoint: review of M0-01 to M0-05; ADR-0001 to ADR-0004 (server authority, PostgreSQL only, one endpoint per command,
  idempotency); the first full as-built architecture overview; the verified command lists in `13`, `getting-started.md` and `AGENTS.md`
  (format check, Bash guard self-test); clearer failures in `ClientAssemblyTests` when the Unity manifest is broken;
  `test-guard-bash.sh` committed as executable, so `setup-hooks.sh` leaves a fresh clone clean (M0-06).
- Content model in the engine: the ids `BuildingTypeId`, `ResearchId` and `EraId`; immutable definitions of eras, items, building types
  (houses and producers, levels, recipes), research (prerequisites, the three effect types) and the global rules; `GameContentFactory`, which
  turns plain `*Data` input into a `GameContent` or lists every error with a code and a JSON path (`04` §14); tests for every rule, a Unity
  test of the factory on Unity's runtime, and package tests for the `.meta` files. M0-07 is split into M0-07a and M0-07b; split tasks carry a
  letter in commit ids (M0-07a).
- Content file and server loading: `server/content/mvp-content.json` (the numbers of `23`, version `mvp-1`), copied next to the app; the
  port `IContentProvider`; a strict System.Text.Json loader (unknown fields, repeated keys and numbers as text rejected); the server loads
  the content from `Content:FilePath` before it accepts requests and refuses to start with every error listed; the how-to
  `add-building-or-item.md` (M0-07b).
- City state and mutations in the engine: `CityState`, `Building`, `CityFactory.CreateStarterCity`, the derived `CityQueries` (workers,
  builders, reserved, effective caps and labor, unlocks), and `CityRules` PlaceBuilding, UpgradeBuilding, AssignWorkers and StartResearch
  with `MutationResult`; era checks for placement and for item use in content (owner directive OD-10) (M0-08).
- Settlement in the engine: `Simulator.Advance` (research and building timers as event sources, auto-staffing, the start rule with research
  labor, the production cap and dormant tail) with a sorted `SettlementReport`, derived stall reasons (`StallReason`), and the how-to
  `add-research-effect.md` (M0-09).
- Engine rule tests: one test per rule of `04` in `EngineRules/` (cycles, starts, workers, research, upgrades), played out through commands
  and advances on the test fixture, which gains a 180-labor workshop level and a producer of an uncapped item; a mutation check shows that
  each test fails when its rule is broken (M0-10).
- Golden scenario #1: the first-session table of `23` as tests on a frozen copy of the content (`Fixtures/content-golden-v1.json`), every
  row checked (stock, free workers, phases, stall reasons, research), and one advance per action gap equal to one-second steps (M0-11).
- Settlement window and production cap: `SettlementWindow.Compute` in the engine (elapsed from the last settled server second to now; a
  clock behind elapses nothing and keeps the second), and a `SettleCity` use case skeleton in Application (`Cities/`: `ICityRepository`,
  `CityAggregate`) that floors the `TimeProvider` to whole seconds and runs `Simulator.Advance`, tested with `FakeTimeProvider`
  (`Microsoft.Extensions.TimeProvider.Testing` 10.10.0) and an in-memory repository (M0-13).
- Property test harness: a seeded random city generator (SplitMix64; commands only through `CityRules`, gaps up to four production caps;
  replayable, printable operation lists), a structural equality helper that names the first differing field, and properties over 500 seeds
  on two frozen contents: determinism, split invariance and timer invariance. Split invariance found an engine bug, now fixed (CR-0001):
  one start pass could leave a lower-id producer idle when a later start freed room under a cap in the same second; the start pass now
  repeats until nothing starts (`04` §6) (M0-14).
- Invariant properties 4–13 of `04` §10 over 500 seeds on both contents, one test each: a step-by-step replay (every timer second, the
  dormant tail against a test model, branches with commands mid-cycle) checks conservation, stock, caps, clock, production, workers,
  builders and research, labor per cycle, no starts after the cap, unlocks and prerequisites; `MutationResult` now names what a mutation
  spent and refunded; how-to `debug-determinism-failure.md` (M0-15).
- Performance budget: the worst case of `04` §12 (64 producers, 10 s cycles ending on all ten seconds, from a test content file) advanced
  8 h, then 7 days, with exact event counts and a timed test (median of 5 runs ≤ 500 ms; printed). An advance went from 144 ms to about
  40 ms (Release): per-advance reservations instead of a scan per capped output, a heap of building timers instead of two scans per event
  time, and a repeat start pass only when it can start something; no behaviour change, property suite green (M0-16).
- Display helpers in `shared/Simulation/Runtime/Display/` (display only, `04` §15): `PerHour` for a worker count, `NetPerHour` and
  `SecondsUntilFull` of an item, `ProgressPermille`, `SecondsUntilNextEvent` and `ResearchProgressPermille`, all integers rounded down (rates in
  thousandths per hour); `IntegerMath.FloorDiv` (M0-17).
- Spec sync and ADRs: `04` compared line by line with the engine; no rule differs, and ten places now say exactly what the engine does
  (every request ends with a zero-length advance, the era transition is planned, `ActiveResearch.EndsAt`, the allowed divisions as decision
  036, what each mutation pays and for how long, labor accounting, a same-count worker assignment). New ADRs: ADR-0005 settle on access,
  ADR-0006 production cap and dormant tail, ADR-0007 shared engine (with its fallback), ADR-0008 labor model (M0-18).
- Project handbook: the docs as a Docusaurus website (`website/`) that renders `core-instructions/`, `docs/` and the READMEs where they
  live, in book chapters, with local search; spec numbers and backticked paths become links, and every page shows its kind (spec, as
  built, how-to, decision, history…) and source file; built and link-checked in CI, published to GitHub Pages after every merge
  (`.github/workflows/docs-site.yml`), `scripts/dev/docs-site.sh`. Every task keeps it current: a Docs impact table in each build log
  entry, and `check-docs.sh --base` fails when a module's code changes without its module doc (or a reason) or a living doc still names a
  deleted or renamed file (`17` §5, §7; OD-11). A full audit of the docs against the code fixed every drift it found (CR-0002-T1).
- Code documentation: every file and every declaration of the code says what it is for (`14` → Documentation in code, how-to
  `docs/how-to/document-code.md`): XML doc comments on every C# type and member at every access level, tests and Unity scripts included,
  and a header comment on every script, hook, workflow, MSBuild, JavaScript, stylesheet, UXML and config file, with a comment above
  every shell, PowerShell and JavaScript function. Checked by the new test project `CityBuilder.Repository.Tests` (Microsoft's C#
  parser), by the compiler (missing or broken doc comments are errors in every server project) and by `check-docs.sh`. The code itself
  changed only in comments (CR-0003-T1).
- Persistence: PostgreSQL 18.6 in Docker (`docker-compose.yml`, `.env.example`), the EF Core model of the seven M0 tables with every key,
  check, index and foreign key, the migration `InitialCreate` applied at startup in Development only, the connection string from
  user-secrets or the environment, the pinned `dotnet-ef` tool, model and migration tests, and `docs/how-to/add-migration.md` (M0-19).
- Repository and mapping: Domain types `Player`, `LedgerEntry`, `CommandReceipt`; ports `ICityRepository.Save(city, ledger, receipt)`,
  `IPlayerRepository`, `IUnitOfWork`; `CityAggregate` keeps research completion clocks; explicit `CityMapping` with round-trip tests;
  `CityRepository`, `PlayerRepository`, `UnitOfWork` over EF Core, registered scoped (M0-20).
- Integration test base on real PostgreSQL: one `postgres:18.6` Testcontainers container per run, a fresh database per test from a
  migrated template, a `WebApplicationFactory` on it with a fake clock; tests for the migrations, the repositories and the constraints;
  `docs/testing/README.md` (M0-21).
- City transaction: `ICityTransactionRunner` is the one way a use case changes a city (begin, `SELECT … FOR UPDATE` on the acting
  player's city, work, commit; rollback on any exception; one city per request); `lock_timeout` from `Database:LockTimeoutMilliseconds`
  (default 3000); a lock timeout or a stale `state_version` is `ConcurrencyConflictException`; `SettleCity` runs on it (M0-22).
- Settle end to end: `SettleCity` runs the closing zero-length advance, writes one `SETTLEMENT_PRODUCED` / `SETTLEMENT_CONSUMED` row per
  item and direction (ref = the settlement's id), records research completions and the content version, and saves only when something
  changed; registered in the Api; tests on PostgreSQL (M0-23).

### Changed
- Handbook site: react and react-dom 19.2.9 → 19.3.1, scheduler 0.28.0 (Dependabot PR #28, DEP-PR28).

### Fixed
- Kit audit follow-ups A1, A2, A3, A9, A15, A16: `[PLAN]` tasks write ahead before the plan (no Plan Mode), the Bash guard no longer
  reads here-documents, comments or quoted text and checks each command on its own (new `scripts/dev/test-guard-bash.sh`),
  `scripts/dev/**` is a protected path, `scripts/` counts as code for the PR checks and the Stop hook, `.DS_Store` is untracked,
  and the license question is parked: no `LICENSE` file for now, directive OD-8 (KIT-AUDIT-FIXES).
- Engine review: `Simulator.Advance` allocates nothing per event (index loops; the stock and report totals in a sorted `ItemTotals`
  instead of a `SortedDictionary`, whose lookups box the id key), a timer due exactly at the clock completes before cycles start (`04` §7,
  decision 033), a restored producing building's end must match its labor, labor subtraction is `checked`; `04` §4 and §8 say which
  buildings have no stall reason and which level's worker limit applies while upgrading (decision 034) (M0-12).
