# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IDLE   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** — (last completed: KIT-AGENT-RULE · the owner's standing rule about agents, `15` OD-9; PR #10 open against `main`; PR #9, M0-04-FOLLOWUP, is merged)
- **Branch:** `task/kit-agent-rule` (`main` merged in after PR #9) until the owner merges
- **Build log entry:** last: `docs/build-log/2026/2026-10-14-02-KIT-AGENT-RULE.md` (Session 1)
- **Last green commit:** the head of `task/kit-agent-rule` (`check-docs --base origin/main` OK; process documents only, no code changed)
- **Done steps:** all four (write-ahead; the rule in `15`, `02`, `CLAUDE.md`; everything that contradicted it; W10, CHANGELOG, finish)
- **Next step:** the owner merges the PR of this branch; then `/start-task M0-05` (Unity test foundation,
  `[S]`). It reads the M0-04 entry → Follow-ups, the M0-04-FOLLOWUP entry → Follow-ups. **No agents** (OD-9): self-review only.
- **Open questions for the owner:** (1) is the reading of OD-9 right (agents only, not the main session's model; one agent per whole task; the coder decides
  what "really necessary" means)? Decision 2 of the KIT-AGENT-RULE entry. (2) the two KIT-AUDIT-FIXES follow-ups (`AGENTS.md` command line, guard test in CI).
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
