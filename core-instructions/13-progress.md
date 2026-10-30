# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-36 · server golden E2E over HTTP (`[O]`, Opus 5.5)
- **Branch:** `task/M0-36-golden-e2e-http` (from `main` = `b31baa2`)
- **Build log entry:** `docs/build-log/2026/2026-10-30-01-M0-36.md`
- **Last green commit:** the head of the branch (build 0 warnings, format clean, fast suite green, Integration 171 incl. the 4 new)
- **Planned steps:** 1. golden scenario #1 over HTTP (every table row via `GET /city`, stored city = engine replay, ledger = stock);
  2. the Trading Post at 1 worker for an hour (coins within 10, one more free worker); 3. `docs/api/smoke.http` and docs; 4. finish, PR
- **Done steps:** start; 1 and 2 (`IntegrationTests/Golden/`, 4 tests, all green; `05` needs no change); 3 (`docs/api/smoke.http`, docs)
- **Next step:** step 4: run the smoke requests against the real server as `devtest` (OD-14; a throwaway `Auth__SigningKey`), then `/finish-task`
- **Open questions for the owner:** the step-7 comment edits of protected files that skipped the
  `ask` prompt (commit `9156324`, M0-21 build
  log → Problems): keep or review; guard test in CI (KIT-AUDIT-FIXES follow-up); [MANUAL] run `test-unity.ps1` once on Windows (never
  executed); the template sample content (M0-04 follow-up; its scripts are the checker's only exemption); the 7
  security alerts GitHub reports on `main`; M0-30: decisions 2 and 5 (400 for a too-big body, not the 413 my
  question named; malformed type ids 400 instead of 422).
- **Known broken / do not touch:** —
- **Verify with:** `bash scripts/dev/check-docs.sh` · `bash scripts/dev/docs-site.sh build` · `dotnet build server/CityBuilder.slnx` ·
  `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` · `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (Editor closed)

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 35 / 85

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 can be closed in M0-04 |
| Shared engine: content, research, labor | done | M0-03 merged (PR #7); the content model and its validation (M0-07a) merged (PR #14); the JSON file and server loading (M0-07b) merged (PR #15); city state and mutations (M0-08) merged (PR #16); `Simulator.Advance` (M0-09) merged (PR #17); rule tests (M0-10) merged (PR #18); golden scenario #1 (M0-11) merged (PR #19); engine review (M0-12) merged (PR #20) |
| Shared engine: invariants, performance | done | settlement window and production cap (M0-13) merged (PR #21); property test harness (M0-14, with engine fix CR-0001) merged (PR #22); invariant properties (M0-15) merged (PR #23); performance budget M0-16 merged (PR #24; worst case ~37 ms per advance in Release); display helpers M0-17 merged (PR #25); spec sync and ADRs M0-18 merged (PR #26) |
| Server skeleton | done | M0-02 merged (PR #6) |
| Persistence (EF + PostgreSQL) | done | M0-19 (EF Core model, migrations, Docker) merged (PR #31); repository and mapping M0-20 merged (PR #32); Testcontainers M0-21 merged (PR #33); locking M0-22 merged (PR #34); settle M0-23 merged (PR #35); M0-24 merged (PR #36) |
| Auth + command pipeline | in progress | dev auth M0-25 merged (PR #37); the pipeline M0-26 merged (PR #38); its integration tests M0-27 merged (PR #39); content and city endpoints M0-28 merged (PR #40); PlaceBuilding M0-29 merged (PR #41); attack review M0-30 merged (PR #42) |
| Commands: place / upgrade / assign workers / start research | done | place-building M0-29 merged (PR #41; `docs/how-to/add-command.md`); upgrade-building M0-31 merged (PR #43; OD-13); assign-workers M0-32 merged (PR #44); start-research M0-33 merged (PR #45): the four commands of M0 |
| Unity skeleton + networking | in progress | M0-04 (project, assemblies, GameRoot) and M0-05 (Unity tests, `test-unity.sh`) merged (PR #8, #12); networking is M0-37 |
| Unity city view + placement | not started | |
| Inspector, worker stepper, research screen, HUD | not started | |
| Resilience (limits, retries, logging) | in progress | error catalogue, global handler, rate limits and input limits M0-34 merged (PR #46); logging, correlation and the dev time scale M0-35 merged (PR #47) |
| Docs automation (DocTools) + docs completeness | in progress | handbook, Pages, docs-as-code checks and a full audit: CR-0002 done (PR #27); code documentation CR-0003 done (PR #29); DocTools is M0-61 |
| Balance simulator | not started | |
| E2E / security / concurrency suites | in progress | server golden E2E over HTTP M0-36 in progress |
| Dev build + M0 close-out | not started | |

States: `not started` · `in progress` · `done` · `blocked (reason)`

## Commands that work right now
All run in M0-06, the handbook in CR-0002-T1 (details and the "since" task: `docs/getting-started.md` → Run).
- **Process:** `bash scripts/dev/setup-hooks.sh` · `bash scripts/dev/check-docs.sh [--base origin/main]` · `bash scripts/dev/show-task.sh <ID>` ·
  `bash scripts/dev/new-log-entry.sh <ID> "<title>" "<tool / model>"` · `bash scripts/dev/test-guard-bash.sh` (after changing the Bash guard) ·
  `bash scripts/dev/docs-site.sh build` (the handbook; `start` previews it; Node.js 20+)
- **Server:** `dotnet restore server/CityBuilder.slnx --locked-mode` · `dotnet format server/CityBuilder.slnx --verify-no-changes` ·
  `dotnet build server/CityBuilder.slnx` · `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` ·
  `dotnet run --project server/src/CityBuilder.Api` (loads `server/content/mvp-content.json`; another file: `-- --Content:FilePath=<file>`;
  needs `ConnectionStrings:CityBuilder` and, in Development, the running database), then `curl http://localhost:8080/health` → `Healthy`
- **Database (M0-19):** `docker compose up -d postgres` (`.env` from `.env.example`) · `dotnet tool restore` ·
  `dotnet ef migrations add <Name> …` (`docs/how-to/add-migration.md`)
- **Unity (Editor closed):** `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (`.ps1` on Windows, never run yet) · compile check
  `<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -` (exit 0, no `error CS`; `client/README.md`)
- **Integration tests (M0-21, Docker running):** `dotnet test server/CityBuilder.slnx --filter "Category=Integration"` (`docs/testing/README.md`)
- **Not yet:** DocTools (M0-61) · `quality.sh` (M0-62) ·
  BalanceSim (M0-67)

## Open issues / tech debt
- Audit item A18 (Unity 6.6 installed, the stack pins 6.3) is closed: the project is 6000.3.25f1 (M0-04).
- Unity manifest pins below the editor's minimum or overridden in the lock (URP, test framework, navigation, timeline, ...): M0-66 audit.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- M0-07 is split into M0-07a (engine) and M0-07b (JSON file, server loading), one PR each: the owner's choice in the plan approval
  (build log BL-20261014-05, decision 1). Split plan tasks carry a letter; the commit id pattern allows it (`18`).
