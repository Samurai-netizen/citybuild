# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-05 · Unity test foundation (`[S]`, Claude Code / claude-sonnet-5-5)
- **Branch:** `task/M0-05-unity-test-foundation` (from `main` = `663a714`)
- **Build log entry:** `docs/build-log/2026/2026-10-14-03-M0-05.md` (Session 1)
- **Last green commit:** the head of `task/M0-05-unity-test-foundation` (`test-unity.sh`: EditMode 6 of 6, PlayMode 6 of 6; server fast suite 173 passed)
- **Done steps:** 1 write-ahead; 2 `AppInfo` + EditMode tests (6 of 6); 3 PlayMode `BootstrapSceneTests` (6 of 6); 4 `scripts/dev/test-unity.sh` and `.ps1`
  (`.sh` run for real: 12 of 12, exit 0; every failure branch driven by a Unity stand-in; 7 of 7 mutations killed in a scratch clone; `.ps1` **not run**,
  no `pwsh` here) (planned: 5 docs, 6 self-review and finish)
- **Next step:** step 5, docs: module docs (Core: `AppInfo`, Presentation, Net text, tests sections), `client/README.md` (the script is no longer
  planned, tests exist), `docs/getting-started.md`, `docs/architecture/overview.md`, `09`, `08` if it says "planned", `AGENTS.md` (remove the
  `[M0-05]` mark; protected: the permission prompt asks the owner), CHANGELOG, decision rows only if project-wide (none so far), the build log's
  Changes / Verification / Follow-ups. **Agents: only after the owner approves a proposal (OD-9).**
- **Open questions for the owner:** the two KIT-AUDIT-FIXES follow-ups (`AGENTS.md` command line, guard test in CI).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh` · `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`

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
| Unity skeleton + networking | in progress | M0-04 (project, assemblies, GameRoot) merged (PR #8); the Unity tests are M0-05, networking M0-37 |
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
- Unity compile check, Editor closed: `<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -` (`client/README.md`; exit 0, no `error CS`)

## Open issues / tech debt
- Audit item A18 (Unity 6.6 installed, the stack pins 6.3) is closed: the project is 6000.3.25f1 (M0-04).
- Unity manifest pins below the editor's minimum or overridden in the lock (URP, test framework, navigation, timeline, ...): M0-66 audit.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
