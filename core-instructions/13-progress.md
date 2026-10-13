# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-04 · Unity project skeleton (`[S]`, MANUAL first; the owner's manual steps 1–5 are done, one deviation: decision 1)
- **Branch:** `task/M0-04-unity-skeleton` (M0-03 is merged: PR #7)
- **Build log entry:** `docs/build-log/2026/2026-10-13-01-M0-04.md` (Session 1, Claude Code / claude-sonnet-5-5)
- **Last green commit:** the head of this branch after the review fixes (headless import exit 0, no compiler errors; build 0 warnings; 170 fast tests; format and `check-docs` OK)
- **Plan (7 steps, details in the build log):** 1 write-ahead · 2 adopt the owner's project (`.gitignore`, PanelSettings rename) ·
  3 manifest, folders, five asmdefs, headless import for `.meta` files · 4 `GameRoot`, bootstrap, "Not connected" label, scratch verification ·
  5 docs and [MANUAL] steps · 6 independent review (Workflow) · 7 finish and PR
- **Done steps:** 1 write-ahead · 2 owner's project adopted · 3 manifest, five asmdefs, `.meta` files · 4 `GameRoot`, bootstrap, label (verified in a
  scratch copy) · 5 docs · 6 independent review (9 agents; fixes: Input System 1.20.0 and Newtonsoft 3.2.2 are the editor's own versions,
  `overrideReferences` on all runtime assemblies, `ClientAssemblyTests`, doc corrections; decisions 4 and 13–15, rows 023–024)
- **Next step:** step 7, finish (`16` §E): Definition of Done, `check-docs.sh --base origin/main`, board row `In review`, resume point `IDLE`, PR
  (template), task report. Unity is at `/Applications/Unity/Hub/Editor/6000.3.25f1`; keep the Editor closed while a headless run uses the project.
- **Open questions for the owner:** (1) the pre-release AI packages in the Unity manifest (`ai.assistant`, `ai.inference`, `pipeline`): not on
  `14`'s list, recommend removing; (2) the [MANUAL] Bootstrap scene (`client/README.md`), needed before M0-05; (3) the two KIT-AUDIT-FIXES
  follow-ups (`AGENTS.md` command line, guard test in CI).
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
- Unity compile check, Editor closed: `<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -` (`client/README.md`; exit 0, no `error CS`)

## Open issues / tech debt
- Audit item A18 (Unity 6.6 installed, the stack pins 6.3) is closed: the project is 6000.3.25f1 (M0-04).
- Unity manifest pins below the editor's minimum or overridden in the lock (URP, test framework, navigation, timeline, ...): M0-66 audit.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- none yet (record the reason and link the decision row in `12-decisions.md`)
