# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** AWAITING OWNER   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** — (next: M0-01 · Repository audit and kit verification)
- **Branch:** —
- **Build log entry:** —
- **Last green commit:** —
- **Done steps:** —
- **Next step:** Owner completes Day 0 in `docs/plan/M0-PLAN.md` (GitHub repo, hooks, branch protection), then runs `/start-task M0-01`.
- **Open questions for the owner:** —
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh`

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 0 / 84

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | not started | |
| Shared engine: content, research, labor | not started | |
| Shared engine: invariants, performance | not started | |
| Server skeleton | not started | |
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
- `bash scripts/dev/check-docs.sh` · `bash scripts/dev/setup-hooks.sh` · `bash scripts/dev/show-task.sh <ID>` · `bash scripts/dev/new-log-entry.sh <ID> "<title>" "<agent>"`

## Open issues / tech debt
- none yet

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
