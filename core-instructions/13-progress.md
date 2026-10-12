# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** KIT-AUDIT-FIXES · owner-approved M0-01 audit fixes A1, A2, A3, A16 (maintenance, no plan task; M0-01 is merged, PR #4)
- **Branch:** `task/kit-audit-fixes`
- **Build log entry:** `docs/build-log/2026/2026-10-12-01-KIT-AUDIT-FIXES.md` (Session 1, Claude Code / claude-sonnet-5-5)
- **Last green commit:** `481aff4` (A16: `.DS_Store` untracked; `check-docs` OK)
- **Done steps:** write-ahead; step 1 (A16)
- **Next step:** step 2, A1 (`CLAUDE.md`). Then A3 (protected list), A2 (`guard-bash.sh` + test), finish.
  Steps are listed in the build log. After the owner merges the PR: `/start-task M0-02` (Server solution skeleton, `[S]`).
  M0-02 sets the M0-01 board row to `Done`.
- **Open questions for the owner:** M0-01 audit A9, A15 (build log → Follow-ups).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh`

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 0 / 84

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 (in review); audit follow-ups open |
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
- Unity 6.6 is installed, but the stack pins 6.3 LTS: install 6.3 before M0-04 (M0-01 audit A18).
- `CLAUDE.md` says Plan Mode for `[PLAN]` tasks, which conflicts with the write-ahead in `16` §B (A1).
- `guard-bash.sh` false positives on heredoc text (A2). Workaround: write text with the file tools; run `git push` in its own call.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
