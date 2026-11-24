# Progress and Resume Point (living file)

> The AI updates this file at **every checkpoint**, not only at the end of a task (`16` §D).
> Keep it ≤ 80 lines: overwrite old details. History lives in the build log and git.
> This file is loaded into every session: Milestone-table notes are task ids and PR numbers only (≤ ~100 characters per row).

## Resume point
- **State:** IN PROGRESS <!-- IDLE | IN PROGRESS | BLOCKED | AWAITING OWNER -->
- **Task:** M1-03 The map: terrain, placement rules, land expansion · branch `task/M1-03-map-terrain` · log `docs/build-log/2026/2026-11-24-01-M1-03.md`
- **Part / step:** part B nearly done (last commit see git log); left: smoke.http on map tiles, docs 05/07/04 §8 + Docs impact rows, `/checkpoint`; then A2, then C
- **Last completed:** A1: map, terrain rules, bridge, walks, `NO_PATH`, reset migration, docs 04/05/06/28/29.
- **Next step:** update `docs/api/smoke.http` (coordinates onto area A, e.g. +4,+4; woodcutter beside forest (4,3)), docs per build log "Part B progress", then `/checkpoint` part B.
- **Open questions for the owner:** 13 rows in `docs/owner-queue.md`. The hosting provider (in M1-13).
- **Known broken / do not touch:**   the owner's local Unity edits (`client/CityBuilder/Assets/Settings/*.asset`, `ProjectSettings/ProjectSettings.asset`,
  `UnityConnectSettings.asset`): leave them uncommitted and don't report them (owner, 2026-11-18). They are hidden with
  `git update-index --skip-worktree` in the owner's clone (undo: `--no-skip-worktree`). The untracked `docs/assets/ready-assets/` (asset
  deliveries for M1-03 … M1-17): leave it, never `git add docs` whole (owner, 2026-11-24).
  CR-0008-T1: decision 38 lowered two property coverage floors (reported in the PR, G-Tests). (`ConnectionMonitorTests.Tick_WithNoPollDue_AllocatesNothing` failed once in a cold full run, flaky; build log M0-55; `PerformanceBudgetTests` fails when the machine is heavily loaded, re-run it alone: CR-0008-T1 build log, C3)
- **Verify with:** in the loop `bash scripts/dev/test.sh <area> [filter]`, at a checkpoint `bash scripts/dev/test.sh changed`, at the finish
  `bash scripts/dev/quality.sh` (as CI; `08` → Test tiers) · `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (Editor closed)

## Milestone
**M0 — vertical slice of Era I** · board: `docs/plan/M0-PLAN.md` (remaining tasks grouped in `docs/plan/M1-PLAN.md` §3) · tasks done: 87 / 88 (M0-71 skipped) · next milestone: `docs/plan/M1-PLAN.md` (approved 2026-11-23, CR-0011)

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
| Workflow v2 (CR-0010) | in progress | CR-0010-T1, lands with M0-A: tests first, `test.sh`, token budget, asset requests (`27`) |
| Logistics (CR-0008) | done | T1 to T3 (PR #86, #87, #88) |
| Balance simulator | done | M0-67 (PR #95); M0-68 to M0-70 (PR #98) |
| E2E / security / concurrency suites | done | M0-36, M0-56, M0-60 (PR #48, #80, #85); M0-64 to M0-66 (PR #92 to #94); M0-B (PR #99); M0-C (PR #100) |
| Dev build + M0 close-out | done | M0-D (PR #101) |
| Player metrics (CR-0009) | in progress | CR-0009-T1 (PR #102); T2 (report) in review |

Per-task detail (what each PR added): `docs/plan/M0-PLAN.md`, `docs/build-log/`, `git log`.

States: `not started` · `in progress` · `done` · `blocked (reason)`

## Commands
Tests: `bash scripts/dev/test.sh` (CR-0010). The command list is in `AGENTS.md` → Commands (marks `[Mx-NN]` = not real yet); the "since" task for each is in `docs/getting-started.md` → Run.
All commands exist.

## Open issues / tech debt
- M0-C-1 P1 route editor unreadable: fixed (layout rework), owner-checked. M0-C-2 P3 EF history-table error lines on an empty database.
- M0-C-3 P1 a store above its limit (returned loads): fixed, stores never overflow (decision 2), owner-checked. M0-C-4 P2 a command with an expired token is refused once (no re-login in CommandSender): follow-up.
- M0-C-5 P2 send to a resting courier said "Sent", nothing moved: fixed, a popup asks to re-energize (decision 3), owner-checked.
- Audit item A18 (Unity 6.6 installed, the stack pins 6.3) is closed: the project is 6000.3.25f1 (M0-04).
- Other kit wording items with their owner tasks: M0-01 build log → Follow-ups.

## Deviations from the plan
- M0-07 is split into M0-07a (engine) and M0-07b (JSON file, server loading), one PR each: the owner's choice in the plan approval
  (build log BL-20261014-05, decision 1). Split plan tasks carry a letter; the commit id pattern allows it (`18`).
- CR-0008 (logistics, approved 2026-11-02) adds three tasks between M0-60 and M0-61 (`CR-0008-T1` … `T3`, board rows "10+"): 88 on the
  board. Its design changes `22`, `04`, `05`, `06`, `23`; M0-61 onward cover the new model (plan §0.00, §7).
- The owner approved the M0 regrouping of `docs/plan/M1-PLAN.md` §3 on 2026-11-17 (M0-A … M0-D, M0-71 skipped): one task per group,
  one branch and PR each; the M0 specs stay in `docs/plan/M0-PLAN.md` (build log BL-20261116-02, Session 2).
