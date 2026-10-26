# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** CR-0003-T1 · Code documentation: every file and declaration (`docs/changes/CR-0003-code-documentation.md`, owner's request of 2026-10-26)
- **Branch:** `task/CR-0003-T1-code-docs` (from `main` = `a402563`)
- **Build log entry:** `docs/build-log/2026/2026-10-25-01-CR-0003-T1.md`
- **Last green commit:** the head of the branch (fast suite: Repository 20, Simulation 872, Domain 1, Application 14, IntegrationTests 25)
- **Steps:** 1 start ✓ · 2 checker + standard ✓ · 3 server ✓ · 4 engine ✓ · 5 tests ✓ · 6 Unity client ✓ · 7 scripts and other files ✓ · 8 workflow docs ✓ ·
  9 finish (details: the entry's Session 1 plan)
- **Next step:** step 9, the finish: byte-identical Release assemblies against `origin/main`, the whole suite, `check-docs.sh --base`,
  the handbook build, the Docs impact table closed, build log `Done`, resume point `IDLE`, then the PR (the owner merges).
- **Open questions for the owner:** guard test in CI (KIT-AUDIT-FIXES follow-up); [MANUAL] run `test-unity.ps1` once on Windows (never
  executed); the template sample content (M0-04 follow-up; its scripts are the checker's only exemption); Dependabot PR #28.
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh` · `bash scripts/dev/docs-site.sh build` · `dotnet build server/CityBuilder.slnx` ·
  `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` · `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (Editor closed)

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 18 / 85 (CR-0003-T1 in progress)

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 can be closed in M0-04 |
| Shared engine: content, research, labor | done | M0-03 merged (PR #7); the content model and its validation (M0-07a) merged (PR #14); the JSON file and server loading (M0-07b) merged (PR #15); city state and mutations (M0-08) merged (PR #16); `Simulator.Advance` (M0-09) merged (PR #17); rule tests (M0-10) merged (PR #18); golden scenario #1 (M0-11) merged (PR #19); engine review (M0-12) merged (PR #20) |
| Shared engine: invariants, performance | done | settlement window and production cap (M0-13) merged (PR #21); property test harness (M0-14, with engine fix CR-0001) merged (PR #22); invariant properties (M0-15) merged (PR #23); performance budget M0-16 merged (PR #24; worst case ~37 ms per advance in Release); display helpers M0-17 merged (PR #25); spec sync and ADRs M0-18 merged (PR #26) |
| Server skeleton | done | M0-02 merged (PR #6) |
| Persistence (EF + PostgreSQL) | not started | |
| Auth + command pipeline | not started | |
| Commands: place / upgrade / assign workers / start research | not started | |
| Unity skeleton + networking | in progress | M0-04 (project, assemblies, GameRoot) and M0-05 (Unity tests, `test-unity.sh`) merged (PR #8, #12); networking is M0-37 |
| Unity city view + placement | not started | |
| Inspector, worker stepper, research screen, HUD | not started | |
| Resilience (limits, retries, logging) | not started | |
| Docs automation (DocTools) + docs completeness | in progress | handbook, Pages, docs-as-code checks and a full audit: CR-0002 done (PR #27); code documentation CR-0003-T1 in progress; DocTools is M0-61 |
| Balance simulator | not started | |
| E2E / security / concurrency suites | not started | |
| Dev build + M0 close-out | not started | |

States: `not started` · `in progress` · `done` · `blocked (reason)`

## Commands that work right now
All run in M0-06, the handbook in CR-0002-T1 (details and the "since" task: `docs/getting-started.md` → Run).
- **Process:** `bash scripts/dev/setup-hooks.sh` · `bash scripts/dev/check-docs.sh [--base origin/main]` · `bash scripts/dev/show-task.sh <ID>` ·
  `bash scripts/dev/new-log-entry.sh <ID> "<title>" "<tool / model>"` · `bash scripts/dev/test-guard-bash.sh` (after changing the Bash guard) ·
  `bash scripts/dev/docs-site.sh build` (the handbook; `start` previews it; Node.js 20+)
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
