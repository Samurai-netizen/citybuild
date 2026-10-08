# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IDLE   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** — (last completed: M0-01 · Repository audit and kit verification, PR open, `In review`)
- **Branch:** — (M0-01 work is on `task/M0-01-kit-audit` until the owner merges)
- **Build log entry:** last: `docs/build-log/2026/2026-10-08-01-M0-01.md`
- **Last green commit:** the head of `task/M0-01-kit-audit` (docs-only task; `check-docs --base origin/main` OK)
- **Done steps:** —
- **Next step:** owner merges the M0-01 PR, then runs `/start-task M0-02` (Server solution skeleton, `[S]`, Sonnet 5.5).
  M0-02 sets the M0-01 board row to `Done`.
- **Open questions for the owner:** M0-01 audit A1, A2, A3, A9, A15 and the Dependabot PRs #1/#2 (build log → Follow-ups).
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
- `.DS_Store` is tracked although ignored: untrack it in a `chore(repo)` PR (A16).
- `CLAUDE.md` says Plan Mode for `[PLAN]` tasks, which conflicts with the write-ahead in `16` §B (A1).
- `guard-bash.sh` false positives on heredoc text (A2). Workaround: write text with the file tools; run `git push` in its own call.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
