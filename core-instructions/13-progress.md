# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.
> This file is loaded into every session: Milestone-table notes are task ids and PR numbers only (≤ ~100 characters per row).

## Resume point
- **State:** IN PROGRESS          <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M0-64 · Ledger audit · branch `task/M0-64-ledger-audit` · build log `docs/build-log/2026/2026-11-12-04-M0-64.md`
- **Part / step:** step 4 of 4 (auditor + port; EF source; HTTP random-sequence test; docs), plan in the build log
- **Last completed:** M0-63 · Documentation completeness pass (PR #91, merged)
- **Next step:** step 4: docs (module docs Application and Infrastructure, `docs/testing/README.md`, `add-command.md`, `06`, `08`, CHANGELOG), then `/finish-task`.
- **Open questions for the owner:** M0-61: the stashed Unity Editor settings (`git stash list`), apply or drop; Follow-ups in its build log. CR-0008-T3: the `22` edit (decision 27) and the Follow-ups in its build log. CR-0008-T1: 8 Dependabot alerts on `main` (build log Follow-ups). M0-58: the [MANUAL] content-change check (`client/README.md`; no automated test covers the `GameRoot` rebuild). CR-0006-T1: `AGENTS.md` (protected) does not list the goods commands, say if I may add them; `test-goods.sh` is not in CI (workflow files are protected); the [MANUAL] try on the real dev database (Follow-ups). M0-53 and older questions: their build log entries (M0-53: the [MANUAL] look at the welcome-back report, decisions 5, 7, 9, 11). M0-55: the [MANUAL] look at the connection states (`client/README.md`). CR-0007-T1: the [MANUAL] look at the new UI (`client/README.md`); the status label may be removed (build log follow-ups).
- **Known broken / do not touch:** nothing.
  CR-0008-T1: decision 38 lowered two property coverage floors (reported in the PR, G-Tests). (`ConnectionMonitorTests.Tick_WithNoPollDue_AllocatesNothing` failed once in a cold full run, flaky; build log M0-55; `PerformanceBudgetTests` fails when the machine is heavily loaded, re-run it alone: CR-0008-T1 build log, C3)
- **Verify with:** `bash scripts/dev/check-docs.sh --base origin/main` (as CI) · `bash scripts/dev/docs-site.sh build` · `dotnet build server/CityBuilder.slnx` ·
  `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` · `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (Editor closed)

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` · tasks done: 67 / 88

| Area | State | Notes |
|---|---|---|
| Repo, kit, hooks, CI | done | M0-01 (PR #4, #5) |
| Shared engine (content, research, labor, invariants, performance) | done | M0-03 to M0-18 (PR #7 to #26); worst case 308 ms per advance in Release (`04` §12) |
| Server skeleton | done | M0-02 (PR #6) |
| Persistence (EF + PostgreSQL) | done | M0-19 to M0-24 (PR #31 to #36) |
| Auth + command pipeline | in progress | M0-25 to M0-30 (PR #37 to #42) |
| Commands: place / upgrade / assign workers / start research | done | M0-29, M0-31 to M0-33 (PR #41, #43 to #45); `docs/how-to/add-command.md` |
| Unity skeleton + networking | in progress | M0-04, M0-05, M0-37 to M0-42 (PR #8, #12, #49 to #57); `docs/testing/LOCAL-E2E.md` |
| Unity city view + placement | in progress | M0-43 to M0-48 (PR #58 to #63) |
| Era I art (CR-0005) | in progress | T1 to T4, M0-50 (PR #66 to #70) |
| Inspector, worker stepper, research screen, HUD | in progress | M0-49, M0-51 to M0-55, CR-0007-T1 (PR #64, #70 to #73, #76, #79) |
| Dev tooling (CR-0006) | done | goods for the `devtest` account (CR-0006-T1) |
| Resilience (limits, retries, logging) | in progress | M0-34, M0-35, M0-57 to M0-59 (PR #46, #47, #81, #82, #84) |
| Docs automation (DocTools) + docs completeness | in progress | CR-0002, CR-0003 (PR #27, #29); M0-61 (PR #89); M0-63 (PR #91) |
| Logistics (CR-0008) | done | T1 to T3 (PR #86, #87, #88) |
| Balance simulator | not started | |
| E2E / security / concurrency suites | in progress | M0-36, M0-56, M0-60 (PR #48, #80, #85); `docs/testing/KILL-SERVER-CHECK.md` |
| Dev build + M0 close-out | not started | |

Per-task detail (what each PR added): `docs/plan/M0-PLAN.md`, `docs/build-log/`, `git log`.

States: `not started` · `in progress` · `done` · `blocked (reason)`

## Commands
The command list is in `AGENTS.md` → Commands (marks `[Mx-NN]` = not real yet); the "since" task for each is in `docs/getting-started.md` → Run.
Not yet: BalanceSim (M0-67).

## Open issues / tech debt
- Audit item A18 (Unity 6.6 installed, the stack pins 6.3) is closed: the project is 6000.3.25f1 (M0-04).
- Unity manifest pins below the editor's minimum or overridden in the lock (URP, test framework, navigation, timeline, ...): M0-66 audit.
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- M0-07 is split into M0-07a (engine) and M0-07b (JSON file, server loading), one PR each: the owner's choice in the plan approval
  (build log BL-20261014-05, decision 1). Split plan tasks carry a letter; the commit id pattern allows it (`18`).
- CR-0008 (logistics, approved 2026-11-02) adds three tasks between M0-60 and M0-61 (`CR-0008-T1` … `T3`, board rows "10+"): 88 on the
  board. Its design changes `22`, `04`, `05`, `06`, `23`; M0-61 onward cover the new model (plan §0.00, §7).
