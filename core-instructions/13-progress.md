# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-03 · Shared engine skeleton and architecture tests (`[S]`, Sonnet 5.5)
- **Branch:** `task/M0-03-engine-skeleton`
- **Build log entry:** `docs/build-log/2026/2026-10-12-03-M0-03.md` (Session 1; the 9-step plan is in it)
- **Last green commit:** `d2e0f9d` on `main` (baseline: locked restore, format check, Release build, 6 fast tests). No code on the branch yet.
- **Done steps:** 7 of 9 (write-ahead; probes; package + build project + module doc; `IntegerMath`, `ItemId`; Domain and Application
  reference Simulation with regenerated lock files; architecture, package and build-project tests, mutation-checked; docs and `12` rows
  021–022, CHANGELOG line). 121 fast tests green. The CHANGELOG line is already in; board and resume point still say In progress.
- **Next step:** plan step 8 in the build log: run the independent review as a Workflow of read-only `reviewer` agents over
  `git diff origin/main...HEAD` (lenses: spec and Definition of Done; engine rules and helper correctness; build, lock files and CI;
  test quality; docs and process), verify each finding adversarially, fix what is confirmed with a log line each. Then step 9 (finish):
  Definition of Done, `check-docs.sh --base origin/main`, clean-clone run of CI's server sequence, build log Verification table, board
  `In review`, resume point `IDLE`, `gh pr create`, CI result, task report.
- **Open questions for the owner:** the two KIT-AUDIT-FIXES follow-ups (`AGENTS.md` command line, guard test in CI).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh` · `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 2 / 84

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 open |
| Shared engine: content, research, labor | in progress | M0-03 (skeleton, helpers, architecture tests) started; content model is M0-07 |
| Shared engine: invariants, performance | not started | |
| Server skeleton | done | M0-02 merged (PR #6) |
| Persistence (EF + PostgreSQL) | not started | |
| Auth + command pipeline | not started | |
| Commands: place / upgrade / assign workers / start research | not started | |
| Unity skeleton + networking | not started | |
| Unity city view + placement | not started | |
| Inspector, worker stepper, research screen, HUD | not started | |
| Resilience (limits, retries, logging) | not started | |
| Docs automation (DocTools) + docs completeness | not started | |
| Balance simulator | not started | |
| E2E / security / concurrency suites | not started | |
| Dev build + M0 close-out | not started | |

States: `not started` · `in progress` · `done` · `blocked (reason)`

## Commands that work right now
- `bash scripts/dev/check-docs.sh` · `bash scripts/dev/test-guard-bash.sh` · `bash scripts/dev/setup-hooks.sh` · `bash scripts/dev/show-task.sh <ID>` · `bash scripts/dev/new-log-entry.sh <ID> "<title>" "<agent>"`
- `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` ·
  `dotnet format server/CityBuilder.slnx --verify-no-changes` · `dotnet run --project server/src/CityBuilder.Api` (http://localhost:8080)

## Open issues / tech debt
- Unity 6.6 is installed, but the stack pins 6.3 LTS: install 6.3 before M0-04 (M0-01 audit A18).
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
