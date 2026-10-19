# ADR-0008 — Labor model: recipes cost worker-seconds, and reassigning workers keeps a cycle's progress

- **Status:** Accepted
- **Date:** 2026-10-04 (decided with the kit; written down 2026-10-19) · **Task:** M0-18 · **Decision row:** `core-instructions/12-decisions.md` #G4

## Context
- The owner's pillar 3: workers are assigned to buildings, more workers mean faster production, and a higher level allows more workers
  (`core-instructions/22-game-mechanics.md`; decision G3). `22` adds: speed is proportional to workers, moving workers keeps the progress
  already made, and 0 workers pauses a building.
- Workers are a city-wide pool that houses provide, not an item. Players move them often: "reassign" is a step of the core loop (`22`).
- A cycle takes its inputs when it starts and delivers its outputs when it ends (`22`); the engine works in whole seconds and integers
  (ADR-0005, ADR-0007).
- From Era II, extra output is market power (`26`), so the same worker must never count twice (`07`, threat "use workers twice").
- The open question: what happens to a running cycle when its workers change?

## Decision
A recipe costs a fixed amount of **labor** in worker-seconds per cycle (decision G4, `04` §3). A cycle that starts takes the effective
labor of that moment (research reductions included) as `CycleLabor` and ends after `ceil(labor / workers)` seconds. When its worker count
changes, the labor done so far with the old count is taken off and a new segment starts: `LaborRemaining −= (t − SegmentStartedAt) × wOld`,
`SegmentStartedAt = t`, `PhaseEndsAt = t + ceil(LaborRemaining / wNew)`, or no end (frozen) with 0 workers. A worker-second therefore counts
in one building only: workers who leave a cycle slow it by exactly the work they no longer do. Details:
- **Pause = 0 workers:** a frozen cycle makes no progress and has no timer; its inputs stay taken and its outputs reserved (they count
  against the cap), and its stall reason is `NoWorkers`. There is no pause command (`05`: `assign-workers` with 0).
- **Exact:** the subtraction is exact in worker-seconds. The only rounding is the end, which is the first whole second at which the labor is
  done (`04` invariant 11), however many segments the cycle has: moving workers back and forth never gains a worker-second and never
  loses progress. The same count again starts a new segment but never moves the end (`04` §3).
- **Automatic only on construction:** a completed construction gets `min(free, MaxWorkers)` workers (decision 018); nothing else assigns
  workers. An upgrade keeps them assigned but idle, refunds a running or frozen cycle (decision 013), and its new limit applies when it
  completes (decision 034).
- **Stored per producing building:** `CycleLabor`, `LaborRemaining`, `SegmentStartedAt`, `PhaseEndsAt`, and the cycle's start in
  `PhaseStartedAt` (`04` §4), in the snapshot (`05`) and the database (`06`).

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| A worker change affects only the next cycle; the running one keeps the end it started with (the kit's alternative) | One start and one end per cycle | **Double use:** start a cycle with all the workers, move them to the next building and start it, and so on, and every building runs at full speed on the same workers | Breaks pillar 3 and is an exploit (KIT build log, decision 2) |
| Workers are locked to a running cycle until it ends | No double use; simple state | Reassigning waits for cycles to end; the worker stepper is refused while a cycle runs; a stalled chain can't be fixed at once | Poor play for no gain |
| A worker change restarts the cycle (progress lost; inputs lost or refunded) | Simple state | Punishes every adjustment; with a refund, reassigning becomes a free cancel | Unfair, and `22` says moving workers keeps the progress |
| Continuous output (fractions per second, scaled by workers) | No cycles to track | Fractions on every second (floats or fixed point); inputs and outputs no longer move per cycle (`22`); caps and stall reasons blur | Breaks the integer engine and the recipe model |

## Consequences
- **Easier:** speed follows the workers at every moment, and reassigning is always safe, so players can answer a stall at once. Pausing
  needs no extra command. Worker-seconds can be audited: invariant 11 is checked by a ledger that counts them on its own. The client
  draws exact progress from the snapshot: `done = cycleLabor − (laborRemaining − (clock − segmentStartedAt) × assignedWorkers)` (`05`).
- **Harder:** four fields per producing building to store, map and check (a restored building must end exactly where its labor says,
  M0-12). Every worker change recomputes the end, so mutations and the advance's timer queue must agree; the queue is built anew for
  every advance. A frozen cycle holds its inputs and reserved outputs until workers return or an upgrade refunds them.
- **Watch:** the worker attack tests (M0-32: moving workers between two buildings repeatedly never produces more than the labor allows;
  `07`); the stepper sends one absolute count after a debounce, so rapid taps make one segment (`09`); anything that changes workers later
  (Era V's automation managers, `22`) goes through the same segment update.
- **As built (M0-18):** `Building.StartCycle` and `Building.AssignWorkers`, `CityRules.AssignWorkers` (M0-08); the rule tests in
  `EngineRules/WorkerRuleTests` (the spec's example: 180 labor, 3 workers for 20 s leave 120, and 4 workers end 30 s later; freeze;
  resume; auto-staffing) and `Rules/AssignWorkersTests` (M0-08, M0-10); the exact-end check on restore (M0-12); invariant 11 with its own
  `LaborLedger` (M0-15); `ProgressDisplay` (M0-17). The HTTP command and its attack test come in M0-32.
- **Reversal:** expensive: the fields are in the snapshot (`05`) and the schema (`06`), and `22` promises players this behaviour.
