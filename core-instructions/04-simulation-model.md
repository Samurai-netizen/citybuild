# Simulation Model — the production engine

The core mechanic is **waiting while your workers run production chains**. This file is the spec
for the engine that computes what happened during that waiting. It lives in `shared/Simulation/`,
runs authoritatively on the server, and runs on the client for display only.

## 1. Time
- Simulation time is **integer seconds** (`long`) on a per-city **city clock**.
- The server converts `now` (`TimeProvider`, UTC, floored to whole seconds) into
  `elapsed = nowSec − city.LastSettledUnixSec`. `elapsed < 0` → 0, keep `LastSettledUnixSec`, log a warning.
- Every settlement advances the clock by the **full** elapsed time.
- Client time is never an input.
- Development servers may run a scaled `TimeProvider` (`Simulation:DevTimeScale`, e.g. 60 = one hour per minute).
  The engine doesn't know about it; startup refuses any scale ≠ 1 outside Development.

## 2. Offline production cap (dormant tail)
A settlement covers the window `[T0, T0 + elapsed]`.
- **Timers** (construction, upgrades, research) run for the whole window. A 2-day research finishes on time
  even if the player was away for a week.
- **Production cycles may start only while `t ≤ T0 + Rules.MaxOfflineProductionSeconds`** (8 h in M0).
  After that the city is *dormant*: running cycles finish, no new cycle starts.
- The next request opens a new window, and its closing zero-length advance restarts production immediately.
- Why: this bounds simulation cost and gives players a reason to check in, while never delaying research.

## 3. Workers and labor
- Completed Houses provide workers (per level). `free = provided − Σ assigned`.
- Every producer level has `MaxWorkers`. `0 ≤ assigned ≤ MaxWorkers`. Placing a building never needs workers.
- A recipe costs **labor** in worker-seconds per cycle. Effective labor =
  `ceil(Labor × (1000 − Σ reductions‰) / 1000)`, where the reductions come from completed research.
- With `w` workers, a cycle takes `ceil(remainingLabor / w)` seconds. More workers → faster. `w = 0` → frozen.
- **Changing workers keeps progress:** at time `t`, `done = (t − SegmentStartedAt) × wOld`,
  `LaborRemaining = max(0, LaborRemaining − done)`, `SegmentStartedAt = t`,
  `PhaseEndsAt = t + ceil(LaborRemaining / wNew)`, or `null` (frozen) if `wNew = 0`.
- **Auto-staffing:** when construction completes, the building gets `min(free, MaxWorkers)` workers.
  Nothing else is assigned automatically. Assigning 0 workers is how a player pauses a building.
- Houses have no recipe and take no workers. During an upgrade, workers stay assigned but do nothing.

## 4. Building phases

| Phase | Meaning | Timer fields |
|---|---|---|
| `Constructing` | Placed, not usable yet; uses a builder slot | `PhaseStartedAt`, `PhaseEndsAt` |
| `Upgrading` | No production; uses a builder slot; level rises at end | `PhaseStartedAt`, `PhaseEndsAt`, `TargetLevel` |
| `Idle` | Ready, not producing | — |
| `Producing` | Inputs consumed, outputs reserved | `CycleLabor`, `LaborRemaining`, `SegmentStartedAt`, `PhaseEndsAt` (null if frozen) |

**Stall reason** (derived, never stored), shown to players. The first match wins:
`NoRecipe` (houses) · `NoWorkers` (idle or frozen cycle) · `MissingInput:item` · `StorageFull:item`.

## 5. Research
- One research slot in M0. `StartResearch` pays the full cost up front and sets `ResearchEndsAt = clock + Duration`.
  Research needs no workers.
- On completion, the node is added to `CompletedResearch` and its effects apply at that moment:
  `UnlockBuilding(type)`, `LaborReduction(type, ‰)`, `StorageCapBonus(item, +n)` (M0 set; more types in `25`).
- Effects apply to cycles that start after completion. Running cycles keep their labor.
- **Era transition** (designed, not in M0 content): when every research node of the current era is
  completed, `Era += 1` at that moment.

## 6. Rules
**Start rule:** a producer starts a cycle at time `t` if it is `Idle`, has `w ≥ 1`, production is allowed
at `t` (§2), every input is in stock, and every capped output fits (`stock + reserved + qty ≤ cap`).
On start: subtract inputs, reserve outputs, `CycleLabor = LaborRemaining = effective labor`,
`SegmentStartedAt = t`, `PhaseEndsAt = t + ceil(labor / w)`.
**Completion rule** at `t == PhaseEndsAt`: `Producing` → outputs move from reserved to stock, then `Idle`.
`Constructing` → `Idle` plus auto-staffing. `Upgrading` → `Level = TargetLevel`, then `Idle`.
**Ordering at the same `t`:** (1) research completion → (2) building completions in ascending `Id` →
(3) starts in ascending `Id`. Each step is fully applied before the next one. This order is visible to players.

## 7. The algorithm

```
Advance(city, content, elapsedSeconds) -> SettlementReport
  T0 = city.Clock; target = T0 + max(elapsedSeconds, 0)
  productionUntil = T0 + content.Rules.MaxOfflineProductionSeconds
  t = T0
  loop:
    if t <= productionUntil: StartEligible(city, t)        # ascending Id
    next = min(city.ResearchEndsAt, non-null PhaseEndsAt of all buildings)
    if next is none or next > target: break
    t = next
    CompleteDue(city, t)                                   # research, then buildings by Id
  city.Clock = target
```

`Advance(city, 0)` (a **zero-length advance**) only runs `StartEligible` at the current clock.
Every mutation is followed by a zero-length advance, so the returned state is always canonical.

## 8. Mutations (all happen at `city.Clock`, after settling)

| Mutation | Checks (error code) | Effect |
|---|---|---|
| PlaceBuilding | type known (`UNKNOWN_BUILDING_TYPE`), unlocked (`BUILDING_LOCKED`), in bounds (`OUT_OF_BOUNDS`), tile free (`TILE_OCCUPIED`), free builder (`NO_FREE_BUILDER`), cost (`INSUFFICIENT_RESOURCE`) | pay cost, `Id = NextBuildingId++`, `Constructing` |
| UpgradeBuilding | exists (`BUILDING_NOT_FOUND`), Idle/Producing (`BUILDING_BUSY`), level < max (`MAX_LEVEL`), free builder, cost (checked **before** the refund) | refund the running cycle's inputs, release reservation, pay, `Upgrading` |
| AssignWorkers | exists, has a recipe (`INVALID_ARGUMENT`), not Constructing (`BUILDING_BUSY`), `0 ≤ n ≤ MaxWorkers` (`WORKER_LIMIT`), enough free workers (`INSUFFICIENT_WORKERS`) | set `n`, labor segment update (§3) |
| StartResearch | known (`UNKNOWN_RESEARCH`), not done (`RESEARCH_COMPLETED`), slot free (`RESEARCH_IN_PROGRESS`), prerequisites + current era (`RESEARCH_LOCKED`), cost | pay, start timer |

**Builders busy** = count of `Constructing` + `Upgrading`. **Refunds may exceed storage caps**: caps only limit production.

## 9. Determinism rules (shared code)
- Integers only (`long` quantities, seconds and labor; `int` ids and permille). No `float`, `double` or `decimal`.
- Divisions that produce durations use ceiling division on non-negative integers. Nothing else divides.
- Never depend on `Dictionary`/`HashSet` enumeration order. Iterate sorted ids or arrays.
- No `DateTime`, no randomness, no static mutable state, no I/O inside the engine.
- Use `checked` arithmetic. Overflow is a bug, not a wrap.

## 10. Invariants (each has a test)
1. Same input → structurally identical output and report.
2. **Split invariance:** advancing `a` then `b` equals advancing `a + b` whenever `a + b ≤` the production cap.
3. **Timer invariance:** construction, upgrade and research completion times don't depend on how time is split, for any lengths.
4. Conservation: `final = initial + produced − consumed − spent + refunded` per item.
5. No negative stock.
6. Production never pushes `stock + reserved` above the cap (only refunds may exceed it).
7. The clock never decreases.
8. Nothing is produced before construction completes, and nothing during an upgrade.
9. `Σ assigned ≤ provided`, and `assigned ≤ MaxWorkers` for every building.
10. Busy builders ≤ builder slots. At most one research is active.
11. Labor accounting: a cycle completes exactly when the cumulative worker-seconds reach its labor.
12. No cycle starts after `productionUntil`.
13. Locked buildings are never placed, and research prerequisites are always respected.

## 11. Settlement report
Returned with every settled snapshot: `elapsedSeconds`, `productionSeconds` (= `min(elapsed, cap)`),
`produced{item:qty}`, `consumed{item:qty}`, `completedBuildings[id]`, `completedResearch[id]`, `eraChanged`.
It is persisted as aggregated ledger entries (one per item and direction per settlement).

## 12. Performance budget
Worst case: 64 producers with 10 s cycles over the full 8 h cap ≈ 184k events, plus timers.
Dormant time adds only timer events. Target: < 50 ms on a dev machine (CI uses a generous limit).

## 13. Planned extension — external deliveries (not in M0)
Market fills, expired-order returns and other cross-player transfers will arrive through a **mailbox**
(`26-open-market.md`). The settle passes them in as timed events (`t = max(tradeTime, T0)`), processed
before building completions, and ignoring storage caps. Keep the event sources in `Advance` a list,
not hard-coded, so this is an additive change.
