# ADR-0005 — Settle on access: a deterministic event simulation, run when a request touches the city

- **Status:** Accepted
- **Date:** 2026-10-04 (decided with the kit; written down 2026-10-19) · **Task:** M0-18 · **Decision row:** `core-instructions/12-decisions.md` #005

## Context
- Production runs in real time, also while the player is away, and timers always run (`core-instructions/22-game-mechanics.md`, pillar 2
  and "offline"). Most cities are left alone for hours: players check in a few times a day.
- The server is the authority and its clock the only clock (ADR-0001). Every write row-locks only the acting player's city (decision 008).
- There is no realtime channel; the client polls `GET /city` every 2 minutes in the foreground (ADR-0003).
- The client must predict the same numbers between snapshots (decision 007, ADR-0007), and offline progress must not depend on when the
  server happened to look.
- From Era II, other players' actions (market fills) must reach a city whose owner is away (`26`, decision 016).

## Decision
No city runs in the background. A city's state is brought up to date only when a request touches it: inside the request's transaction,
after the row lock, the server takes the window from the last settled second to now and `Simulator.Advance` computes exactly what happened
in it. `GET /city` settles too, so it is safe to repeat but not free of side effects (`05`). Details:
- **Discrete events:** the engine jumps from one timer end to the next (research, constructions, upgrades, cycles) and changes nothing in
  between, so its cost grows with the events, not with the seconds (`04` §7). Integers and a fixed order at each second make it deterministic.
- **Whole seconds:** `SettlementWindow.Compute` floors now to a whole server second, and each window starts where the last one ended, so
  dropped fractions are counted later, never lost. A server clock that went back gives an empty window and a warning (`04` §1).
- **Request lifecycle** (`03`): lock → (idempotency check) → settle → (mutation) → zero-length advance → write → commit. The settlement is
  committed even when the mutation is rejected (decision 014), and the zero-length advance leaves a canonical state (`04` §7).
- **Bounded cost:** production starts only during the first `MaxOfflineProductionSeconds` of a window (ADR-0006), so one settle handles at
  most that much production plus a few timers (`04` §12).
- **Other players** (M2): deliveries wait in a mailbox and enter the receiver's next settle as timed events (`04` §13), so no transaction
  touches two cities.

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| A "claim" command: the player collects what was produced | One clear moment that writes | Taps become progress, which `22` rules out ("taps are decisions, never progress"); when to claim becomes a tactic; the elapsed time must still be computed | Rejected with the kit (KIT build log, decision 1); `05` has no claim command |
| A background ticker advances and writes every city every few minutes | Stored state always fresh; reads don't write | Cost grows with cities × time, even for cities nobody looks at; the ticker and requests compete for the same row lock | Cost and moving parts with no gain a player sees; decision 005's fallback rules it out |
| Fixed time steps (simulate every second) | The simplest loop | 8 h is 28,800 steps over every building, and in a typical city most of them change nothing | Events give the same result far cheaper |
| Settle only on commands; `GET /city` computes a projection without writing | A side-effect-free read | Every poll of an idle city simulates the whole window since the last write again; two code paths (projection and authority); the welcome-back report and the ledger have no single settlement | One path; a poll every 2 minutes then simulates only those 2 minutes |
| A scheduled job per timer end settles that city | State fresh at every event | One job per event (a busy city has thousands per hour); a scheduler to run and monitor; exactness between events still needs a settle | Not needed in M0; notifications (`22`, later) can be planned from the end times a settle stores |
| The client simulates and the server checks | No server simulation | Breaks the authority | Rejected by ADR-0001 |

## Consequences
- **Easier:** idle cities cost nothing. Offline progress is exact to the second and doesn't depend on how often the server looked: timers
  complete at the same second however time is split, and production too within the cap (`04` §10, invariants 2 and 3, property-tested).
  Online and offline use one code path, and the client predicts with the same engine. Tests drive time with `FakeTimeProvider`.
- **Harder:** every request writes the city and its ledger rows in a transaction, polls included. The settle's cost lands on the request
  (budget < 50 ms in the worst case, `04` §12). The stored state is only as fresh as the last request, so anything that reads many cities at
  once (rankings, analytics, admin tools) must settle them or use only values that don't change with time.
- **Watch:** settle duration and event counts in the logs (M0-35); the write load of polling (ADR-0002, ADR-0003); `state_version` rises
  only when the state changed (M0-23); the dev time scale changes the server clock only, never the engine (`04` §1, M0-35).
- **As built (M0-18):** `SettlementWindow.Compute` and the `SettleCity` skeleton with an in-memory repository (M0-13); `Simulator.Advance`
  (M0-09) with a timer queue since M0-16, measured at about 37 ms for the worst case in Release. Since then: the transaction (M0-22); the
  closing zero-length advance, the ledger rows and a save only when something changed (M0-23). Still to come: the warning for a clock
  behind, logged by the `GET /city` endpoint (M0-28), and the commands (M0-26 on).
- **Fallback (agreed in advance, `12`):** if settle cost becomes a problem (not expected in M0), add a steady-state fast-forward inside the
  engine that computes a stretch of identical cycles at once, never a background ticker that writes every city. Record the switch in `12`.
- **Reversal:** a ticker could call the same settle later without engine changes, at the costs above. Dropping lazy settlement is not planned.
