# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IDLE   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** — (last completed: M0-03 · Shared engine skeleton and architecture tests; PR open)
- **Branch:** `task/M0-03-engine-skeleton` (until the owner merges the PR)
- **Build log entry:** last: `docs/build-log/2026/2026-10-12-03-M0-03.md` (Session 1)
- **Last green commit:** the head of `task/M0-03-engine-skeleton` (clean clone at `cb63262`: CI's server sequence, 152 fast tests;
  the GitHub CI result is in the build log)
- **Done steps:** all nine (write-ahead, probes, package and build project, helpers and tests, references, architecture tests, docs,
  the independent review with its fixes, finish)
- **Next step:** the owner merges the PR, then runs `/start-task M0-04` (Unity project skeleton, `[S]`, MANUAL first). The owner's Unity
  project already exists, untracked, in `client/CityBuilder/` (6000.3.25f1): M0-04 commits it, adds `client/CityBuilder/.gitignore`
  (also for `*.slnx`), and removes the lines that the M0-03 session added to this clone's `.git/info/exclude` (build log decision 24).
  M0-04 sets the M0-03 board row to `Done` and reads the M0-03 build log → Follow-ups first.
- **Open questions for the owner:** the two KIT-AUDIT-FIXES follow-ups (`AGENTS.md` command line, guard test in CI).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh` · `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 2 / 84

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 can be closed in M0-04 |
| Shared engine: content, research, labor | in progress | M0-03 (package, build project, helpers, architecture tests) in review; the content model is M0-07 |
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
- Unity 6.3 LTS now exists: the owner's project in `client/CityBuilder` is 6000.3.25f1, so the audit item A18 (Unity 6.6 installed, the
  stack pins 6.3) can be closed in M0-04.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
