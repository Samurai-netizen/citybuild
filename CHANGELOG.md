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

### Fixed
- Kit audit follow-ups A1, A2, A3, A9, A15, A16: `[PLAN]` tasks write ahead before the plan (no Plan Mode), the Bash guard no longer
  reads here-documents, comments or quoted text and checks each command on its own (new `scripts/dev/test-guard-bash.sh`),
  `scripts/dev/**` is a protected path, `scripts/` counts as code for the PR checks and the Stop hook, `.DS_Store` is untracked,
  and the license question is parked: no `LICENSE` file for now, directive OD-8 (KIT-AUDIT-FIXES).
