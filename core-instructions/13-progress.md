# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-02 · Server solution skeleton (`[S]`; this session: Claude Code / Opus 5.5)
- **Branch:** `task/M0-02-server-skeleton`
- **Build log entry:** `docs/build-log/2026/2026-10-12-02-M0-02.md` (Session 1)
- **Last green commit:** the head of `task/M0-02-server-skeleton` (CI's server sequence passes locally: 6 tests; `check-docs` OK)
- **Done steps:** 1 write-ahead · 2 gate questions answered (build log → Owner input) · 3 SDK pin, props, `src/` projects, slnx, Api host, module docs · 4 test projects (6 tests), lock files
- **Planned steps:** 2 gate questions (G-Dependency xUnit runner, G-Rules `AGENTS.md` marks) · 3 SDK pin, props files,
  `src/` projects, slnx, Api host · 4 test projects, lock files, CI sequence locally · 5 docs · 6 finish (`16` §E)
- **Next step:** step 5 docs: `14` approved list (test packages), `12` rows 019 (test stack) + 020 (build conventions),
  `07` threat rows for `/health` and OpenAPI, `docs/getting-started.md`, `docs/architecture/overview.md` projects table, `AGENTS.md` marks (owner OK).
- **Open questions for the owner:** the two KIT-AUDIT-FIXES follow-ups (`AGENTS.md` command line, guard test in CI).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh`

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 1 / 84

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 open |
| Shared engine: content, research, labor | not started | |
| Shared engine: invariants, performance | not started | |
| Server skeleton | in progress | M0-02 |
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

## Open issues / tech debt
- Unity 6.6 is installed, but the stack pins 6.3 LTS: install 6.3 before M0-04 (M0-01 audit A18).
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
