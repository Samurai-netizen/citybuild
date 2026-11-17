# M0 handoff

M0, the technical vertical slice of Era I, is complete (M0-84, release `0.1.0 — M0` in `CHANGELOG.md`). This page lets the next coder or
the owner start M1 cold: what exists, how to run it, what the server guarantees, what is known to be wrong, and what comes next.

## What exists
- **A shared deterministic engine** (`shared/Simulation/`): integers only, settle-on-access over timed events. It covers the labor model,
  research, builder slots, building stores and the purse, projects waiting for deliveries, roads and the path search, manual trips,
  couriers on routes and courier stamina. It runs on the server for authority and in the client for display (ADR-0005, ADR-0007).
- **The server** (`server/`, .NET 10, PostgreSQL 18): dev login, `GET /content`, `GET /city`, and 12 commands (place, upgrade, assign
  workers, start research, three road commands, start trip, set route, re-energize couriers, cancel delivery, set paused). Each command
  is idempotent by `requestId` and locks only the player's own city. Every resource change writes a ledger row (`docs/architecture/overview.md`).
- **The Unity client** (`client/CityBuilder/`, Unity 6000.3.25f1): the city on the 8×8 grid with the Era I art, the build menu and
  placement, the inspector with its worker stepper and upgrade, the research screen, the HUD, road mode, trips, the courier station and
  route editor, walkers, the welcome-back report and the connection banner. The client predicts for display and snaps to each snapshot.
- **Tools:** `tools/BalanceSim` (strategies × check-in patterns, `docs/balance/REPORT-mvp-1.md`), `tools/DocTools` (the generated
  reference in `docs/reference/generated/`) and `server-panel/` (the local database and API).
- **Content** `mvp-3` (`server/content/`): 3 goods, the Era I buildings at levels 1–2, 6 research nodes and road levels 1–2.
- **Process:** the build log for every task (`docs/build-log/`), 10 ADRs, CRs, the plan boards, git hooks, CI with the docs checks and
  the handbook site.

## How to run everything
- Set up a clone: `docs/getting-started.md` → "A new clone". Run the server, the client and the tests: the same page → Run.
- Make a development build (macOS, Windows, Android, iOS) and run it against your server: `docs/BUILD.md`.
- Tests: `bash scripts/dev/test.sh <area> [filter]` in the loop, and `bash scripts/dev/quality.sh` for everything CI runs
  (`core-instructions/08-testing-strategy.md`). Manual runs: `docs/testing/` (local end to end, logistics, the Unity smoke test,
  killing the server).

## What the server guarantees, and the limits
Details and the tests behind each line are in `docs/security/MVP-SECURITY-BOUNDARIES.md`, and the whole chain from a tap to the screen is
in `docs/MVP-AUDIT.md`.
- The client only proposes: it sends ids and intents, never amounts, costs, times or player ids (`ClientInputInventoryTests`).
- Server time only. Each request settles the city to now inside its transaction. A command never applies twice, and every write locks
  only the acting player's city.
- Every `/api/v1` endpoint but the dev login requires a token (`Security_EveryApiEndpoint_RequiresAToken`). Rate limits apply per
  player, and a refusal never locks the city.
- The ledger reconciles with the stores, the loads and the purse (`LedgerAuditDatabaseTests`). State survives a restart.
- **Limits:** dev login only (no accounts, no revocation, 12 h tokens); no device attestation, so a script can poll to keep couriers
  awake; rate limits are in memory and per instance; no TLS on the server; one server instance only.

## Known issues
| Id | Issue | Severity |
|---|---|---|
| M0-C-4 | A command sent with an expired token is refused once: `CommandSender` does not log in again on a 401 | P2 |
| M0-C-2 | On an empty database, EF logs history-table error lines before the first migration (harmless) | P3 |
| M0-D A | `COURIER_BUSY` on assign-workers has no HTTP test (the engine test and the error mapping cover it) | P3 |
| M0-D B | The Android and iOS application ids are the Unity template's | P3, before a store build |
| M0-D C | Not verified: the Windows and iOS builds, the APK on a phone, the city in a player on screen (owner, `docs/BUILD.md`) | open |
| — | Flaky when the machine is loaded: `PerformanceBudgetTests`, once `ConnectionMonitorTests.Tick_WithNoPollDue_AllocatesNothing` | P3 |
| — | GitHub reports 6 Dependabot alerts on `main`, all in `website/package-lock.json` (the handbook site); not triaged in M0 | open |

Open owner questions: `docs/owner-queue.md`.

## M1 backlog
M1 is "Era I complete": the first 3–5 days worth playing (`core-instructions/10-milestones.md`). The proposed plan is
`docs/plan/M1-PLAN.md` (CR-0011, Proposed), which holds the task specs. Approved and waiting: CR-0012 (courier stamina as an Era I
research), which runs first. Ordered by value, each item can become a CR or is already an M1 task:

1. **Design lock for Era I** (M1-01): the goals, names, style guide and master asset list that every later item reads.
2. **Full Era I content** (M1-02): about 8 goods (wood, stone, grain, flour, bread), about 10 buildings at levels 1–4, about 12 research
   nodes in 3–4 branches, and the effect types max level, land and builder slots (`25`).
3. **The map and land** (M1-03): terrain, placement rules and Land Survey expansions beyond the 8×8 grid.
4. **Guest accounts** (M1-12): device-bound accounts in place of the dev login, with recovery and deletion, refresh tokens and a
   re-login on 401 in `CommandSender` (closes M0-C-4).
5. **The Chronicle** (M1-04): goals and chapters with rewards, so a player always knows what to do next.
6. **Tutorial and first session** (M1-10): the first 15 minutes guided (it replaces the skipped M0-71).
7. **Hosting and operations** (M1-13): a hosted server with managed PostgreSQL, TLS in front, logs and backups; the EF lines on an empty
   database cleaned up (M0-C-2).
8. **Android release build** (M1-14): real application ids, signing, a release build that refuses `http://`, and a performance budget
   on a phone.
9. **Notifications and return hooks** (M1-11): construction and research done, a store full, couriers resting.
10. **Merchant orders and the daily caravan** (M1-05): a coin sink and a reason to plan production.
11. **Game feel and art** (M1-07, M1-08, M1-09): Russian localization, the UI skin, audio, animation and the Era I art.
12. **Town management** (M1-06): name the town, move and demolish buildings, decorations, cancel research.
13. **Balance for a week of Era I** (M1-15): the simulator matched to playtest pacing within ±25 %.
14. **Cross-system suites** (M1-16): the attack, concurrency and end-to-end suites over the new systems, with the `COURIER_BUSY` HTTP
    test; then the closed alpha and its fixes (M1-17, M1-18).

## M2 readiness
What the multiplayer era needs, and where M0 already leaves room (`docs/ARCHITECTURE-FITNESS-REVIEW.md` → M2 readiness,
`docs/security/MVP-SECURITY-BOUNDARIES.md` → Before the market):
- **Era transition:** era is data (`CityState.Era`, content per era, no era numbers in engine code). Needed: a mutator for the era, the
  check when a research completes, and the era charter grant.
- **Mailbox events:** `Advance` gathers timed events from a list of `IEventSource`s. A mailbox is one more source, and the only channel
  between players. Needed: the table, which store a delivery enters (`26` rule 4), and pending deliveries passed in by the settle.
- **Accounts:** platform sign-in on top of the M1 guest accounts, session revocation, an account-age gate.
- **Market:** the order book, escrow and idempotent orders through the mailbox with a ledger row on both sides, rate limits shared by
  all instances, the ledger auditor on a schedule, a per-item kill switch, alt-account defences, and attack and concurrency suites
  for the market endpoints (`26`).

Last updated: M0-84 (M0-D)
