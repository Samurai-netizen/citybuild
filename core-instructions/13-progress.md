# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-03 · Shared engine skeleton and architecture tests (`[S]`, Sonnet 5.5)
- **Branch:** `task/M0-03-engine-skeleton`
- **Build log entry:** `docs/build-log/2026/2026-10-12-03-M0-03.md` (Session 1; the 9-step plan is in it)
- **Last green commit:** `d2e0f9d` on `main` (baseline: locked restore, format check, Release build, 6 fast tests). No code on the branch yet.
- **Done steps:** 2 of 9 (write-ahead; tooling probes: results in the build log → Verification and decisions 4–6).
- **Next step:** plan step 3 in the build log: `shared/Simulation/package.json`, `Runtime/CityBuilder.Simulation.asmdef`,
  `shared/Simulation.Build/CityBuilder.Simulation.csproj` (no lock file, decision 5), the `server/CityBuilder.slnx` entry and
  `docs/modules/CityBuilder.Simulation.md` in one commit (`check-docs.sh` needs the module doc with the first `.csproj` or
  `.asmdef`). Green with an empty assembly; check warnings-as-errors with `--no-incremental`. Then step 4 (test-first:
  `IntegerMath`, `ItemId`, `CityBuilder.Simulation.Tests`). Decision 2 says where each dependency test goes.
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
