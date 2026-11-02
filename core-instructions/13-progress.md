# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.

## Resume point
- **State:** IN PROGRESS   <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-56 · Proving idempotent retries (`[O]`, Opus 5.5)
- **Branch:** `task/M0-56-idempotent-retries`
- **Build log entry:** `docs/build-log/2026/2026-11-02-01-M0-56.md`
- **Last green commit:** the head of the branch
- **Done steps:** write-ahead
- **Next step:** step 1, `Commands/LostResponseDatabaseTests` (plan in the build log)
- **Open questions for the owner:** CR-0006-T1: `AGENTS.md` (protected) does not list the goods commands, say if I may add them; `test-goods.sh` is not in CI (workflow files are protected); the [MANUAL] try on the real dev database (Follow-ups). M0-53 and older questions: their build log entries (M0-53: the [MANUAL] look at the welcome-back report, decisions 5, 7, 9, 11). M0-54: the dev database still runs the fast clock until the owner does step 7 of the waiting-loop run (`LOCAL-E2E.md`). M0-55: the [MANUAL] look at the connection states (`client/README.md`). CR-0007-T1: the [MANUAL] look at the new UI (`client/README.md`); the status label may be removed (build log follow-ups).
- **Known broken / do not touch:** — (`ConnectionMonitorTests.Tick_WithNoPollDue_AllocatesNothing` failed once in a cold full run, flaky; build log M0-55)
- **Verify with:** `bash scripts/dev/check-docs.sh` · `bash scripts/dev/docs-site.sh build` · `dotnet build server/CityBuilder.slnx` ·
  `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` · `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (Editor closed)

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 55 / 85

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 and the audit fixes merged (PR #4, #5); A18 can be closed in M0-04 |
| Shared engine: content, research, labor | done | M0-03 merged (PR #7); the content model and its validation (M0-07a) merged (PR #14); the JSON file and server loading (M0-07b) merged (PR #15); city state and mutations (M0-08) merged (PR #16); `Simulator.Advance` (M0-09) merged (PR #17); rule tests (M0-10) merged (PR #18); golden scenario #1 (M0-11) merged (PR #19); engine review (M0-12) merged (PR #20) |
| Shared engine: invariants, performance | done | settlement window and production cap (M0-13) merged (PR #21); property test harness (M0-14, with engine fix CR-0001) merged (PR #22); invariant properties (M0-15) merged (PR #23); performance budget M0-16 merged (PR #24; worst case ~37 ms per advance in Release); display helpers M0-17 merged (PR #25); spec sync and ADRs M0-18 merged (PR #26) |
| Server skeleton | done | M0-02 merged (PR #6) |
| Persistence (EF + PostgreSQL) | done | M0-19 (EF Core model, migrations, Docker) merged (PR #31); repository and mapping M0-20 merged (PR #32); Testcontainers M0-21 merged (PR #33); locking M0-22 merged (PR #34); settle M0-23 merged (PR #35); M0-24 merged (PR #36) |
| Auth + command pipeline | in progress | dev auth M0-25 merged (PR #37); the pipeline M0-26 merged (PR #38); its integration tests M0-27 merged (PR #39); content and city endpoints M0-28 merged (PR #40); PlaceBuilding M0-29 merged (PR #41); attack review M0-30 merged (PR #42) |
| Commands: place / upgrade / assign workers / start research | done | place-building M0-29 merged (PR #41; `docs/how-to/add-command.md`); upgrade-building M0-31 merged (PR #43; OD-13); assign-workers M0-32 merged (PR #44); start-research M0-33 merged (PR #45): the four commands of M0 |
| Unity skeleton + networking | in progress | M0-04 (project, assemblies, GameRoot) and M0-05 (Unity tests, `test-unity.sh`) merged (PR #8, #12); API client and DTOs M0-37 merged (PR #49); auth and content on the client M0-38 merged (PR #50); the city store and display prediction M0-39 merged (PR #51); the command sender M0-40 merged (PR #52); polling and lifecycle M0-41 merged (PR #53); end-to-end check M0-42 merged (PR #57; `docs/testing/LOCAL-E2E.md`) |
| Unity city view + placement | in progress | grid, ground and camera M0-43 merged (PR #58; `CityWorld`, drag/pinch/wheel/tap, tile selection); building views M0-44 merged (PR #59; `CityView`, `BuildingLooks`, `BuildingViewFactory`); build menu M0-45 merged (PR #60; `BuildMenu`, `BuildCardModel`, `BuildMenuModel`; a press over the UI stays off the ground); placement flow M0-46 merged (PR #61; `PlacementFlow`, `PlacementFlowModel`, `PlacementBar`, `PlacementGhosts`); feedback for every result M0-47 merged (PR #62; `FeedbackMessages`, `ToastQueue`, `ToastHost`, `CommandFeedback`, `CommandSender.Completed`); client review M0-48 merged (PR #63; `ConnectionStatusView`) |
| Era I art (CR-0005) | in progress | T1 `art/` adopted, merged (PR #66); T2 (first import, `ArtImportPostprocessor`) merged (PR #67); T3 (models in the city) merged (PR #68); T4 (icons) merged (PR #69); M0-50 (building status, uses the anchors and the site) merged (PR #70) |
| Inspector, worker stepper, research screen, HUD | in progress | connection states M0-55 merged (`CommandAvailability`, `ConnectionBannerModel`, `ConnectionBanner`, `ConnectionFlow`); UI visual pass CR-0007-T1 merged (PR #79) (`Common.uss`, the panel scaled by height); waiting-loop review M0-54 merged (PR #76) (`PredictionWatch`; no mismatch in the owner's run at scale 60); welcome-back report M0-53 merged (PR #73) (`WelcomeBackModel`, `WelcomeBackPanel`, `WelcomeBackFlow`, `SettlementSummary`, `ResearchEffectText`); research screen M0-52 merged (PR #72) (`ResearchScreenModel`, `ResearchNodeModel`, `ResearchScreen`, `ResearchFlow`); building inspector with the worker stepper M0-51 merged (PR #71) (`BuildingInspectorModel`, `BuildingInspector`, `InspectorFlow`, `ItemText`); HUD M0-49 merged (PR #64) (`HudView`, `HudModel`, `ItemChipModel`, `ResearchRingModel`, `PerHourText`); building status visuals M0-50 merged (PR #70) (`BuildingStatusModel`, `BuildingStatusView`, `StatusArt`, `StalledTint`) |
| Dev tooling (CR-0006) | done | server panel goods for the `devtest` account: `server-panel/goods.sql`, `panel.sh goods`, tests (CR-0006-T1) |
| Resilience (limits, retries, logging) | in progress | error catalogue, global handler, rate limits and input limits M0-34 merged (PR #46); logging, correlation and the dev time scale M0-35 merged (PR #47) |
| Docs automation (DocTools) + docs completeness | in progress | handbook, Pages, docs-as-code checks and a full audit: CR-0002 done (PR #27); code documentation CR-0003 done (PR #29); DocTools is M0-61 |
| Balance simulator | not started | |
| E2E / security / concurrency suites | in progress | server golden E2E over HTTP M0-36 merged (PR #48) |
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
