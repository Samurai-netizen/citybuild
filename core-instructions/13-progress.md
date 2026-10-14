# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IDLE   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** — (last completed: M0-05 · Unity test foundation; PR open, the owner merges it)
- **Branch:** `task/M0-05-unity-test-foundation` (from `main` = `663a714`) until the owner merges
- **Build log entry:** last: `docs/build-log/2026/2026-10-14-03-M0-05.md` (Session 1)
- **Last green commit:** the head of `task/M0-05-unity-test-foundation` (`test-unity.sh`: EditMode 6 of 6, PlayMode 6 of 6; server fast suite 173 passed;
  `check-docs --base origin/main` OK)
- **Done steps:** all six (write-ahead; `AppInfo` + EditMode tests; PlayMode `BootstrapSceneTests`; `test-unity.sh` / `.ps1`; docs; self-review and finish)
- **Next step:** the owner merges the PR; then `/model` → Opus 5.5 and `/start-task M0-06` (Day 1 checkpoint, `[O]`). M0-06's text offers the `reviewer`
  subagent: only after the owner approves an OD-9 proposal. M0-06 also fills "Commands that work right now" and the `AGENTS.md` marks (protected: ask).
- **Open questions for the owner:** the two KIT-AUDIT-FIXES follow-ups (`AGENTS.md` command line, guard test in CI); [MANUAL] run `test-unity.ps1` once on
  Windows (never executed).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh` · `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` ·
  `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (Editor closed)

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 4 / 84

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 can be closed in M0-04 |
| Shared engine: content, research, labor | in progress | M0-03 (package, build project, helpers, architecture tests) merged (PR #7); the content model is M0-07 |
| Shared engine: invariants, performance | not started | |
| Server skeleton | done | M0-02 merged (PR #6) |
| Persistence (EF + PostgreSQL) | not started | |
| Auth + command pipeline | not started | |
| Commands: place / upgrade / assign workers / start research | not started | |
| Unity skeleton + networking | in progress | M0-04 (project, assemblies, GameRoot) merged (PR #8); M0-05 (Unity tests, `test-unity.sh`) in review; networking is M0-37 |
| Unity city view + placement | not started | |
| Inspector, worker stepper, research screen, HUD | not started | |
| Resilience (limits, retries, logging) | not started | |
| Docs automation (DocTools) + docs completeness | not started | |
| Balance simulator | not started | |
| E2E / security / concurrency suites | not started | |
| Dev build + M0 close-out | not started | |

States: `not started` · `in progress` · `done` · `blocked (reason)`

## Commands that work right now
- `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (`client/README.md` → Run the tests; `.ps1` for Windows, not yet run by anyone) ·
  `bash scripts/dev/check-docs.sh` · `bash scripts/dev/test-guard-bash.sh` · `bash scripts/dev/setup-hooks.sh` · `bash scripts/dev/show-task.sh <ID>` · `bash scripts/dev/new-log-entry.sh <ID> "<title>" "<agent>"`
- `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` ·
  `dotnet format server/CityBuilder.slnx --verify-no-changes` · `dotnet run --project server/src/CityBuilder.Api` (http://localhost:8080)
- Unity compile check, Editor closed: `<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -` (`client/README.md`; exit 0, no `error CS`)

## Open issues / tech debt
- Audit item A18 (Unity 6.6 installed, the stack pins 6.3) is closed: the project is 6000.3.25f1 (M0-04).
- Unity manifest pins below the editor's minimum or overridden in the lock (URP, test framework, navigation, timeline, ...): M0-66 audit.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
