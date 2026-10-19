# ADR-0006 — Production cap per absence: timers always run, production stops after the cap (dormant tail)

- **Status:** Accepted
- **Date:** 2026-10-04 (decided with the kit, replacing an earlier "pause the whole city" cap; written down 2026-10-19) · **Task:** M0-18 ·
  **Decision row:** `core-instructions/12-decisions.md` #006

## Context
- Production runs in real time while the player is away, and timers always run (`core-instructions/22-game-mechanics.md`: "Production runs
  for at most the offline production cap per absence (8 h at the start, more via research). This rewards checking in a few times a day
  without punishing a weekend away").
- Research is the main progression and its nodes run for hours, later for days (`25`). A long research must finish while the player is away.
- A city is settled when a request touches it (ADR-0005), and a settle costs time in proportion to its events. Without a limit, one
  request after a long absence would simulate all of it: 64 producers with 10-second cycles over 30 days are about 16.6 million cycle ends.
- Coins have no storage cap, and the Trading Post turns goods into coins (`24`). Storage caps alone don't stop a chain that ends in coins.
- Every check-in should have something worth doing (`24`, goal 2), and absent players should not out-earn active ones.

## Decision
A settlement window `[T0, T0 + elapsed]` always moves the city clock by the full elapsed time, and every timer (construction, upgrade,
research) completes at its own second. **New production cycles start only while `t ≤ T0 + Rules.MaxOfflineProductionSeconds`**; after that
the city is *dormant*: running cycles finish and deliver, a construction that completes still gets its workers, nothing new starts. The
zero-length advance that ends every request opens a new window at once, so production restarts when the player returns (`04` §2, §7).
- The cap is content (`maxOfflineProductionSeconds`: 28,800 s = 8 h in M0, > 0 by content rule 1), not code. A research effect that raises it
  is planned (`OfflineProductionBonus`, `25`).
- It counts from the start of each window, which is the last request, so it limits the gap between check-ins, not the production per day.
- The settlement report says how long cycles could start: `productionSeconds = min(elapsed, cap)` (`04` §11), for the welcome-back report
  and, later, the "production cap reached" notification (`22`).
- Splitting time changes nothing within the cap, and never changes when a timer completes (`04` §10, invariants 2 and 3).

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| No cap: production runs for the whole absence | The simplest rule | Settle cost grows with the absence; a week away earns like a week of play; coins inflate | Unbounded cost and economy |
| Pause the whole city after the cap (the kit's first rule) | One rule for everything; bounded cost | A 2-day research doesn't finish during a week away, constructions stop too; contradicts "timers always run" | Replaced on 2026-10-04: long research must finish while away (KIT build log, decision 4) |
| A reduced rate while away (for example half speed) | Softer than a stop | The server can't tell "away" from "online" (it only sees requests); fractional rates on every cycle; cost still unbounded | No notion of online; unbounded |
| A cap in cycles per building instead of time | Fair per building | A different limit for every speed; hard to explain; timers need a rule of their own anyway | Players think in hours ("8 h") |
| Storage caps as the only limit | They exist already | Coins are uncapped, so a chain ending at the Trading Post never stops; full stores still cost events until they fill | Doesn't bound coins or cost |

## Consequences
- **Easier:** a settle never handles more than one cap of production plus the timers, whatever the absence (the worst case of `04` §12,
  about 37 ms). Research and construction never wait for the player. Offline earnings are bounded, so checking in matters, and the limit
  is a balance number that content can change.
- **Harder:** beyond the cap, the result depends on how time is split: a request in the middle of a long absence restarts production. So
  split invariance holds only within the cap, and the property tests model the tail separately (M0-15). A client that polls keeps
  production running, but no more than a player who checks in every 8 hours, so a script gains nothing a disciplined player doesn't
  (ADR-0001). Players who can't check in every 8 hours lose production overnight until research raises the cap.
- **Watch:** session patterns in the balance simulator (M0-67, for example "once a day" against "three times a day"); the welcome-back
  report showing `productionSeconds`; a new timer type must keep running in the tail (only cycle starts are capped).
- **As built (M0-18):** `Simulator.Advance` starts cycles only up to `productionUntil` (M0-09); the report's `ProductionSeconds`; the rule
  test `StartRuleTests.ProductionCap_DormantTail_StartsNoCycleWhileTimersStillComplete` (M0-10); `SettleCityTests` for 8 h, 8 h + 1 s and
  30 days (M0-13); timer invariance beyond the cap, split invariance within it, and invariant 12 in the property tests (M0-14, M0-15).
- **Reversal:** raising the cap is a content change (version bump and a balance run, `19`). Removing it needs another bound on settle cost
  (ADR-0005's fallback, a steady-state fast-forward) and an answer for unbounded coins.
