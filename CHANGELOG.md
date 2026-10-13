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
  Newtonsoft and Input System 1.14.2 (1.12.0 does not compile on 6000.3) in the manifest; the five asmdefs and the `.meta` files (also for
  `shared/Simulation`); `GameRoot` with a bootstrap that creates it when the `Bootstrap` scene loads, and a UI Toolkit "Not connected" label;
  module docs, `client/README.md` with the [MANUAL] steps for the Bootstrap scene (M0-04).

### Fixed
- Kit audit follow-ups A1, A2, A3, A9, A15, A16: `[PLAN]` tasks write ahead before the plan (no Plan Mode), the Bash guard no longer
  reads here-documents, comments or quoted text and checks each command on its own (new `scripts/dev/test-guard-bash.sh`),
  `scripts/dev/**` is a protected path, `scripts/` counts as code for the PR checks and the Stop hook, `.DS_Store` is untracked,
  and the license question is parked: no `LICENSE` file for now, directive OD-8 (KIT-AUDIT-FIXES).
