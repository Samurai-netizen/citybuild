# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-14 · Property test harness (`[O]`, Claude Code / claude-opus-5-5)
- **Branch:** `task/M0-14-property-harness` (from `main` = `b183438`)
- **Build log entry:** `docs/build-log/2026/2026-10-19-04-M0-14.md`
- **Last green commit:** the head of the branch (Simulation tests 729 passed, format clean)
- **Steps:** 1 write-ahead · 2 structural equality helper · 3 seeded generator + coverage test · 4 determinism property ·
  5 split and timer invariance properties · 6 docs and finish (details in the build log Plan)
- **Done steps:** 1, 2 (`StructuralEquality`, `ReportTotals`), 3 (`SeededRandom`, `CityOperation`, `CityHistory`, `CityGenerator`,
  `PropertyContent`, coverage test), 4 (`PropertyCheck` seed runner, `DeterminismPropertyTests`), all in
  `server/tests/CityBuilder.Simulation.Tests/Properties/`
- **Next step:** step 5: `SplitInvariancePropertyTests` (a + b ≤ cap: city equal, `ReportTotals` of the two pieces = the whole) and
  `TimerInvariancePropertyTests` (random splits beyond the cap: each timer completes in the piece holding its end second, final timers
  equal), both through `PropertyCheck.ForEachSeed` over both contents; then check that they catch an injected engine bug (reverted).
- **Open questions for the owner:** guard test in CI (KIT-AUDIT-FIXES follow-up); [MANUAL] run `test-unity.ps1` once on Windows (never
  executed); the template sample content (M0-04 follow-up).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh` · `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` ·
  `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (Editor closed)

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 13 / 85 (M0-14 in progress)

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 can be closed in M0-04 |
| Shared engine: content, research, labor | done | M0-03 merged (PR #7); the content model and its validation (M0-07a) merged (PR #14); the JSON file and server loading (M0-07b) merged (PR #15); city state and mutations (M0-08) merged (PR #16); `Simulator.Advance` (M0-09) merged (PR #17); rule tests (M0-10) merged (PR #18); golden scenario #1 (M0-11) merged (PR #19); engine review (M0-12) merged (PR #20) |
| Shared engine: invariants, performance | in progress | settlement window and production cap (M0-13) merged (PR #21); property test harness is M0-14 (in progress) |
| Server skeleton | done | M0-02 merged (PR #6) |
| Persistence (EF + PostgreSQL) | not started | |
| Auth + command pipeline | not started | |
| Commands: place / upgrade / assign workers / start research | not started | |
| Unity skeleton + networking | in progress | M0-04 (project, assemblies, GameRoot) and M0-05 (Unity tests, `test-unity.sh`) merged (PR #8, #12); networking is M0-37 |
| Unity city view + placement | not started | |
| Inspector, worker stepper, research screen, HUD | not started | |
| Resilience (limits, retries, logging) | not started | |
| Docs automation (DocTools) + docs completeness | not started | |
| Balance simulator | not started | |
| E2E / security / concurrency suites | not started | |
| Dev build + M0 close-out | not started | |

States: `not started` · `in progress` · `done` · `blocked (reason)`

## Commands that work right now
All run in M0-06 (details and the "since" task: `docs/getting-started.md` → Run).
- **Process:** `bash scripts/dev/setup-hooks.sh` · `bash scripts/dev/check-docs.sh [--base origin/main]` · `bash scripts/dev/show-task.sh <ID>` ·
  `bash scripts/dev/new-log-entry.sh <ID> "<title>" "<tool / model>"` · `bash scripts/dev/test-guard-bash.sh` (after changing the Bash guard)
- **Server:** `dotnet restore server/CityBuilder.slnx --locked-mode` · `dotnet format server/CityBuilder.slnx --verify-no-changes` ·
  `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` ·
  `dotnet run --project server/src/CityBuilder.Api` (loads `server/content/mvp-content.json`; another file: `-- --Content:FilePath=<file>`),
  then `curl http://localhost:8080/health` → `Healthy`
- **Unity (Editor closed):** `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (`.ps1` on Windows, never run yet) · compile check
  `<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -` (exit 0, no `error CS`; `client/README.md`)
- **Not yet:** integration tests (the filter finds none until M0-21) · `docker compose` (M0-19) · DocTools (M0-61) · `quality.sh` (M0-62) ·
  BalanceSim (M0-67)

## Open issues / tech debt
- Audit item A18 (Unity 6.6 installed, the stack pins 6.3) is closed: the project is 6000.3.25f1 (M0-04).
- Unity manifest pins below the editor's minimum or overridden in the lock (URP, test framework, navigation, timeline, ...): M0-66 audit.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- M0-07 is split into M0-07a (engine) and M0-07b (JSON file, server loading), one PR each: the owner's choice in the plan approval
  (build log BL-20261014-05, decision 1). Split plan tasks carry a letter; the commit id pattern allows it (`18`).
