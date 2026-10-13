# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-04 · Unity project skeleton (`[S]`, MANUAL first; the owner's manual steps 1–5 are done, one deviation: decision 1)
- **Branch:** `task/M0-04-unity-skeleton` (M0-03 is merged: PR #7)
- **Build log entry:** `docs/build-log/2026/2026-10-13-01-M0-04.md` (Session 1, Claude Code / claude-sonnet-5-5)
- **Last green commit:** the head of this branch after step 3 (headless import exit 0; engine tests 145 passed; `check-docs` OK)
- **Plan (7 steps, details in the build log):** 1 write-ahead · 2 adopt the owner's project (`.gitignore`, PanelSettings rename) ·
  3 manifest, folders, five asmdefs, headless import for `.meta` files · 4 `GameRoot`, bootstrap, "Not connected" label, scratch verification ·
  5 docs and [MANUAL] steps · 6 independent review (Workflow) · 7 finish and PR
- **Done steps:** 1, 2, 3 (Input System bumped to 1.14.2 because 1.12.0 does not compile on 6000.3.25f1: decision 023)
- **Next step:** step 4: `Presentation/Bootstrap/GameRoot.cs` + `GameBootstrap.cs` (spawns GameRoot on `sceneLoaded` for the scene named
  `Bootstrap`), `Resources/UI/ConnectionStatus.uxml` + `.uss`; headless import for `.meta` files; verify in a scratch copy outside the repo.
  Unity 6000.3.25f1 is at `/Applications/Unity/Hub/Editor/6000.3.25f1`; the Editor must stay closed while a headless run uses the project.
- **Open questions for the owner:** the two KIT-AUDIT-FIXES follow-ups (`AGENTS.md` command line, guard test in CI).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh` · `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 3 / 84

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 can be closed in M0-04 |
| Shared engine: content, research, labor | in progress | M0-03 (package, build project, helpers, architecture tests) merged (PR #7); the content model is M0-07 |
| Shared engine: invariants, performance | not started | |
| Server skeleton | done | M0-02 merged (PR #6) |
| Persistence (EF + PostgreSQL) | not started | |
| Auth + command pipeline | not started | |
| Commands: place / upgrade / assign workers / start research | not started | |
| Unity skeleton + networking | in progress | M0-04 (project, assemblies, GameRoot) on its branch; networking is M0-37 |
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
